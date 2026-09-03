# CV Builder

A small self-hosted web app for building a CV and exporting it to PDF. You keep one
**master CV** with everything you have ever done, then tick entries in or out to produce
a PDF tailored to a specific application. Nothing is deleted when you exclude it.

- **Frontend** — React 19 + TypeScript + Vite
- **Backend** — ASP.NET Core minimal API (.NET 10)
- **Database** — PostgreSQL via EF Core
- **PDF** — QuestPDF, rendered server-side

## Architecture

```
┌──────────────────────┐        JSON over /api        ┌───────────────────────────┐
│  React SPA (Vite)    │ ───────────────────────────▶ │  ASP.NET Core minimal API │
│                      │                              │                           │
│  Editor  │  Preview  │ ◀─── application/pdf ─────── │  EF Core  →  QuestPDF     │
└──────────────────────┘                              └─────────────┬─────────────┘
                                                                    │
                                                            ┌───────▼────────┐
                                                            │   PostgreSQL   │
                                                            └────────────────┘
```

Four deliberate choices, each to keep the project small:

**The PDF is rendered on the server, from the database.** The browser preview is a
lookalike, not the source of truth — `Preview.tsx` and `CvPdfGenerator.cs` apply the same
inclusion and ordering rules. That means the export never depends on browser state, and
the app can grow a "mail me my CV" or scheduled export later without moving any logic.
The cost is that the two renderers must be kept in step by hand; they are ~100 lines each
and sit next to each other in the repo for exactly that reason.

**Inclusion is a flag, not a delete.** `Section`, `CvItem` and `Bullet` each carry
`Included`. The PDF generator filters on it, and drops any section or entry that would
print empty once filtered. This is the whole tailoring feature — no separate "export
document" table, no copying.

**Styles are typography, not layout.** `CvTheme` is a record of type sizes, weights,
tracking, rule weights, colour and spacing — nothing in it can move content around, which
is what keeps `Base` and `Mono` the same document. `CvPdfGenerator` holds the single
arrangement and reads every visual value from the theme. Adding a third style means adding
one more `CvTheme` and its CSS counterpart; it cannot accidentally become a second layout.

**There is no client-side state library.** `useCvEditor` holds the CV tree, applies every
edit locally first so the preview is instant, and schedules the matching API call keyed
per entity so typing collapses into one request per field. `flush()` forces pending writes
out before the PDF is requested — the export reads the database, so the database has to be
current.

### Data model

```
Cv ──< Section ──< CvItem ──< Bullet
```

Every level has `SortOrder` and `Included`. `Section.Kind` decides layout:

| Kind       | Renders as                                            | Used for            |
| ---------- | ----------------------------------------------------- | ------------------- |
| `Timeline` | Title + organisation on the left, dates + location on the right, then bullets | Experience, Education, Projects |
| `Grouped`  | `Item.Title` as a label, bullets joined onto one line  | Skills, Languages   |
| `Bullets`  | Bullets only, no entry header                          | Highlights          |
| `FreeForm` | Paragraphs of prose under the section title, unmarked  | Personal Life, About |

Cascade deletes run in the database, so removing a section takes its items and bullets
with it.

### Styles

`Cv.Style` picks the typography, saved per CV and toggled from the topbar:

| Style  | Treatment |
| ------ | --------- |
| `Base` | Soft greys, semibold headings, hairline rules |
| `Mono` | Tracked capitals for headings and entry titles, heavy grey rules, black body text |

Both print the same content in the same arrangement. The values live in
`Pdf/CvTheme.cs`, mirrored for the on-screen preview by the `.paper-mono` block in
`src/index.css`.

### API

All routes are under `/api`. Requests replace an entity's own scalar fields; children are
managed through their own routes.

