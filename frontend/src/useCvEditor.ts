import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { blankBullet, blankItem, blankSection, toEditable, toSaveFile } from './cvFile'
import type { Bullet, Cv, Item, SectionKind, Section } from './types'

const DRAFT_KEY = 'cvbuilder.draft'

/** Autosave to the browser is cheap, but not on every keystroke. */
const AUTOSAVE_MS = 400

/**
 * Holds the CV being edited, entirely in the browser. There is no server-side store,
 * so every edit is a local state update — instant, and free to host.
 *
 * A draft is mirrored into localStorage so a refresh or a closed tab does not lose
 * work. That is a convenience, not a backup: it is per-browser, and clearing site data
 * clears it. The `.cvjson` file is the real save.
 */
export function useCvEditor() {
  const [cv, setCv] = useState<Cv | null>(null)
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)

  /** True once edits have been made that are not in any file yet. */
  const [unsaved, setUnsaved] = useState(false)

  // Mutations read the latest tree from here, so several edits in one tick compose.
  const cvRef = useRef<Cv | null>(null)
  const autosave = useRef<number>(0)

  const commit = useCallback((next: Cv, marksUnsaved = true) => {
    cvRef.current = next
    setCv(next)
    if (marksUnsaved) setUnsaved(true)

    window.clearTimeout(autosave.current)
    autosave.current = window.setTimeout(() => {
      try {
        localStorage.setItem(DRAFT_KEY, JSON.stringify(toSaveFile(next)))
      } catch {
        // A full or disabled localStorage must not break editing.
      }
    }, AUTOSAVE_MS)
  }, [])

  /** Takes a CV wholesale — from a file, from the template, or from tailoring. */
  const adopt = useCallback(
    (next: Cv, marksUnsaved = true) => {
      commit(next, marksUnsaved)
    },
    [commit],
  )

  const markSaved = useCallback(() => setUnsaved(false), [])

  // Restore the draft, or start from the server's starter CV.
  const started = useRef(false)
  useEffect(() => {
    if (started.current) return
    started.current = true

    const draft = (() => {
      try {
        return localStorage.getItem(DRAFT_KEY)
      } catch {
        return null
      }
    })()

    if (draft) {
      try {
        commit(toEditable(JSON.parse(draft)), false)
        setReady(true)
        return
      } catch {
        // A corrupt draft is not worth blocking on; fall through to the template.
      }
    }

    api
      .template()
      .then((file) => commit(toEditable(file), false))
      .catch((err: unknown) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setReady(true))
  }, [commit])

  const startNew = useCallback(async () => {
    const file = await api.template()
    commit(toEditable(file), false)
  }, [commit])

  // ---- Header -----------------------------------------------------------

  const updateHeader = useCallback(
    (patch: Partial<Omit<Cv, 'uid' | 'sections'>>) => {
      const current = cvRef.current
      if (!current) return
      commit({ ...current, ...patch })
    },
    [commit],
  )

  // ---- Sections ---------------------------------------------------------

  const addSection = useCallback(
    (kind: SectionKind) => {
      const current = cvRef.current
      if (!current) return
      commit({ ...current, sections: [...current.sections, blankSection(kind)] })
    },
    [commit],
  )

  const updateSection = useCallback(
    (uid: string, patch: Partial<Section>) => {
      const current = cvRef.current
      if (!current) return
      commit(mapSections(current, (s) => (s.uid === uid ? { ...s, ...patch } : s)))
    },
    [commit],
  )

  const removeSection = useCallback(
    (uid: string) => {
      const current = cvRef.current
      if (!current) return
      commit({ ...current, sections: current.sections.filter((s) => s.uid !== uid) })
    },
    [commit],
  )

  const moveSection = useCallback(
    (uid: string, delta: number) => {
      const current = cvRef.current
      if (!current) return
      const sections = moved(current.sections, uid, delta)
      if (sections) commit({ ...current, sections })
    },
    [commit],
  )

  // ---- Items ------------------------------------------------------------

  const addItem = useCallback(
    (sectionUid: string) => {
      const current = cvRef.current
      if (!current) return
      commit(
        mapSections(current, (s) =>
          s.uid === sectionUid ? { ...s, items: [...s.items, blankItem(s.kind)] } : s,
        ),
      )
    },
    [commit],
  )

  const updateItem = useCallback(
    (uid: string, patch: Partial<Item>) => {
      const current = cvRef.current
      if (!current) return
      commit(mapItems(current, (i) => (i.uid === uid ? { ...i, ...patch } : i)))
    },
    [commit],
  )

  const removeItem = useCallback(
    (uid: string) => {
      const current = cvRef.current
      if (!current) return
      commit(mapSections(current, (s) => ({ ...s, items: s.items.filter((i) => i.uid !== uid) })))
    },
    [commit],
  )

  const moveItem = useCallback(
    (sectionUid: string, uid: string, delta: number) => {
      const current = cvRef.current
      if (!current) return
      const section = current.sections.find((s) => s.uid === sectionUid)
      const items = section && moved(section.items, uid, delta)
      if (items) commit(mapSections(current, (s) => (s.uid === sectionUid ? { ...s, items } : s)))
    },
    [commit],
  )

  // ---- Bullets ----------------------------------------------------------

  const addBullet = useCallback(
    (itemUid: string) => {
      const current = cvRef.current
      if (!current) return
      commit(
        mapItems(current, (i) =>
          i.uid === itemUid ? { ...i, bullets: [...i.bullets, blankBullet()] } : i,
        ),
      )
    },
    [commit],
  )

  const updateBullet = useCallback(
    (uid: string, patch: Partial<Bullet>) => {
      const current = cvRef.current
      if (!current) return
      commit(mapBullets(current, (b) => (b.uid === uid ? { ...b, ...patch } : b)))
    },
    [commit],
  )

  const removeBullet = useCallback(
    (uid: string) => {
      const current = cvRef.current
      if (!current) return
      commit(mapItems(current, (i) => ({ ...i, bullets: i.bullets.filter((b) => b.uid !== uid) })))
    },
    [commit],
  )

  const moveBullet = useCallback(
    (itemUid: string, uid: string, delta: number) => {
      const current = cvRef.current
      if (!current) return
      const item = findItem(current, itemUid)
      const bullets = item && moved(item.bullets, uid, delta)
      if (bullets) commit(mapItems(current, (i) => (i.uid === itemUid ? { ...i, bullets } : i)))
    },
    [commit],
  )

  return {
    cv,
    ready,
    error,
    unsaved,
    dismissError: useCallback(() => setError(null), []),
    adopt,
    markSaved,
    startNew,
    actions: {
      updateHeader,
      addSection,
      updateSection,
      removeSection,
      moveSection,
      addItem,
      updateItem,
      removeItem,
      moveItem,
      addBullet,
      updateBullet,
      removeBullet,
      moveBullet,
    },
  }
}

export type CvActions = ReturnType<typeof useCvEditor>['actions']

// ---- Pure helpers -------------------------------------------------------

const mapSections = (cv: Cv, fn: (s: Section) => Section): Cv => ({
  ...cv,
  sections: cv.sections.map(fn),
})

const mapItems = (cv: Cv, fn: (i: Item) => Item): Cv =>
  mapSections(cv, (s) => ({ ...s, items: s.items.map(fn) }))

const mapBullets = (cv: Cv, fn: (b: Bullet) => Bullet): Cv =>
  mapItems(cv, (i) => ({ ...i, bullets: i.bullets.map(fn) }))

const findItem = (cv: Cv, uid: string): Item | undefined =>
  cv.sections.flatMap((s) => s.items).find((i) => i.uid === uid)

/** Returns the list with `uid` shifted by `delta`, or undefined when it cannot move. */
function moved<T extends { uid: string }>(list: T[], uid: string, delta: number): T[] | undefined {
  const from = list.findIndex((x) => x.uid === uid)
  const to = from + delta
  if (from < 0 || to < 0 || to >= list.length) return undefined

  const next = [...list]
  const [row] = next.splice(from, 1)
  next.splice(to, 0, row)
  return next
}
