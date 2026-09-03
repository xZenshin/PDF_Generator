export type CvStyle = 'Base' | 'Mono'

export type SectionKind = 'Timeline' | 'Grouped' | 'Bullets' | 'FreeForm'

export interface Bullet {
  id: string
  text: string
  sortOrder: number
  included: boolean
}

export interface Item {
  id: string
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
  title: string
  kind: SectionKind
  sortOrder: number
  included: boolean
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