| Method | Route | Purpose |
| ------ | ----- | ------- |
| `GET`    | `/cvs` | List CVs (id, name, last edited) |
| `POST`   | `/cvs` | Create a CV from the starter template |
| `GET`    | `/cvs/{id}` | Full CV tree |
| `PUT`    | `/cvs/{id}` | Update name, contact details, summary, style |
| `DELETE` | `/cvs/{id}` | Delete a CV |
| `GET`    | `/cvs/{id}/pdf` | Render the included slice to PDF |
| `POST`   | `/cvs/{id}/sections` · `PUT` `/sections/{id}` · `DELETE` `/sections/{id}` | Sections |
| `PUT`    | `/cvs/{id}/sections/order` | Reorder sections |
| `POST`   | `/sections/{id}/items` · `PUT` `/items/{id}` · `DELETE` `/items/{id}` | Entries |
| `PUT`    | `/sections/{id}/items/order` | Reorder entries |
| `POST`   | `/items/{id}/bullets` · `PUT` `/bullets/{id}` · `DELETE` `/bullets/{id}` | Bullets |
| `PUT`    | `/items/{id}/bullets/order` | Reorder bullets |

Reorder requests take `{ "ids": [...] }`. Ids the client did not mention keep their
relative order and are appended after the listed ones, so a stale tab cannot scramble the
list.

## Running it

Three terminals, or run the first two once and leave them.

```bash
# 1. Database
docker compose up -d

# 2. Backend  (http://localhost:5199)
cd backend/CvBuilder.Api
dotnet run

# 3. Frontend (http://localhost:5173)
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Migrations run at startup, and a fresh database gets one
starter CV so the editor is never a blank page.

### Configuration

| Setting | Where | Default |
| ------- | ----- | ------- |
| Connection string | `ConnectionStrings__Default` env var, or `appsettings.json` | `Host=localhost;Port=5432;Database=cvbuilder;Username=cvbuilder;Password=cvbuilder` |
| API URL for the dev proxy | `VITE_API_TARGET` env var | `http://localhost:5199` |

The frontend only ever calls `/api` on its own origin; Vite proxies that in development.
CORS is enabled for `localhost:5173` **in development only**.

### Database changes

```bash
cd backend/CvBuilder.Api
dotnet ef migrations add <Name>
```

The app applies pending migrations on startup, so there is no separate deploy step.

## When you come to host it

The parts that touch hosting were left as the simple thing, but not the wrong thing:

- **Config is already environment-driven.** Set `ConnectionStrings__Default` and nothing
  else needs to change.
- **Serving the SPA from the API.** `npm run build` emits `frontend/dist`. Copy it to
  `wwwroot` and add `app.UseDefaultFiles(); app.UseStaticFiles(); app.MapFallbackToFile("index.html");`
  to `Program.cs`. Same origin, so the dev-only CORS policy stays unused and `/api` calls
  work untouched.
- **Adding accounts.** The app is single-user by design — there is no login and every CV is
  visible to whoever can reach the app, so do not expose it to the internet as it stands.
  Adding auth means one nullable `OwnerId` on `Cv`, a filter in the `/cvs` routes, and an
  ownership check in `LoadFull`. Everything below `Cv` is already reachable only through it.
- **Migrations on startup** are fine for one instance. If you ever run more than one, move
  `db.Database.Migrate()` out of `Program.cs` into a deploy step.
- **QuestPDF** is used under its Community licence (free for individuals and small
  companies). It renders without a browser, so the container stays small and there is no
  headless-Chrome dependency to keep alive.

## Layout

```
backend/CvBuilder.Api/
  Domain/Entities.cs        Cv, Section, CvItem, Bullet
  Data/CvDbContext.cs       Mapping, cascade deletes, indexes
  Data/Templates.cs         The starter CV
  Api/Contracts.cs          DTOs and mapping
  Api/CvEndpoints.cs        All routes
  Pdf/CvTheme.cs            Typography per style (Base, Mono)
  Pdf/CvPdfGenerator.cs     Inclusion filtering + A4 layout
frontend/src/
  api.ts                    Typed fetch wrappers
  useCvEditor.ts            CV state, optimistic edits, debounced saves, flush()
  components/Editor.tsx     Left pane
  components/Preview.tsx    Right pane — mirrors CvPdfGenerator
```
