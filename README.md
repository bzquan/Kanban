# Kanban

A simple Kanban board application written in C# targeting .NET 10. The repository contains two front ends over a shared domain and the same MongoDB database:

- **WPF desktop app** (`net10.0-windows`) — Views, MVVM-style ViewModels, and adapter components for persistence and utilities.
- **Web app** — an ASP.NET Core REST API (`Kanban.Api`) plus a React SPA (`frontend/app`, Vite + MUI + TanStack Query). The API also serves the built SPA from `Kanban.Api/wwwroot`, so a single `dotnet` process can host the whole web app.

## Key features
- Multi-column Kanban board with draggable cards
- Card details, tags, priorities, and story points
- Zoom-to-fit board view and refresh controls
- Import/export and backup utilities (adapter layer)
- Web: board list, board page (WPF-parity card fields, hover tooltips), process-step editor, metrics charts, settings and release-note pages; localized in EN/JA/ZH

## Requirements
- Windows 10/11
- .NET 10 SDK
- Visual Studio 2022/2026 or dotnet CLI
- MongoDB 8.x
- Node.js 22+ (web front end only; developed with Node 24)

## Build and run

1. Clone the repository:

   git clone [Kanban URL](https://github.com/bzquan/Kanban.git)

### WPF desktop app

Build and run using dotnet CLI from repository root:

   dotnet build Kanban/Kanban.csproj
   dotnet run --project Kanban/Kanban.csproj

Or open the solution in Visual Studio and run the Kanban project (set as startup project).

The database host/name come from `Kanban/App.config` (`DB_HOST`, `DB_NAME` appSettings).

### Web app (single process: API + SPA)

The SPA is built into `Kanban.Api/wwwroot` (git-ignored, so build it before first run) and
served by the API together with `/api` — one `dotnet` process hosts everything.

From the repository root:

   cd frontend/app
   npm install
   npm run build
   cd ../..
   dotnet run --project Kanban.Api --urls http://192.168.21.233:80

Then open **http://192.168.21.233:80** (this machine's LAN IP — use your own
machine's IP here; `localhost` is not bound, so LAN clients and this machine
both connect via the IP).

The API reads `Kanban.Api/appsettings.json` (`Kanban:DB_HOST` — currently
`192.168.21.150:27017` — and `Kanban:DB_NAME`, currently `kanban`).
`index.html` is served with `Cache-Control: no-store` (hashed assets are cached),
so a fresh build is picked up on reload without stale-SPA issues.

### Web app (front-end development mode)

Run the API on its default port and Vite bound to the LAN IP — Vite proxies
`/api` to `http://localhost:5000` (same machine), so SPA edits hot-reload
without rebuilding `wwwroot`:

   dotnet run --project Kanban.Api
   cd frontend/app
   npm run dev -- --host 192.168.21.233   # open http://192.168.21.233:5173

### Tests

- .NET: `dotnet test` (TestKanban + Kanban.Api.Tests)
- Front end: `cd frontend/app && npm test -- --run` (Vitest + Testing Library)
- Front-end lint: `npm run lint` (oxlint)

## Project layout (high level)
- Kanban/ — WPF application project (Views, ViewModels; `src/` layout)
- Kanban.Domain/ — shared domain + MongoDB adapters (Model, Repository, Infrastructure), used by both front ends
- Kanban.Api/ — ASP.NET Core REST API; also serves the built SPA from `wwwroot`
- Kanban.Api.Tests/ — API integration tests (WebApplicationFactory)
- frontend/app/ — React SPA (Vite + TypeScript + MUI + TanStack Query + i18next); locales are generated from the WPF resx trio via `npm run i18n:export`
- frontend/openapi/ — OpenAPI schema + generated types for the typed API client
- TestKanban/ — WPF/domain test project

## Contributing
Contributions are welcome. Please open an issue to discuss large changes, and submit pull requests with focused commits and tests when appropriate.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Troubleshooting
- If images do not appear at runtime, confirm that the images are present in `Kanban/images` and included as project Resources in Kanban.csproj.
- If build reports duplicate assembly attributes, check SDK-generated assembly info and project settings (GenerateAssemblyInfo).
