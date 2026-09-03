# CV Builder

A small self-hosted web app for building a CV and exporting it to PDF. You keep one
**master CV** with everything you have ever done, then tick entries in or out to produce
a PDF tailored to a specific application. Nothing is deleted when you exclude it.

Paste a job listing and DeepSeek will pick which parts belong in that application.

- **Frontend** — React 19 + TypeScript + Vite
- **Backend** — ASP.NET Core minimal API (.NET 10), stateless
- **PDF** — QuestPDF, rendered server-side
- **Storage** — none on the server. Your CV is a `.cvjson` file on your own disk.

## Architecture

```
┌──────────────────────┐   POST the whole CV per call  ┌───────────────────────────┐
│  React SPA (Vite)    │ ───────────────────────────▶  │  ASP.NET Core minimal API │
│                      │                               │                           │
│  Editor  │  Preview  │ ◀── PDF / .cvjson / plan ──── │  QuestPDF   ·   DeepSeek  │
└──────────┬───────────┘                               └───────────────────────────┘
           │                                                   (no state)
   ┌───────▼────────┐
   │  localStorage  │  autosaved draft
   └────────────────┘
   ┌────────────────┐
   │  .cvjson file  │  the real save
   └────────────────┘
```

Five deliberate choices, each to keep the project small:

**The server keeps nothing.** There is no database, no session and no cache. The browser
owns the document and posts the whole CV with each request; the API renders it, or asks
DeepSeek about it, and forgets it. That is why hosting is one stateless container with
nothing to pay for, back up or migrate — and why there is no login, no account, and no
copy of your CV on someone else's machine.

**The PDF is rendered on the server, from the CV you posted.** The browser preview is a
lookalike, not the source of truth — `Preview.tsx` and `CvPdfGenerator.cs` apply the same
inclusion and ordering rules. The cost is that the two renderers must be kept in step by
hand; they are ~100 lines each and sit next to each other in the repo for that reason.

**Inclusion is a flag, not a delete.** `Section`, `CvItem` and `Bullet` each carry
`Included`. The PDF generator filters on it, and drops any section or entry that would
print empty once filtered. This is the whole tailoring feature — no separate "export
document", no copying.

**Styles are typography, not layout.** `CvTheme` is a record of type sizes, weights,
tracking, rule weights, colour and spacing — nothing in it can move content around, which
is what keeps `Base` and `Mono` the same document. Adding a third style means adding one
more `CvTheme` and its CSS counterpart; it cannot accidentally become a second layout.

**Refs are identifiers, not descriptions.** Every section, entry and bullet carries an id
— `exp`, `exp_i01`, `exp_003` — so an LLM can point at one line of your CV. They are
assigned server-side and preserved through every round trip, so `exp_003` still means the
same bullet next month and a model's reply from last week can still be applied.

### Data model

```
Cv ──< Section ──< CvItem ──< Bullet
```

Order is array order — there are no sort fields. Every level has `Included`.
`Section.Kind` decides layout:

| Kind       | Renders as                                            | Used for            |
| ---------- | ----------------------------------------------------- | ------------------- |
| `Timeline` | Title + organisation on the left, dates + location on the right, then bullets | Experience, Education, Projects |
| `Grouped`  | `Item.Title` as a label, bullets joined onto one line  | Skills, Languages   |
| `Bullets`  | Bullets only, no entry header                          | Highlights          |
| `FreeForm` | Paragraphs of prose under the section title, unmarked  | Personal Life, About |

`Bullets` sections can also be set two-column, which flows every bullet in the section
across two columns — left column first — to save vertical space.

### Where your CV actually lives

There are two copies, and only one of them is a save:

- **The `.cvjson` file** you get from **Save to file**. This is the real one. Keep it
  somewhere you back up. Losing it loses the CV.
- **A draft in `localStorage`**, autosaved ~400 ms after you stop typing. It exists so a
  refresh or an accidentally closed tab does not cost you an hour. It is per-browser,
  never sent to the server, and disappears if you clear site data or switch machine.

**New** and **Open file** both warn before discarding unsaved edits.

### Save files

The file is indented JSON, safe to hand-edit:

```json
{
  "format": "cvbuilder.cv",
  "version": 1,
  "exportedAt": "2026-09-03T11:39:23Z",
  "cv": {
    "name": "Master CV",
    "style": "Mono",
    "sections": [
      {
        "id": "skill",
        "title": "Skills",
        "kind": "Grouped",
        "included": true,
        "twoColumns": false,
        "items": [
          {
            "id": "skill_i01",
            "title": "Languages",
            "included": true,
            "bullets": [{ "id": "skill_001", "text": "C#", "included": true }]
          }
        ]
      }
    ]
  }
}
```

`format` and `version` are checked before anything is used, so opening the wrong file
gets a readable message. Files from older versions stay importable, and a hand-written
file with ids missing gets them assigned. Adding an optional field does not bump
`version` — an older build losing one boolean is a better failure than refusing the file
outright.

### AI tailoring

