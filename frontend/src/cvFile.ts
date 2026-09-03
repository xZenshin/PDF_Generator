import type { Bullet, Cv, Item, SaveFile, Section, SectionKind } from './types'

/**
 * Converts between the editor's tree and the save-file shape the API speaks.
 *
 * The only difference between them is `uid`: a locally generated React key that must
 * survive edits but must never reach the server or a file. Refs (`id`) travel the other
 * way — the server assigns them, so a freshly added row carries an empty one until the
 * CV next passes through an endpoint.
 */

const newUid = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `uid-${Math.random().toString(36).slice(2)}-${Date.now()}`

export const uid = newUid

/** Adds local keys to a CV that came from the API or a file. */
export function toEditable(file: SaveFile): Cv {
  const cv = file.cv
  return {
    uid: newUid(),
    name: cv.name ?? '',
    fullName: cv.fullName ?? '',
    headline: cv.headline ?? '',
    email: cv.email ?? '',
    phone: cv.phone ?? '',
    location: cv.location ?? '',
    website: cv.website ?? '',
    summary: cv.summary ?? '',
    style: cv.style ?? 'Base',
    sections: (cv.sections ?? []).map((section) => ({
      uid: newUid(),
      id: section.id ?? '',
      title: section.title ?? '',
      kind: section.kind ?? 'Timeline',
      included: section.included ?? true,
      twoColumns: section.twoColumns ?? false,
      items: (section.items ?? []).map((item) => ({
        uid: newUid(),
        id: item.id ?? '',
        title: item.title ?? '',
        organization: item.organization ?? '',
        location: item.location ?? '',
        startDate: item.startDate ?? '',
        endDate: item.endDate ?? '',
        included: item.included ?? true,
        bullets: (item.bullets ?? []).map((bullet) => ({
          uid: newUid(),
          id: bullet.id ?? '',
          text: bullet.text ?? '',
          included: bullet.included ?? true,
        })),
      })),
    })),
  }
}

/** Strips local keys, producing the body to POST. */
export function toSaveFile(cv: Cv): SaveFile {
  return {
    format: 'cvbuilder.cv',
    version: 1,
    exportedAt: new Date().toISOString(),
    cv: {
      name: cv.name,
      fullName: cv.fullName,
      headline: cv.headline,
      email: cv.email,
      phone: cv.phone,
      location: cv.location,
      website: cv.website,
      summary: cv.summary,
      style: cv.style,
      sections: cv.sections.map((section) => ({
        id: section.id,
        title: section.title,
        kind: section.kind,
        included: section.included,
        twoColumns: section.twoColumns,
        items: section.items.map((item) => ({
          id: item.id,
          title: item.title,
          organization: item.organization,
          location: item.location,
          startDate: item.startDate,
          endDate: item.endDate,
          included: item.included,
          bullets: item.bullets.map((bullet) => ({
            id: bullet.id,
            text: bullet.text,
            included: bullet.included,
          })),
        })),
      })),
    },
  } as SaveFile
}

// ---- New rows -----------------------------------------------------------

export const blankBullet = (): Bullet => ({ uid: newUid(), id: '', text: '', included: true })

export const blankItem = (kind: SectionKind): Item => ({
  uid: newUid(),
  id: '',
  title: kind === 'Grouped' ? 'Category' : '',
  organization: '',
  location: '',
  startDate: '',
  endDate: '',
  included: true,
  bullets: [],
})

const DEFAULT_SECTION_TITLES: Record<SectionKind, string> = {
  Timeline: 'Experience',
  Grouped: 'Skills',
  Bullets: 'Highlights',
  FreeForm: 'Personal Life',
}

export function blankSection(kind: SectionKind): Section {
  const section: Section = {
    uid: newUid(),
    id: '',
    title: DEFAULT_SECTION_TITLES[kind],
    kind,
    included: true,
    twoColumns: false,
    items: [],
  }

  // A free-form section is just a title and prose, so hand the user a paragraph to
  // type into rather than making them build the scaffolding first.
  if (kind === 'FreeForm') {
    section.items = [{ ...blankItem(kind), bullets: [blankBullet()] }]
  }

  return section
}
