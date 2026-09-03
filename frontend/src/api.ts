import { toSaveFile } from './cvFile'
import type {
  AiStatus,
  Cv,
  SaveFile,
  TailoringPlan,
  TailoringRecommendation,
  TailorResponse,
} from './types'

const BASE = '/api'

/**
 * Every call is stateless: the CV travels in the request body, is used once, and is
 * forgotten. There is nothing on the server to load, update or delete.
 */

async function postCv(path: string, cv: Cv): Promise<Response> {
  const res = await fetch(BASE + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(toSaveFile(cv)),
  })
  if (!res.ok) throw new Error(await problemText(res))
  return res
}

async function postJson<T>(path: string, payload: unknown): Promise<T> {
  const res = await fetch(BASE + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  if (!res.ok) throw new Error(await problemText(res))
  return (await res.json()) as T
}

export const api = {
  /** The starter CV behind the New button. */
  template: async (): Promise<SaveFile> => {
    const res = await fetch(`${BASE}/cv/template`)
    if (!res.ok) throw new Error(await problemText(res))
    return (await res.json()) as SaveFile
  },

  /** Renders the current CV. Returns the response so the caller can take the blob. */
  pdf: (cv: Cv) => postCv('/cv/pdf', cv),

  /** Normalises and serialises the CV as a .cvjson save file. */
  export: (cv: Cv) => postCv('/cv/export', cv),

  /** Validates a file's text and returns it normalised, refs filled in. */
  import: async (text: string): Promise<SaveFile> => {
    const res = await fetch(`${BASE}/cv/import`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: text,
    })
    if (!res.ok) throw new Error(await problemText(res))
    return (await res.json()) as SaveFile
  },

  // ---- AI tailoring ---------------------------------------------------

  aiStatus: async (): Promise<AiStatus> => {
    const res = await fetch(`${BASE}/ai/status`)
    if (!res.ok) throw new Error(await problemText(res))
    return (await res.json()) as AiStatus
  },

  /** Asks the model what to include. Changes nothing — returns the proposal. */
  tailor: (cv: Cv, jobListing: string) =>
    postJson<TailorResponse>('/cv/tailor', { jobListing, cv: toSaveFile(cv) }),

  /** Applies a confirmed recommendation and hands back the amended CV. */
  applyTailoring: (cv: Cv, recommendation: TailoringRecommendation) =>
    postJson<{ plan: TailoringPlan; cv: SaveFile }>('/cv/tailor/apply', {
      cv: toSaveFile(cv),
      recommendation,
    }),
}

/** Pulls the human-readable reason out of an ASP.NET ProblemDetails response. */
async function problemText(res: Response): Promise<string> {
  const raw = await res.text().catch(() => '')
  try {
    const problem = JSON.parse(raw) as { detail?: string; title?: string }
    return problem.detail ?? problem.title ?? `Request failed (${res.status})`
  } catch {
    return raw || `Request failed (${res.status})`
  }
}
