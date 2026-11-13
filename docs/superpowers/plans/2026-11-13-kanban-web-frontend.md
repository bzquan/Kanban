# Kanban Web Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the React+TypeScript SPA that replaces the WPF Kanban client (spec §4.5), packaged into `Kanban.Api/wwwroot` so one ASP.NET Core process serves SPA + API.

**Architecture:** Vite React-TS app in `frontend/app/`. All server state via TanStack Query over a thin typed client (`openapi-fetch`) generated against `frontend/openapi/schema.d.ts`. The backend API on this branch is complete — the frontend consumes it exactly as-is (13 endpoints, no new ones). Drag&drop with dnd-kit maps to `PATCH /api/cards/{id}/move`; metrics are ECharts views over the 5 metrics DTOs; i18n loads `.resx`-derived JSON with i18next.

**Tech Stack:** React 19, TypeScript, Vite 7, MUI v7 (builder's pick from spec's MUI/shadcn choice), dnd-kit, Apache ECharts, TanStack Query v5, react-router v7, i18next + react-i18next, openapi-fetch, Vitest + React Testing Library (jsdom).

**Spec:** `docs/superpowers/specs/2026-11-13-kanban-web-migration-design.md` (approved 2026-11-13) — read it before starting; this plan argues from it.

## Global Constraints

- Server-side stays C#/.NET 10; **the frontend adds no new backend endpoints** (Task 12's static-file wiring is not an endpoint).
- Multilingual EN/JP/ZH required for all UI strings (spec anti-goal: "多言語(EN/JP/ZH)が欠落する").
- No auth, no CORS, no HTTPS (LAN-only, spec §3).
- Types come **only** from `frontend/openapi/schema.d.ts` (regenerate via `npm run generate` in `frontend/openapi` against a running server; script URL corrected to `http://localhost:5000/openapi/v1.json` in Task 2). `number | string` unions from int64/Bson serialization stay as-is in the types; convert with `Number(...)` at the display edge.
- User settings live in `localStorage` under `kanban.*` keys only (spec §4.6): `kanban.lang`, `kanban.zoom`, `kanban.isZoomed`, `kanban.lastMetricChartType`, `kanban.lastBoardId`.
- WPF parity minus backup: reference WPF sources listed per task (spec §4.2); WPF is feature-frozen during migration.
- Frontend commands run from `frontend/app/`; .NET commands from the repo root. Node ≥ 22 (machine has v24).
- Every task commits with the repo's commit convention (`feat:`/`fix:` prefixes, Japanese or English subject as in history) plus the Co-Authored-By attribution line from session guidance.

## Review Focus

The spec implies these behaviors but no single task's happy path exercises them; each line has a pinning test in the owning task:

1. **Failed write leaves no ghost state** — a rejected move/patch/delete must invalidate the board queries so the screen re-reads from server, never keeps an optimistic ghost card (Task 7/8 tests).
2. **Malformed ObjectId in URL** (`/boards/xyz`) — API returns 400; UI shows a recoverable error state with a back link, app keeps working (Task 5 test).
3. **Drop target on an empty sub-column** — a column with zero cards must still accept drops at index 0 (Task 7 test).
4. **Unknown i18n key / unsupported browser language** — falls back to `en`; raw key strings must never render (Task 3 test).
5. **Zero/empty metrics series** (fresh board: all counts 0, empty weeks) — every chart renders empty axes without a JS error (Task 10 test).

## File Structure

```
frontend/
  openapi/                    # exists; typegen (generate script URL corrected in Task 2)
    schema.d.ts
  app/                        # all new work
    package.json  vite.config.ts  vitest.config.ts  tsconfig.json  index.html
    scripts/export-resx.mjs   # resx -> locales JSON (Task 3)
    src/
      main.tsx  App.tsx       # router + QueryClientProvider + ThemeProvider + i18n init
      api/client.ts           # openapi-fetch instance + typed helper wrappers (Task 2)
      api/types.ts            # narrow aliases of schema components (Task 2)
      i18n/index.ts  locales/{en,ja,zh}.json   # (Task 3)
      components/CardView.tsx               # (Task 6)
      components/CardEditDialog.tsx         # (Task 8)
      components/ProcessStepEditor.tsx      # (Task 9)
      pages/BoardListPage.tsx               # (Task 4)
      pages/BoardPage.tsx                   # (Tasks 5,7) columns + toolbar + DndContext
      pages/MetricsPanel.tsx                # (Task 10) kanban/chart toggle lives in BoardPage
      pages/SettingsPage.tsx  pages/AboutPage.tsx   # (Task 11)
      charts/options.ts       # pure DTO->EChartsOption builders ×5 (Task 10)
      cardType.ts             # trilingual labels + colors copied from CardTypePresentation.cs (Task 6)
      test/fetchStub.ts       # global fetch stub for tests (Task 2)
      test/renderWithProviders.tsx          # (Task 1)
```

