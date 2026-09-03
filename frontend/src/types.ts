export type CvStyle = 'Base' | 'Mono'

export type SectionKind = 'Timeline' | 'Grouped' | 'Bullets' | 'FreeForm'

export interface Bullet {
  id: string
  /** Stable handle an LLM can point at, e.g. "exp_003". */
  ref: string
  text: string
  sortOrder: number
  included: boolean
}

export interface Item {
  id: string
  ref: string
  title: string
  organization: string
  location: string
  startDate: string
  endDate: string
  sortOrder: number
  included: boolean
  bullets: Bullet[]
}

export interface Section {
  id: string
  ref: string
  title: string
  kind: SectionKind
  sortOrder: number
  included: boolean
  /** Bullets sections only: run the section's bullets in two columns. */
  twoColumns: boolean
  items: Item[]
}

export interface Cv {
  id: string
  name: string
  fullName: string
  headline: string
  email: string
  phone: string
  location: string
  website: string
  summary: string
  style: CvStyle
  updatedAt: string
  sections: Section[]
}

export interface CvSummary {
  id: string
  name: string
  fullName: string
  updatedAt: string
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