Paste a job listing and DeepSeek is asked which parts of the CV belong in that
application. It replies with ids only:

```json
{ "include": ["exp_001", "exp_007"], "exclude": ["exp_002", "exp_003"] }
```

The reply is never applied straight away. `CvTailoring` resolves it against the CV and
returns what *would* change — described by the text of each line, not by id — and you
confirm. The same code then performs the write. Details worth knowing:

- **Ids in neither list keep their current setting.**
- **Including a bullet pulls its ancestors in.** A kept line inside an excluded entry
  would still not print, so the entry and section come along, marked as such in the
  preview. An ancestor the model excluded on purpose wins over that.
- **An id in both lists is treated as excluded** and flagged.
- **Ids that are not in the CV are ignored**, and reported as a count.
- The prompt is the single constant in `Ai/TailoringPrompt.cs`. Nothing else depends on
  its wording, only on the shape of the reply.
- The API key stays server-side. The browser talks only to our own API.

### API

Every route is stateless. There is no `GET /cvs`, no ids in URLs, and nothing to delete.

| Method | Route | Purpose |
| ------ | ----- | ------- |
| `GET`  | `/health` | Liveness |
| `GET`  | `/api/cv/template` | The starter CV behind the New button |
| `POST` | `/api/cv/pdf` | Render the posted CV's included slice to PDF |
| `POST` | `/api/cv/export` | Validate, assign missing refs, return a `.cvjson` download |
| `POST` | `/api/cv/import` | Validate a file's contents and return it normalised |
| `GET`  | `/api/ai/status` | Whether a DeepSeek key is configured, and which model |
| `POST` | `/api/cv/tailor` | Ask the model what to include; returns the proposed changes |
| `POST` | `/api/cv/tailor/apply` | Apply a confirmed recommendation, return the amended CV |

`/export` is where refs get assigned, so a row you just added carries a blank id until
the CV next passes through the server. That is invisible in the UI, where ids are never
shown.

## Running it

Two terminals.

```bash
# 1. Backend  (http://localhost:5199)
cd backend/CvBuilder.Api
dotnet run

# 2. Frontend (http://localhost:5173)
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Nothing else to install — no database to start.

### Configuration

| Setting | Where | Default |
| ------- | ----- | ------- |
| DeepSeek API key | `dotnet user-secrets set "DeepSeek:ApiKey" …`, or `DeepSeek__ApiKey` env var | *(none — tailoring is disabled without it)* |
| DeepSeek model | `DeepSeek__Model` env var, or `appsettings.json` | `deepseek-chat` |
| API URL for the dev proxy | `VITE_API_TARGET` env var | `http://localhost:5199` |

The key is never committed and never reaches the browser. The frontend only ever calls
`/api` on its own origin; Vite proxies that in development, and CORS is enabled for
`localhost:5173` **in development only**.

## When you come to host it

- **One stateless container.** `dotnet publish`, run it, done. No database, no volumes,
  no backups, no migrations. It scales to zero happily, which is what makes it cheap.
- **Serving the SPA from the API.** `npm run build` emits `frontend/dist`. Copy it to
  `wwwroot` and add `app.UseDefaultFiles(); app.UseStaticFiles(); app.MapFallbackToFile("index.html");`
  to `Program.cs`. Same origin, so the dev-only CORS policy stays unused.
- **Set `DeepSeek__ApiKey`** in the host's environment. That is the only required setting.
- **It is single-user by design and holds no data**, so exposing it costs you nothing in
  privacy terms — but anyone who finds the URL can spend your DeepSeek credit. Put it
  behind basic auth or an allowlist, or accept the risk knowingly.
- **QuestPDF** is used under its Community licence (free for individuals and small
  companies). It renders without a browser, so the container stays small.

## Layout

```
backend/CvBuilder.Api/
  Domain/Entities.cs        Cv, Section, CvItem, Bullet — in-memory only
  Domain/CvRefs.cs          Stable exp_003-style handles
  Data/Templates.cs         The starter CV
  Api/CvSaveFile.cs         Save-file format, validation, mapping
  Api/CvEndpoints.cs        Template, PDF, export, import
  Api/TailorEndpoints.cs    Tailoring routes
  Ai/TailoringPrompt.cs     THE PROMPT — replace with your own
  Ai/DeepSeekClient.cs      Chat call, key handling, error messages
  Ai/CvTailoring.cs         Reply -> planned toggle changes -> write
  Pdf/CvTheme.cs            Typography per style (Base, Mono)
  Pdf/CvPdfGenerator.cs     Inclusion filtering + A4 layout
frontend/src/
  types.ts                  The CV tree, plus local uids
  cvFile.ts                 Editor tree <-> save file, and new blank rows
  api.ts                    Typed fetch wrappers
  useCvEditor.ts            Local CV state + localStorage autosave
  components/Editor.tsx     Left pane
  components/Preview.tsx    Right pane — mirrors CvPdfGenerator
  components/TailorDialog.tsx  Job listing in, proposed changes out
```
