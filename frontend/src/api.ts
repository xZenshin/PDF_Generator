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

/** Where the tailoring passphrase is remembered, if the user asks for that. */
const PASSPHRASE_KEY = 'cvbuilder.passphrase'

/**
 * The DeepSeek endpoints are the only ones that cost money, so they are the only ones
 * that ask for the shared passphrase. It travels in a header on each call — there is no
 * session to establish — and is remembered per browser only if the user ticks the box.
 */
export const passphrase = {
  load(): string {
    try {
      return localStorage.getItem(PASSPHRASE_KEY) ?? ''
    } catch {
      return ''
    }
  },
  remember(value: string) {
    try {
      localStorage.setItem(PASSPHRASE_KEY, value)
    } catch {
      /* private mode, or storage full: not worth bothering the user about */
    }
  },
  forget() {
    try {
      localStorage.removeItem(PASSPHRASE_KEY)
    } catch {
      /* as above */
    }
  },
}

/** The passphrase header, omitted entirely when there is nothing to send. */
function authHeader(secret: string): Record<string, string> {
  return secret ? { 'X-Cv-Auth': secret } : {}
}

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

async function postJson<T>(
  path: string,
  payload: unknown,
  headers: Record<string, string> = {},
): Promise<T> {
  const res = await fetch(BASE + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
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
  tailor: (cv: Cv, jobListing: string, secret = '') =>
    postJson<TailorResponse>(
      '/cv/tailor',
      { jobListing, cv: toSaveFile(cv) },
      authHeader(secret),
    ),

  /** Applies a confirmed recommendation and hands back the amended CV. */
  applyTailoring: (cv: Cv, recommendation: TailoringRecommendation, secret = '') =>
    postJson<{ plan: TailoringPlan; cv: SaveFile }>(
      '/cv/tailor/apply',
      { cv: toSaveFile(cv), recommendation },
      authHeader(secret),
    ),
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