---

### Task 1: Scaffold `frontend/app` (Vite + React + Vitest + MUI + Router + Query)

**Files:**
- Create: `frontend/app/` via `npm create vite@latest` (react-ts template), then `vite.config.ts`, `vitest.config.ts`, `src/main.tsx`, `src/App.tsx`, `src/test/renderWithProviders.tsx`
- Create: `frontend/app/package.json` (via npm installs below)

**Interfaces:**
- Consumes: nothing.
- Produces: `<App/>` shell rendering `<AppBar>` with app title key `app.title` and `<Outlet/>` under `BrowserRouter` routes `/` → `BoardListPage`, `/boards/:boardId` → `BoardPage` (placeholder components now, real pages in Tasks 4–5); `renderWithProviders(ui)` = RTL render wrapped in `QueryClientProvider` (retry false) + MUI `ThemeProvider` + `MemoryRouter` (accepts `{ route }` option). Dev server proxies `/api` → `http://localhost:5000`.

- [ ] **Step 1: Scaffold and install**

```powershell
cd frontend; npm create vite@latest app -- --template react-ts; cd app
npm i react-router @tanstack/react-query @mui/material @emotion/react @emotion/styled @mui/icons-material echarts @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities i18next react-i18next openapi-fetch
npm i -D vitest jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event @vitejs/plugin-react
```

If `npm create vite` prompts interactively, accept defaults (React, TypeScript). One MUI install only: `@mui/material @emotion/react @emotion/styled @mui/icons-material`.

- [ ] **Step 2: Config** — `vite.config.ts`: `server.proxy['/api'] = 'http://localhost:5000'`; `build.outDir = '../../Kanban.Api/wwwroot'` (two levels — Vite resolves outDir against the project root `frontend/app/`; erratum R5) with `emptyOutDir: true` (harmless until Task 12; keep a comment pointing to Task 12). `vitest.config.ts`: `environment: 'jsdom'`, `globals: true`, setup file importing `@testing-library/jest-dom`.
- [ ] **Step 3: Write the failing test** `src/App.test.tsx`:

```tsx
it('renders app shell with title and board-list route', async () => {
  renderWithProviders(<App />, { route: '/' });
  expect(await screen.findByTestId('app-bar')).toBeInTheDocument();
});
```

- [ ] **Step 4: Run to verify it fails** — `npm run test -- --run src/App.test.tsx` → FAIL (cannot resolve `./App`).
- [ ] **Step 5: Implement** `src/App.tsx` (AppBar `data-testid="app-bar"` with `t('app.title')`, Routes, QueryClient created once with `retry: false`, ThemeProvider with default MUI theme), `src/main.tsx`, placeholder pages `pages/BoardListPage.tsx`/`pages/BoardPage.tsx` (render `data-testid` markers), `src/test/renderWithProviders.tsx`. Title renders the raw key `app.title` for now — i18n arrives in Task 3; AppBar must go through `useTranslation`.
- [ ] **Step 6: Run tests** — `npm run test -- --run` → PASS.
- [ ] **Step 7: Smoke the dev server** — `npm run dev` in background; `curl -s http://localhost:5173/api/health` → `{"status":"ok"}` (requires Kanban.Api running: `dotnet run --project Kanban.Api`). Kill it after.
- [ ] **Step 8: Commit** — `git add frontend/app && git commit -m "feat: SPA足場(Vite/React/Vitest/MUI/Router/TanStack Query)"`

### Task 2: Typed API client + fetch stub

**Files:**
- Create: `src/api/client.ts`, `src/api/types.ts`, `src/test/fetchStub.ts`
- Modify: `frontend/openapi/package.json` (`generate` URL → `http://localhost:5000/openapi/v1.json`), `frontend/openapi/schema.d.ts` (regenerate)

