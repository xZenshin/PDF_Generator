export type CvStyle = 'Base' | 'Mono'

export type SectionKind = 'Timeline' | 'Grouped' | 'Bullets' | 'FreeForm'

/**
 * The CV as the browser holds it — the save-file shape, plus a `uid` on each node.
 *
 * `id` is the stable ref an LLM points at ("exp_003"); it is assigned server-side and
 * is blank on rows you have only just added. `uid` is a local React key, generated
 * here and never sent anywhere. Order is array order: there are no sort fields.
 */
export interface Bullet {
  uid: string
  id: string
  text: string
  included: boolean
}

export interface Item {
  uid: string
  id: string
  title: string
  organization: string
  location: string
  startDate: string
  endDate: string
  included: boolean
  bullets: Bullet[]
}

export interface Section {
  uid: string
  id: string
  title: string
  kind: SectionKind
  included: boolean
  /** Bullets sections only: run the section's bullets in two columns. */
  twoColumns: boolean
  items: Item[]
}

export interface Cv {
  uid: string
  name: string
  fullName: string
  headline: string
  email: string
  phone: string
  location: string
  website: string
  summary: string
  style: CvStyle
  sections: Section[]
}

/** What the API sends and receives: the same tree without the local uids. */
export interface SaveFile {
  format: string
  version: number
  exportedAt: string
  cv: Omit<Cv, 'uid'>
}

export const SECTION_KINDS: { value: SectionKind; label: string; hint: string }[] = [
  { value: 'Timeline', label: 'Timeline', hint: 'Role, employer, dates and bullets' },
  { value: 'Grouped', label: 'Grouped', hint: 'A label plus a comma-separated list' },
  { value: 'Bullets', label: 'Bullets', hint: 'Plain bullet points' },
  { value: 'FreeForm', label: 'Free form', hint: 'A title and paragraphs of prose — e.g. Personal Life' },
]

export const CV_STYLES: { value: CvStyle; label: string; hint: string }[] = [
  { value: 'Base', label: 'Base', hint: 'Soft greys, semibold headings, hairline rules' },
  { value: 'Mono', label: 'Mono', hint: 'Tracked capitals, heavy grey rules, black body text' },
]

// ---- AI tailoring -------------------------------------------------------

export interface AiStatus {
  configured: boolean
  model: string
  /** True when the API wants the shared passphrase before it will call DeepSeek. */
  authRequired: boolean
}

export interface TailoringRecommendation {
  include: string[] | null
  exclude: string[] | null
}

export interface PlannedChange {
  ref: string
  kind: 'section' | 'entry' | 'bullet'
  label: string
  include: boolean
  /** Added by the server so an included child can actually print. */
  cascaded: boolean
}

export interface TailoringPlan {
  changes: PlannedChange[]
  alreadyCorrect: number
  unrecognised: string[]
  contradictory: string[]
}

export interface TailorResponse {
  model: string
  recommendation: TailoringRecommendation
  plan: TailoringPlan
}
