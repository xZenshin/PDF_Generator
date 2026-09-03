import { useCallback, useEffect, useRef, useState } from 'react'
import { api, type BulletBody, type CvHeader, type ItemBody, type SectionBody } from './api'
import type { Bullet, Cv, Item, Section, SectionKind } from './types'

export type SaveStatus = 'idle' | 'saving' | 'saved' | 'error'

/** Typing keeps the preview instant; the write is coalesced to one request per field. */
const DEBOUNCE_MS = 500

/**
 * Holds the CV being edited. Every mutation updates local state immediately and
 * schedules the matching API call, keyed per entity so rapid typing collapses into
 * one request. `flush()` forces everything out — call it before exporting the PDF,
 * which is rendered server-side from whatever is stored.
 */
export function useCvEditor(cvId: string | null) {
  const [cv, setCv] = useState<Cv | null>(null)
  const [loading, setLoading] = useState(false)
  const [status, setStatus] = useState<SaveStatus>('idle')
  const [error, setError] = useState<string | null>(null)

  // Mutations read the latest tree from here, so several edits in one tick compose.
  const cvRef = useRef<Cv | null>(null)
  const inflight = useRef(new Set<Promise<unknown>>())
  const queued = useRef(new Map<string, { timer: number; fire: () => void }>())

  const commit = useCallback((next: Cv) => {
    cvRef.current = next
    setCv(next)
  }, [])

  const track = useCallback(<T,>(p: Promise<T>): Promise<T | undefined> => {
    inflight.current.add(p)
    setStatus('saving')
    const done = (failure?: unknown) => {
      inflight.current.delete(p)
      if (failure !== undefined) {
        setError(failure instanceof Error ? failure.message : String(failure))
        setStatus('error')
      } else if (inflight.current.size === 0 && queued.current.size === 0) {
        setStatus('saved')
      }
    }
    return p.then(
      (value) => {
        done()
        return value
      },
      (err) => {
        done(err ?? new Error('Request failed'))
        return undefined
      },
    )
  }, [])

  const later = useCallback(
    (key: string, send: () => Promise<unknown>) => {
      const existing = queued.current.get(key)
      if (existing) window.clearTimeout(existing.timer)

      const fire = () => {
        queued.current.delete(key)
        void track(send())
      }
      queued.current.set(key, { timer: window.setTimeout(fire, DEBOUNCE_MS), fire })
      setStatus('saving')
    },
    [track],
  )

  const cancel = useCallback((keys: string[]) => {
    for (const key of keys) {
      const queuedSave = queued.current.get(key)
      if (queuedSave) {
        window.clearTimeout(queuedSave.timer)
        queued.current.delete(key)
      }
    }
  }, [])

  const flush = useCallback(async () => {
    for (const entry of [...queued.current.values()]) {
      window.clearTimeout(entry.timer)
      entry.fire()
    }
    await Promise.allSettled([...inflight.current])
  }, [])

  const load = useCallback(
    async (id: string) => {
      setLoading(true)
      setError(null)
      try {
        const next = await api.getCv(id)
        commit(next)
      } catch (err) {
        setError(err instanceof Error ? err.message : String(err))
      } finally {
        setLoading(false)
      }
    },
    [commit],
  )

  useEffect(() => {
    if (!cvId) {
      cvRef.current = null
      setCv(null)
      return
    }
    void load(cvId)
  }, [cvId, load])

  // ---- Header -----------------------------------------------------------

  const updateHeader = useCallback(
    (patch: Partial<CvHeader>) => {
      const current = cvRef.current
      if (!current) return
      const next = { ...current, ...patch }
      commit(next)
      later(`cv:${next.id}`, () => api.updateCv(next.id, headerOf(next)))
    },
    [commit, later],
  )

  // ---- Sections ---------------------------------------------------------

  const addSection = useCallback(
    async (kind: SectionKind) => {
      const current = cvRef.current
      if (!current) return
      const created = await track(api.addSection(current.id, kind, defaultSectionTitle(kind)))
      if (!created) return

      // A free-form section is just a title and prose, so hand the user a paragraph
      // to type into rather than making them build the scaffolding first.
      let section = created
      if (kind === 'FreeForm') {
        const item = await track(api.addItem(created.id, blankItem(kind)))
        if (item) {
          const paragraph = await track(api.addBullet(item.id, { text: '', included: true }))
          section = { ...created, items: [{ ...item, bullets: paragraph ? [paragraph] : [] }] }
        }
      }

      const now = cvRef.current
      if (now) commit({ ...now, sections: [...now.sections, section] })
      return section
    },
    [commit, track],
  )

  const updateSection = useCallback(
    (id: string, patch: Partial<SectionBody>, immediate = false) => {
      const current = cvRef.current
      if (!current) return
      const next = mapSections(current, (s) => (s.id === id ? { ...s, ...patch } : s))
      commit(next)

      const section = next.sections.find((s) => s.id === id)
      if (!section) return
      const send = () => api.updateSection(id, bodyOfSection(section))
      if (immediate) void track(send())
      else later(`section:${id}`, send)
    },
    [commit, later, track],
  )

  const removeSection = useCallback(
    (id: string) => {
      const current = cvRef.current
      if (!current) return
      const section = current.sections.find((s) => s.id === id)
      if (section) cancel(subtreeKeys(section))

      commit({ ...current, sections: current.sections.filter((s) => s.id !== id) })
      void track(api.deleteSection(id))
    },
    [cancel, commit, track],
  )

  const moveSection = useCallback(
    (id: string, delta: number) => {
      const current = cvRef.current
      if (!current) return
      const sections = moved(current.sections, id, delta)
      if (!sections) return
      commit({ ...current, sections })
      void track(api.reorderSections(current.id, sections.map((s) => s.id)))
    },
    [commit, track],
  )

  // ---- Items ------------------------------------------------------------

  const addItem = useCallback(
    async (sectionId: string) => {
      const current = cvRef.current
      if (!current) return
      const section = current.sections.find((s) => s.id === sectionId)
      const created = await track(api.addItem(sectionId, blankItem(section?.kind ?? 'Timeline')))
      if (!created) return
      const now = cvRef.current
      if (!now) return
      commit(
        mapSections(now, (s) =>
          s.id === sectionId ? { ...s, items: [...s.items, created] } : s,
        ),
      )
      return created
    },
    [commit, track],
  )

  const updateItem = useCallback(
    (id: string, patch: Partial<ItemBody>, immediate = false) => {
      const current = cvRef.current
      if (!current) return
      const next = mapItems(current, (i) => (i.id === id ? { ...i, ...patch } : i))
      commit(next)

      const item = findItem(next, id)
      if (!item) return
      const send = () => api.updateItem(id, bodyOfItem(item))
      if (immediate) void track(send())
      else later(`item:${id}`, send)
    },
    [commit, later, track],
  )

  const removeItem = useCallback(
    (id: string) => {
      const current = cvRef.current
      if (!current) return
      const item = findItem(current, id)
      if (item) cancel([`item:${id}`, ...item.bullets.map((b) => `bullet:${b.id}`)])

      commit(mapSections(current, (s) => ({ ...s, items: s.items.filter((i) => i.id !== id) })))
      void track(api.deleteItem(id))
    },
    [cancel, commit, track],
  )

  const moveItem = useCallback(
    (sectionId: string, id: string, delta: number) => {
      const current = cvRef.current
      if (!current) return
      const section = current.sections.find((s) => s.id === sectionId)
      const items = section && moved(section.items, id, delta)
      if (!items) return
      commit(mapSections(current, (s) => (s.id === sectionId ? { ...s, items } : s)))
      void track(api.reorderItems(sectionId, items.map((i) => i.id)))
    },
    [commit, track],
  )

  // ---- Bullets ----------------------------------------------------------

  const addBullet = useCallback(
    async (itemId: string) => {
      const created = await track(api.addBullet(itemId, { text: '', included: true }))
      if (!created) return
      const now = cvRef.current
      if (!now) return
      commit(mapItems(now, (i) => (i.id === itemId ? { ...i, bullets: [...i.bullets, created] } : i)))
      return created
    },
    [commit, track],
  )

  const updateBullet = useCallback(
    (id: string, patch: Partial<BulletBody>, immediate = false) => {
      const current = cvRef.current
      if (!current) return
      const next = mapBullets(current, (b) => (b.id === id ? { ...b, ...patch } : b))
      commit(next)

      const bullet = findBullet(next, id)
      if (!bullet) return
      const send = () => api.updateBullet(id, { text: bullet.text, included: bullet.included })
      if (immediate) void track(send())
      else later(`bullet:${id}`, send)
    },
    [commit, later, track],
  )

  const removeBullet = useCallback(
    (id: string) => {
      const current = cvRef.current
      if (!current) return
      cancel([`bullet:${id}`])
      commit(mapItems(current, (i) => ({ ...i, bullets: i.bullets.filter((b) => b.id !== id) })))
      void track(api.deleteBullet(id))
    },
    [cancel, commit, track],
  )

  const moveBullet = useCallback(
    (itemId: string, id: string, delta: number) => {
      const current = cvRef.current
      if (!current) return
      const item = findItem(current, itemId)
      const bullets = item && moved(item.bullets, id, delta)
      if (!bullets) return
      commit(mapItems(current, (i) => (i.id === itemId ? { ...i, bullets } : i)))
      void track(api.reorderBullets(itemId, bullets.map((b) => b.id)))
    },
    [commit, track],
  )

  return {
    cv,
    loading,
    status,
    error,
    dismissError: useCallback(() => setError(null), []),
    flush,
    reload: useCallback(() => (cvId ? load(cvId) : Promise.resolve()), [cvId, load]),
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

const headerOf = (cv: Cv): CvHeader => ({
  name: cv.name,
  fullName: cv.fullName,
  headline: cv.headline,
  email: cv.email,
  phone: cv.phone,
  location: cv.location,
  website: cv.website,
  summary: cv.summary,
  style: cv.style,
})

const bodyOfSection = (s: Section): SectionBody => ({
  title: s.title,
  kind: s.kind,
  included: s.included,
  twoColumns: s.twoColumns,
})

const bodyOfItem = (i: Item): ItemBody => ({
  title: i.title,
  organization: i.organization,
  location: i.location,
  startDate: i.startDate,
  endDate: i.endDate,
  included: i.included,
})

const mapSections = (cv: Cv, fn: (s: Section) => Section): Cv => ({
  ...cv,
  sections: cv.sections.map(fn),
})

const mapItems = (cv: Cv, fn: (i: Item) => Item): Cv =>
  mapSections(cv, (s) => ({ ...s, items: s.items.map(fn) }))

const mapBullets = (cv: Cv, fn: (b: Bullet) => Bullet): Cv =>
  mapItems(cv, (i) => ({ ...i, bullets: i.bullets.map(fn) }))

const findItem = (cv: Cv, id: string): Item | undefined =>
  cv.sections.flatMap((s) => s.items).find((i) => i.id === id)

const findBullet = (cv: Cv, id: string): Bullet | undefined =>
  cv.sections.flatMap((s) => s.items).flatMap((i) => i.bullets).find((b) => b.id === id)

const subtreeKeys = (section: Section): string[] => [
  `section:${section.id}`,
  ...section.items.flatMap((i) => [`item:${i.id}`, ...i.bullets.map((b) => `bullet:${b.id}`)]),
]

/** Returns the list with `id` shifted by `delta`, or undefined when it cannot move. */
function moved<T extends { id: string }>(list: T[], id: string, delta: number): T[] | undefined {
  const from = list.findIndex((x) => x.id === id)
  const to = from + delta
  if (from < 0 || to < 0 || to >= list.length) return undefined

  const next = [...list]
  const [row] = next.splice(from, 1)
  next.splice(to, 0, row)
  return next
}

const DEFAULT_SECTION_TITLES: Record<SectionKind, string> = {
  Timeline: 'Experience',
  Grouped: 'Skills',
  Bullets: 'Highlights',
  FreeForm: 'Personal Life',
}

const defaultSectionTitle = (kind: SectionKind) => DEFAULT_SECTION_TITLES[kind]

const blankItem = (kind: SectionKind): ItemBody => ({
  title: kind === 'Grouped' ? 'Category' : '',
  organization: '',
  location: '',
  startDate: '',
  endDate: '',
  included: true,
})
