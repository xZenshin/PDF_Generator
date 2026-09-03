/**
 * The public face of the app: static assets plus a proxy to the API.
 *
 * Cloudflare Workers cannot run the .NET API — QuestPDF needs native libraries — so the
 * backend lives in a container elsewhere and this Worker forwards `/api/*` to it. That
 * keeps the browser on one origin, exactly as the Vite dev proxy does, so there is no
 * CORS to configure and the API's own hostname never reaches the client.
 *
 * API_ORIGIN is a Worker secret, e.g. https://cv-builder-api.<region>.azurecontainerapps.io
 */
interface Env {
  ASSETS: { fetch(request: Request): Promise<Response> }
  API_ORIGIN?: string
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url)

    if (!url.pathname.startsWith('/api/')) return env.ASSETS.fetch(request)

    const origin = env.API_ORIGIN?.replace(/\/+$/, '')
    if (!origin) {
      return problem(
        'This deployment has no API configured. Set the API_ORIGIN secret on the Worker.',
        503,
      )
    }

    // Same method, same headers, same body — including the X-Cv-Auth passphrase and the
    // posted CV. Only the host changes.
    const upstream = new Request(origin + url.pathname + url.search, request)
    upstream.headers.set('Host', new URL(origin).host)

    try {
      return await fetch(upstream)
    } catch (err) {
      // A cold container that has scaled to zero is the usual cause.
      return problem(`Could not reach the API: ${err instanceof Error ? err.message : err}`, 502)
    }
  },
}

/** Shaped like ASP.NET's ProblemDetails so the SPA's error handling reads it unchanged. */
function problem(detail: string, status: number): Response {
  return new Response(JSON.stringify({ title: 'Bad Gateway', status, detail }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}