**Interfaces:**
- Consumes: schema types `components['schemas'][...]`.
- Produces (used by every later task, exact names):
  - `api` — `createClient<paths>({ baseUrl: '' })` (erratum R6: schema paths already begin with `/api/`, so a `'/api'` baseUrl double-prefixes; paths from `../../../openapi/schema` per R1; late-bound `fetch: (r) => globalThis.fetch(r)` so tests can swap fetch after module load).
  - `boardsApi = { list(): Promise<BoardDto[]>, create(uiLanguage: string): Promise<BoardDto>, remove(id: string): Promise<void> }`
  - `cardsApi = { list(boardId: string, q: CardsQuery): Promise<CardDto[]>, create(req: CardCreateRequest): Promise<CardDto>, patch(id: string, p: CardPatch): Promise<CardDto>, move(id: string, req: MoveCardRequest): Promise<void>, remove(id: string): Promise<void> }`
  - `metricsApi = { cfd|cycleTime|throughput|bugsBlocked|velocity(boardId: string): Promise<...Dto> }`
  - `CardsQuery = { processStepSeqNo?: number; isWip?: boolean; filter?: string; onBoard?: 'front' | 'back' | 'all' }`
  - All wrappers throw `ApiError { status: number }` on non-2xx.
  - `fetchStub.ts`: `installFetch(handlers: Record<string, (req: Request) => Response | object>)` keyed `"METHOD /path"`, records `calls: {method,url,body}[]`, plus `restoreFetch()`.
- `intVal(v: number | string | undefined): number` helper exported from `client.ts` (handles the `number | string` int64 unions; `NaN`/undefined → 0).

- [ ] **Step 1: Correct typegen and regenerate** — start `dotnet run --project Kanban.Api`, then `cd frontend/openapi && npm i && npm run generate`; expect schema.d.ts regenerated unchanged except formatting.
- [ ] **Step 2: Write failing tests** `src/api/client.test.ts` using `installFetch`:

```ts
it('boardsApi.list GETs /api/boards and unwraps array', …);      // asserts recorded call method/url, returns parsed BoardDto[]
it('cardsApi.list encodes CardsQuery into query string', …);      // ?processStepSeqNo=2&isWip=false&filter=abc&onBoard=back
it('cardsApi.move PATCHes /api/cards/{id}/move with body', …);    // body {processStepSeqNo,isWip,index}; resolves on 204
it('boardsApi.create sends {uiLanguage} and throws ApiError on 400', …);
it('intVal parses "7", 7 and undefined to 7, 7, 0', …);
```

- [ ] **Step 3: Run → fail.**
- [ ] **Step 4: Implement** `types.ts` (re-export aliases `BoardDto = components['schemas']['BoardDto']` etc. for the 7 DTOs + 3 requests + `WorkStateDto`), `client.ts`, `fetchStub.ts`.
- [ ] **Step 5: Run → pass.** **Step 6: Commit** `"feat: SPA typed API client(openapi-fetch)とfetch stub"`.

### Task 3: i18n — resx export + language detection

**Files:**
- Create: `scripts/export-resx.mjs`, `src/i18n/index.ts`, `src/locales/{en,ja,zh}.json`, test `src/i18n/i18n.test.ts`

**Interfaces:**
- Consumes: `Kanban/src/View/Properties/Resources.resx` (+`.ja-JP`, `.zh-CN`; 145 real `<data>` entries each — raw grep counts 149 because 4 sample entries sit inside the ResX schema header comment).
- Produces: `initI18n(): Promise<void>` (called in `main.tsx` before render), `setLanguage(lang: 'en'|'ja'|'zh')`, `detectLanguage(): 'en'|'ja'|'zh'`. JSON shape: flat `{ "key": "text" }` under a single `ui` namespace. `kanban.lang` localStorage key stores manual choice.

- [ ] **Step 1: Export script** `scripts/export-resx.mjs`: parse the three `.resx` with regex on `<data name="…">…<value>…</value>` (entities decoded; ignore comments), write `src/locales/{en,ja,zh}.json`. Add npm script `"i18n:export": "node scripts/export-resx.mjs"`.
- [ ] **Step 2: Run it** — `npm run i18n:export`; expect 3 files × 149 keys, `en` non-empty for every key.
- [ ] **Step 3: Write failing tests** `src/i18n/i18n.test.ts`:

