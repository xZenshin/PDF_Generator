import type { Bullet, Cv, CvSummary, Item, Section, SectionKind } from './types'

const BASE = '/api'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(BASE + path, {
    ...init,
    headers: init?.body ? { 'Content-Type': 'application/json' } : undefined,
  })
  if (!res.ok) {
    const detail = await res.text().catch(() => '')
    throw new Error(`${init?.method ?? 'GET'} ${path} failed (${res.status}) ${detail}`.trim())
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

const body = (value: unknown) => JSON.stringify(value)

// Requests mirror the C# record shapes: full replacement of an entity's own fields.
export type CvHeader = Pick<
  Cv,
  'name' | 'fullName' | 'headline' | 'email' | 'phone' | 'location' | 'website' | 'summary'
>
export type SectionBody = Pick<Section, 'title' | 'kind' | 'included'>
export type ItemBody = Pick<
  Item,
  'title' | 'organization' | 'location' | 'startDate' | 'endDate' | 'included'
>
export type BulletBody = Pick<Bullet, 'text' | 'included'>

export const api = {
  listCvs: () => request<CvSummary[]>('/cvs'),
  getCv: (id: string) => request<Cv>(`/cvs/${id}`),
  createCv: () => request<Cv>('/cvs', { method: 'POST' }),
  updateCv: (id: string, b: CvHeader) =>
    request<CvSummary>(`/cvs/${id}`, { method: 'PUT', body: body(b) }),
  deleteCv: (id: string) => request<void>(`/cvs/${id}`, { method: 'DELETE' }),

  addSection: (cvId: string, kind: SectionKind, title: string) =>
    request<Section>(`/cvs/${cvId}/sections`, {
      method: 'POST',
      body: body({ title, kind, included: true } satisfies SectionBody),
    }),
  updateSection: (id: string, b: SectionBody) =>
    request<void>(`/sections/${id}`, { method: 'PUT', body: body(b) }),
  deleteSection: (id: string) => request<void>(`/sections/${id}`, { method: 'DELETE' }),
  reorderSections: (cvId: string, ids: string[]) =>
    request<void>(`/cvs/${cvId}/sections/order`, { method: 'PUT', body: body({ ids }) }),

  addItem: (sectionId: string, b: ItemBody) =>
    request<Item>(`/sections/${sectionId}/items`, { method: 'POST', body: body(b) }),
  updateItem: (id: string, b: ItemBody) =>
    request<void>(`/items/${id}`, { method: 'PUT', body: body(b) }),
  deleteItem: (id: string) => request<void>(`/items/${id}`, { method: 'DELETE' }),
  reorderItems: (sectionId: string, ids: string[]) =>
    request<void>(`/sections/${sectionId}/items/order`, { method: 'PUT', body: body({ ids }) }),

  addBullet: (itemId: string, b: BulletBody) =>
    request<Bullet>(`/items/${itemId}/bullets`, { method: 'POST', body: body(b) }),
  updateBullet: (id: string, b: BulletBody) =>
    request<void>(`/bullets/${id}`, { method: 'PUT', body: body(b) }),
  deleteBullet: (id: string) => request<void>(`/bullets/${id}`, { method: 'DELETE' }),
  reorderBullets: (itemId: string, ids: string[]) =>
    request<void>(`/items/${itemId}/bullets/order`, { method: 'PUT', body: body({ ids }) }),

  pdfUrl: (id: string) => `${BASE}/cvs/${id}/pdf`,
}
