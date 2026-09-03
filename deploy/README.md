# Hosting

Two halves, because Cloudflare Workers cannot run this API: QuestPDF renders through
SkiaSharp's native libraries, and Workers run only JavaScript and WASM.

```
browser ─▶ Cloudflare Worker ──/api/*──▶ Azure Container App (.NET, QuestPDF)
             static SPA assets                 scaled to zero when idle
```

The browser only ever talks to the Worker, exactly as it only ever talks to Vite in
development. No CORS to configure, and the container's hostname stays out of the bundle.

## 1. The API on Azure Container Apps

Built from [`backend/Dockerfile`](../backend/Dockerfile) — `aspnet:10.0` plus
`libfontconfig1`, which is all Skia needs. The fonts are QuestPDF's own Lato, embedded in
the PDF, so output is byte-identical to a Windows build apart from the timestamp.

```bash
az login
az group create --name cv-builder --location westeurope

az containerapp env create \
  --name cv-builder-env --resource-group cv-builder --location westeurope

# Builds the image in Azure from backend/ and creates the app with public ingress.
az containerapp up \
  --name cv-builder-api --resource-group cv-builder \
  --environment cv-builder-env \
  --source backend --target-port 8080 --ingress external
```

Then the two secrets, and scale-to-zero so an idle CV builder bills nothing:

```bash
az containerapp secret set -n cv-builder-api -g cv-builder \
  --secrets deepseek-key=sk-… auth-password=…

az containerapp update -n cv-builder-api -g cv-builder \
  --set-env-vars DeepSeek__ApiKey=secretref:deepseek-key \
                 Auth__Password=secretref:auth-password \
  --min-replicas 0 --max-replicas 1
```

`min-replicas 0` means the first request after an idle spell waits a few seconds for a
cold start. For one user that is the right trade.

Check it: `curl https://<app>.azurecontainerapps.io/api/ai/status` should say
`{"configured":true,…,"authRequired":true}`.

Optional hardening: `az containerapp ingress access-restriction set` can limit inbound
traffic to Cloudflare's published ranges, so nobody reaches the API except through the
Worker.

## 2. The SPA on Cloudflare Workers

`frontend/wrangler.jsonc` serves `frontend/dist` as static assets and hands `/api/*` to
[`frontend/worker/index.ts`](../frontend/worker/index.ts), which forwards it upstream.

In the Cloudflare dashboard: **Workers & Pages → Create → Connect to Git**, pick this
repository, and set

| Field | Value |
| ----- | ----- |
| Root directory | `frontend` |
| Build command | `npm run build` |
| Deploy command | `npx wrangler deploy` |

Then give the Worker the backend's address — as a **secret**, so a deploy never
overwrites it and it is not in the repository:

```bash
cd frontend
npx wrangler secret put API_ORIGIN     # https://<app>.azurecontainerapps.io
```

Pushes to the connected branch build and deploy from then on. The API is deployed
separately with `az containerapp up`, which suits how rarely it changes.

## Running the hosted shape locally

```bash
docker build -t cv-builder-api backend
docker run --rm -p 8080:8080 -e Auth__Password=… -e DeepSeek__ApiKey=… cv-builder-api

cd frontend && npm run build && npx wrangler dev    # reads API_ORIGIN from .dev.vars
```

`.dev.vars` holds `API_ORIGIN=http://localhost:8080` and is git-ignored. This is the
combination the deployed pair uses, so it catches proxy and routing mistakes that
`npm run dev` cannot.

## Configuration summary

| Setting | Where it lives in production |
| ------- | ---------------------------- |
| `Auth__Password` | Container App secret `auth-password` |
| `DeepSeek__ApiKey` | Container App secret `deepseek-key` |
| `API_ORIGIN` | Worker secret |
| Port | 8080, fixed by the Dockerfile's `ASPNETCORE_URLS` |