```ts
it('detects ja from navigator.language=ja-JP, zh from zh-CN, en otherwise', …);   // matchMedia/navigator stubbed
it('localStorage kanban.lang overrides detection', …);
it('missing key falls back to en value; unknown language falls back to en', …);   // t('some.missing.key') returns en text, never the raw key
```

- [ ] **Step 4: Run → fail. Step 5: Implement** `src/i18n/index.ts` (i18next init: `fallbackLng: 'en'`, resources loaded synchronously from the 3 JSONs, `nonExplicitSupportedLngs: true`, `returnEmptyString: false`). [**Step 6:** run → pass.] **Step 7: Wire** into `main.tsx`; AppBar title now shows localized "Kanban"/"カンバン"/中文 from the existing resx title key (find the actual key used for the app title in the exported JSON; if none exists, add `app.title` to all 3 JSONs by hand with the three translations — WPF Window title lives in XAML, not resx).
- [ ] **Step 8: Update App.test.tsx** to assert the AppBar shows the translated title (i18n now initialized in tests via `initI18n()` in `renderWithProviders`).
- [ ] **Step 9: Commit** `"feat: i18next(resx→JSON抽出スクリプト・EN/JP/ZH・言語検出)"`.

### Task 4: Board list page

**Files:**
- Modify: `src/pages/BoardListPage.tsx`; Test: `src/pages/BoardListPage.test.tsx`

**Interfaces:**
- Consumes: `boardsApi`, `renderWithProviders`, `t('…')` keys.
- Produces: route `/` fully working: board rows (title, description, developers, #steps), row click → `/boards/{id}` + `localStorage['kanban.lastBoardId']`; "New board" button → `boardsApi.create(t('board.new.defaultTitle'))` then navigates to it; "Delete" per row with MUI confirm dialog → `boardsApi.remove(id)` + invalidate `['boards']`.

- [ ] **Step 1: Write failing tests** (fetch-stubbed): lists titles from stubbed `GET /api/boards`; create click POSTs `{uiLanguage:'ja'}` when language is ja and navigates to `/boards/{newId}`; delete click confirms then records `DELETE /api/boards/{id}` and refetches the list; empty state shows localized "no boards" text.
- [ ] **Step 2: Run → fail. Step 3: Implement** (TanStack Query `useQuery({queryKey:['boards'],queryFn:boardsApi.list})`, MUI List/ListItem, Dialog confirm, `useNavigate`). [ ] **Step 4: Run → pass. Step 5: Commit** `"feat: ボード一覧ページ(作成/削除/遷移)"`.

### Task 5: Board page — columns, sub-columns, toolbar, queries

**Files:**
- Modify: `src/pages/BoardPage.tsx`; Create: `src/pages/boardQueries.ts`; Test: `src/pages/BoardPage.test.tsx`

**Interfaces:**
- Consumes: `boardsApi.list` (board meta), `cardsApi.list(boardId, q)`, `ProcessStepDto`, `CardDto`, `intVal`.
- Produces (used by Tasks 6–7):
  - Query keys: `['board', boardId]` for meta, `['cards', boardId, processStepSeqNo, isWip, onBoard, filter]`.
  - `useBoard(boardId)`, `useCards(boardId, step: number, isWip: boolean, onBoard, filter)` in `boardQueries.ts`.
  - Column layout: one `<ColumnView>` per `processSteps[i]` (name shown verbatim — it embeds "(Limit:N)"), each with two sub-columns headed `wipTitle` / `doneTitle` listing cards sorted by `intVal(seqNo)` ascending (server already sorted; do not re-sort except stable ascending seqNo). Back/front toggle → `onBoard` state (default `front`). Toolbar: filter text (debounced 300ms → `filter` param), refresh button (invalidate by prefix `['cards']`), zoom out/in/reset buttons, add-card buttons are Task 8 but reserve `data-testid="add-card-{seqNo}-{wip|done}"`. `localStorage['kanban.lastBoardId']` set on mount.
  - **Zoom** (spec §4.5/§4.6, WPF zoom-to-fit parity-lite): board canvas wrapped in a div with `transform: scale(z)`; `z` state persisted to `localStorage['kanban.zoom']` (0.5–1.5, step 0.1), plus `kanban.isZoomed` boolean ("fit-to-window" toggle button measuring container width vs content width). Test: clicking zoom-out twice renders scale(0.8) with `kanban.zoom='0.8'`; reload restores it.
  - Error state: on `ApiError` with status 400/404 render `data-testid="board-error"` with back-to-list link (Review Focus #2).
  - Reference for layout semantics: `Kanban/src/View/xaml/BoardPage.xaml`, `ProcessStepView.cs`.

- [ ] **Step 1: Write failing tests**: stubs return 1 board (2 steps) + per-query card lists; assert 2 columns × 2 sub-columns with localized/verbatim headers, cards render title+seqNo order; toggle to Back re-queries with `onBoard=back`; malformed boardId `'xyz'` renders `board-error`, no crash (Review Focus #2); empty board (steps=1 backlog-only, no cards) renders empty columns without error.
- [ ] **Step 2: Run → fail. Step 3: Implement** `boardQueries.ts` + `BoardPage.tsx` (MUI Toolbar/TextField/Button, plain flex columns; card rendering behind `CardView` placeholder used by Task 6 — render a div with `data-testid="card"` + title now).
- [ ] **Step 4: Run → pass. Step 5: Commit** `"feat: ボードページ(列/サブ列/ツールバー/取得クエリ)"`.

### Task 6: CardView component (parity with WPF card)

**Files:**
- Create: `src/components/CardView.tsx`, `src/cardType.ts`; Test: `src/components/CardView.test.tsx`

**Interfaces:**
- Consumes: `CardDto`, `intVal`.
- Produces: `<CardView card={CardDto} onOpen={() => void}/>` used by BoardPage (Task 5 swap-in) and drop zones (Task 7). Renders: title; storyPoints badge when `>0`; `tag`; `executors`; **block indicator** when `isBlocked`; **merge indicator** (two small chips when `isMergedIntoMaster` / `isMergedIntoMajorBranch`); scheduled-release progress bar when `isScheduledToRelease` (progress = elapsed workdays `createDate→stateChangedDate` vs `stateChangedDate→releaseDate` — copy exact formula from `Kanban/src/ViewModel/Card.cs:~150-260`); background tint by `cardType` from `CARD_TYPE_BACKGROUND: Record<string, string>` (WPF `CardTypePresentation.cs` table: Feature `#008000`, Improvement `#00FF00`, BugFix `#FF0000`, DefectInvestigation `#FFB6C1`, TechnicalDebt `#FFFF00`, Cancelled `#A9A9A9`, Evaluation `#FFEFD5`, TestDesign `#FFA500`, Task — read `CardTypePresentation.cs` remainder for the last color) with readable text (dark text on light colors via contrast check helper `textColorFor(hex): string` in `cardType.ts`); localized type label via `CARD_TYPE_LABELS: Record<string, {en: string; ja: string; zh: string}>` copied from `CardType.cs` `[Description]` values verbatim.

- [ ] **Step 1: Write failing tests**: BugFix card gets red background; blocked card shows block chip; `storyPoints: 0` hides badge; type label localizes (ja stub); progress bar hidden unless `isScheduledToRelease`; `textColorFor('#008000')='#fff'`, `textColorFor('#FFFF00')='#000'`.
- [ ] **Step 2: Run → fail. Step 3: Implement** (read `CardType.cs` + `CardTypePresentation.cs` + `ViewModel/Card.cs` first; copy numbers exactly). [ ] **Step 4: Run → pass. Step 5: Swap into BoardPage** (replace placeholder divs) + fix BoardPage tests. [ ] **Step 6: Commit** `"feat: カード表示(SP/進捗バー/マージ/ブロック/種別色)"`.

### Task 7: Drag & drop move + reorder (dnd-kit)

**Files:**
- Modify: `src/pages/BoardPage.tsx`; Create: `src/pages/dnd.ts`; Test: `src/pages/dnd.test.tsx`

**Interfaces:**
- Consumes: `cardsApi.move`, per-sub-column queries from Task 5, `CardView` (renders inside `SortableContext`).
- Produces: `moveRequestFor(drag: {step, isWip, card}, to: {step, isWip, overCard|null}): MoveCardRequest` — pure function: target `{processStepSeqNo, isWip, index}` where `index` = insertion position among the destination sub-column's cards (`null` overCard → append = list length); `DndContext` + `SortableContext` wiring with `PointerSensor` (activation distance 5px), auto-scroll via dnd-kit's built-in `AutoScroller` (board container `overflow: auto`), drop indicator via dnd-kit default. On drop: optimistic `setQueryData` then `cardsApi.move`; **on `ApiError` rollback + `invalidateQueries(['cards'])`** (Review Focus #1). Same-column reorder = move with same step/isWip and new index (matches WPF DragAndDropLib behavior).
- Tests: pure `moveRequestFor` ×5 cases (same col before/after, cross col append → index=length, cross col onto position of `overCard`, drop into **empty sub-column → index 0**, Review Focus #3). Drop-indicator/DndContext wiring verified manually in browser at Task 12.

- [ ] **Step 1: Write failing tests** for `dnd.ts` per the table above. [ ] **Step 2: Run → fail.** [ ] **Step 3: Implement** `dnd.ts`. [ ] **Step 4: Run → pass.**
- [ ] **Step 5: Wire DndContext** into BoardPage (columns become droppable containers; sub-column card lists SortableContext over `card.id`); optimistic update + rollback via TanStack Query's `onMutate/onError/onSettled` with `useMutation`.
- [ ] **Step 6: Re-run BoardPage tests + full `npm run test -- --run`** → pass (existing renders must survive sensors; tests may use `userEvent` with activation-distance respected or call `onDragEnd` handler directly through an exported prop — export `BoardPage({onDragEndOverride})` if needed).
- [ ] **Step 7: Commit** `"feat: dnd-kitによる列間移動・列内並び替え(SeqNo不変条件はAPI側遵守)"`.

### Task 8: Card create / edit dialog / delete

**Files:**
- Create: `src/components/CardEditDialog.tsx`; Modify: `src/pages/BoardPage.tsx`; Test: `src/components/CardEditDialog.test.tsx`

**Interfaces:**
- Consumes: `cardsApi.create/patch/remove`, `CardDto`, `CardPatch` fields exactly: `title, tag, executors, summary, testCases, designOverview, cardType, storyPoints, releaseDate, isBlocked, isMergedIntoMaster, isMergedIntoMajorBranch, isScheduledToRelease` (spec §4.5 — full field parity with WPF `EditCardDialog`; WPF `EditCardDialog.xaml` is the layout reference, section order: title → tag → executors → cardType → storyPoints → releaseDate → checkboxes → summary/testCases/designOverview).
- Produces: add-card buttons (`add-card-{seqNo}-{wip|done}` from Task 5) → `cardsApi.create({boardId, processStepSeqNo, isWip})` then open the edit dialog on the returned card; card click opens dialog prefilled; Save → `cardsApi.patch`; Delete with confirm → `cardsApi.remove` then invalidate `['cards']`; dialog invalidates `['cards']` on any success. `summary`/`testCases`/`designOverview` are plain `<textarea>` HTML-text editing in the dialog (spec §4.5: parity with WPF raw TextBox, no WYSIWYG). Display side: `CardView` gains an expandable `<details>` region that renders `summary` HTML via `dangerouslySetInnerHTML` — per spec §4.2 "ブラウザが直接表示する (sanitization なし)"; this is an accepted, documented risk of the LAN trust model, do not "fix" it.

- [ ] **Step 1: Write failing tests**: create button POSTs `{boardId, processStepSeqNo:2, isWip:true}`; opening dialog prefills all 13 fields; Save PATCHes only changed fields (test `title` only → body has `{title}`; openapi-fetch `PATCH` body is the full `CardPatch` — assert only changed keys present); delete confirm calls DELETE `/api/cards/{id}`; invalid storyPoints text shows localized validation error and blocks Save.
- [ ] **Step 2: Run → fail. Step 3: Implement** dialog (MUI Dialog/TextField, `cardType` select from `CARD_TYPE_LABELS` in current language, `releaseDate` = `type="date"` input, checkboxes for the 4 booleans; storyPoints numeric parse with localized error) and wire create/delete into BoardPage.
- [ ] **Step 4: Run → pass. Step 5: Commit** `"feat: カード作成/編集ダイアログ・削除(全フィールドWPFパリティ)"`.

### Task 9: Process-step editor

**Files:**
- Create: `src/components/ProcessStepEditor.tsx`; Modify: `src/pages/BoardListPage.tsx`; Test: `src/components/ProcessStepEditor.test.tsx`

**Interfaces:**
- Consumes: `boardsApi.list` (to get current `processSteps`), `PUT /api/boards/{id}/process-steps` via new `boardsApi.updateSteps(id, steps: ProcessStepDto[]): Promise<BoardDto>` **added to `api/client.ts`** (same wrapper style as Task 2; throws `ApiError` incl. 409).
- Produces: `<ProcessStepEditor board={BoardDto} open onClose/>` opened from board-list row menu ("プロセスステップ設定" / localized `board.editSteps` key): sortable list (dnd-kit vertical Sortable) of steps with fields `name` (text — "(Limit:N)" is user-typed free text, WPF parity), `labelColor` (native color swatch select of named colors WPF uses: Aqua, DarkOrange, LimeGreen, CornflowerBlue, DarkSeaGreen, Gold, Gray, Green, OrangeRed, Plum, RoyalBlue, Silver, Teal, Tomato, Violet), `wipTitle`, `doneTitle`, `isStartOfCycleTime` / `isEndOfCycleTime` (radio: exactly one start, one end, or neither for backlog), add/remove buttons. Save = PUT full array with `phaseSeqNo` renumbered 0..n-1 → invalidate `['board']`,`['boards']`. **409 → localized conflict message + refetch steps.** Guard: cannot remove the last remaining step (disable Remove, localized tooltip).

- [ ] **Step 1: Write failing tests**: PUT body = full renumbered array after reorder; radio invariant enforced in UI (second start selection moves first); remove disabled at 1 step; 409 from stub renders conflict text, not a crash; `updateSteps` wrapper test in `client.test.ts` style.
- [ ] **Step 2: Run → fail. Step 3: Implement.** [ ] **Step 4: Run → pass. Step 5: Commit** `"feat: プロセスステップ編集(追加/削除/並べ替え/改名)"`.

### Task 10: Metrics — ECharts panel (5 charts)

**Files:**
- Create: `src/charts/options.ts`, `src/pages/MetricsPanel.tsx`; Modify: `src/pages/BoardPage.tsx`; Tests: `src/charts/options.test.ts`, `src/pages/MetricsPanel.test.tsx`

**Interfaces:**
- Consumes: `metricsApi` 5 methods (Task 2), DTOs `CfdDto/CycleTimeDto/ThroughputDto/BugsBlockedDto/VelocityDto` (fields per `schema.d.ts`, incl. `summary` sub-objects), `intVal`.
- Produces: 5 **pure** builders in `options.ts`: `cfdOption(d: CfdDto): EChartsOption` (stacked area over `days` per `series[].stepName`; legend + tooltip default), `cycleTimeOption(d)` (stacked bars `weeks[].buckets` percentages, line overlay `averageCycleTimeDays`, x = `weekEnds`), `throughputOption(d)` (bars `itemCounts` + performance stacks from `performance[]` ahead/onTime/delayed), `bugsBlockedOption(d)` (two areas `bugFixCounts`/`blockedCounts` over `days`), `velocityOption(d)` (line `storyPoints` over `weekEnds`, `hasUnknownStoryPoints` marks via `symbol` red `!` on affected points). `useECharts(ref, option)` tiny hook (`echarts.init` once, `setOption` on change, `dispose` on unmount, `resize` listener).
- `MetricsPanel(boardId)`: chart-type tabs (5, localized labels), persists `localStorage['kanban.lastMetricChartType']` and restores it; summary lines from `summary` fields (CFD maxWip/averageWip, throughput realAverage/estimated, velocity average2wk) rendered as text under chart.
- BoardPage toolbar gets a Kanban/Chart segmented toggle (`view` local state; chart tab renders `MetricsPanel`, keeps queries mounted state simple — unmount on toggle back is fine).

- [ ] **Step 1: Write failing pure-builder tests** (`options.test.ts`): each builder with realistic fixture asserts series type/count/x-axis data (e.g. `cfdOption(3 days, 2 steps)` → `series.length === 2`, `series[0].type === 'line'` with `stack` + `areaStyle`); **Review Focus #5: all-zero/empty fixtures** — `cfdOption({days: [], series: []})`, `velocityOption` with `storyPoints: []`, `throughputOption` with empty `weeks`/`performance` → returns option object without throwing, `series` empty array.
- [ ] **Step 2: Run → fail. Step 3: Implement** `options.ts`. [ ] **Step 4: Run → pass.**
- [ ] **Step 5: Write failing MetricsPanel test**: stubs return 5 DTO fixtures; asserts tab click switches chart (stub `useECharts` via module mock capturing option) and persists chosen type to localStorage; reload restores last type.
- [ ] **Step 6: Run → fail → implement** `useECharts` + `MetricsPanel` + BoardPage toggle. [ ] **Step 7: Run → pass. Step 8: Commit** `"feat: メトリクス5種(ECharts)・ボードページチャート切替"`.

### Task 11: Settings + About pages

**Files:**
- Modify: `src/pages/BoardListPage.tsx` (nav buttons); Create: `src/pages/SettingsPage.tsx`, `src/pages/AboutPage.tsx`; Tests in same dirs
- Create: copy `Kanban/ReleaseNote/ReleaseNote.html` + `ReleaseNote_jp.html` → `frontend/app/public/release-note/{en,ja}.html`

**Interfaces:**
- Consumes: `setLanguage` (Task 3).
- Produces: route `/settings` — language select (EN/日本語/中文) → `setLanguage` immediately, choice survives reload (`kanban.lang`); **only** this section (spec §4.6: DB choice and backup deleted). Route `/about` — `<iframe data-testid="release-note" srcDoc>` showing `ja` HTML when language is ja else `en` HTML (fetch from `/release-note/*.html`); iframe isolates WPF-era HTML styles from MUI.

- [ ] **Step 1: Write failing tests**: settings language switch re-renders AppBar title localized + persists; about renders ja fixture when lang=ja (stub fetch of the html). [ ] **Step 2: Run → fail. Step 3: Implement.** [ ] **Step 4: Run → pass. Step 5: Commit** `"feat: システム設定(言語切替のみ)とAbout(リリースノート)"`.

### Task 12: wwwroot packaging + SPA fallback + end-to-end smoke

**Files:**
- Modify: `Kanban.Api/Program.cs` (static files + fallback), `frontend/app/vite.config.ts` (confirm outDir)
- Create: `Kanban.Api.Tests/Endpoints/SpaFallbackTests.cs` (extend existing WebApplicationFactory test suite; name per existing files in `Kanban.Api.Tests/Endpoints/`)

**Interfaces:**
- Consumes: Task 1's `build.outDir = '../Kanban.Api/wwwroot'`.
- Produces: single-process deploy — `dotnet publish Kanban.Api -c Release` serves SPA at `/` and deep links (`/boards/xyz`) return `index.html`; API routes unchanged; `/api/*` misses stay 404/400 JSON, **never** fall back to index.html.

- [ ] **Step 1: Write failing test** `SpaFallbackTests.cs` (follow existing `TestDatabase`/factory pattern of sibling endpoint tests): `GET /` → 200 `text/html`; `GET /boards/abc` → 200 html; `GET /api/boards/notanid` → 400 (not html). Expected: FAIL (404 today).
- [ ] **Step 2: Implement** in `Program.cs` before `app.Run()`: `app.UseDefaultFiles(); app.UseStaticFiles();` then **one** fallback: `app.MapFallback(ctx => ctx.Request.Path.StartsWithSegments("/api") ? Results.NotFound() : Results.File("wwwroot/index.html", "text/html"));` (single fallback endpoint so unmatched `/api/*` never returns HTML).
- [ ] **Step 3: Run API tests** `dotnet test Kanban.Api.Tests` → pass (existing suites unchanged + new fallback tests).
- [ ] **Step 4: Build SPA + full smoke**: `cd frontend/app && npm run test -- --run && npm run build`; then start `dotnet run --project Kanban.Api`; `curl -s http://localhost:5000/ | head -3` → html doctype; `curl -s http://localhost:5000/api/health` → ok. Kill server.
- [ ] **Step 5: Manual browser acceptance** (drive with browser or note for user; Review Focus #3 and Task 7 drop-indicator verification happen here): open board, drag card across columns and onto empty sub-column, edit fields, create/delete card, check all 5 charts, switch EN→JP→ZH and confirm every visible string changes, on a second browser tab edit the same board and refresh — data consistent (Review Focus #1/#2 spot checks).
- [ ] **Step 6: Commit** `"feat: SPAのwwwroot同梱・SPAフォールバック(1プロセスデプロイ)"`.

---

## After this plan (out of scope here, from spec §5 step 7)

Dual-client regression on the full flow was already seeded on this branch (`dual-client regression` commit) — re-run `dotnet test` in CI posture; deployment (Windows service / `dotnet publish` runbook) and WPF decommission come after user acceptance of the SPA.
