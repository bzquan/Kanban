# カンバンWeb移植(バックエンド: Kanban.Domain + Kanban.Api)実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 既存WPFアプリのModel/Adapter/KanbanDB層をnet10.0クラスライブラリ `Kanban.Domain` へ抽出し、それを再利用するASP.NET Core Web API `Kanban.Api`(ボード/カード/プロセスステップ/メトリクスのRESTエンドポイント)を構築する。

**Architecture:** 3層は維持(Kanban.RepositoryのMongo文書 + Kanban.Modelドメイン + 新サービス層)。新規の書き込み・集計ロジックは書かず、既存のDBCards/DBBoards/DBActivitiesとModel.CardのActivity合成アルゴリズムをそのまま呼ぶ薄いサービス(`CardCommandService`/`BoardCommandService`/`MetricsQueryService`)をDomainに追加し、APIはその呼び出し側。データは既存MongoDB(デプロイ単位で`appsettings.json`の1DB固定)。

**Tech Stack:** .NET 10 (SDK 10.0.404 確認済)、ASP.NET Core Minimal APIs + `Microsoft.AspNetCore.OpenApi`、MongoDB.Driver 3.8.1(既存固定バージョン)、MSTest 4.0.2。

**Spec:** `docs/superpowers/specs/2026-11-13-kanban-web-migration-design.md`(この計画は同設計書に沿う。両方読むこと)

## Global Constraints

- .NET 10。新規プロジェクトのTFMはすべて `net10.0`(WPF参照のためだけに`net10.0-windows`へ戻さない。WPF本体は`net10.0-windows`のまま`Kanban.Domain`を参照できる)
- MongoDB.Driver 3.8.1 のみ使用(EF Core・Novell MongoDB.Driver等の追加ドライバー禁止)。新規NuGetはASP.NET Core同梱品とテスト用パッケージに限定
- 認証・認可・HTTPS・CORSは実装しない(設計書スコープ外)。CORS設定ミドルウェアは追加しない
- 本番DB名は`appsettings.json`の `Kanban:DB_NAME`(既定 `kanban`)で固定。APIからDBを切り替える引数・ヘッダーを作らない
- 自動テストが接続・DropしてよいDBは `kanban_test` のみ。`DB_HOST`は環境変数 `KANBAN_DB_HOST`(既定 `localhost:27017`・現環境ではリモートへフォワード済み)
- WPFアプリ(`Kanban/Kanban.csproj`)は移行期間中ビルド・起動可能であり続けること(各タスクの受け入れ条件に含む)
- 公開RESTのURLはすべて `/api` プレフィックス。レスポンスはcamelCase JSON
- 各タスクの末尾でコミット(メッセージは `feat:`/`test:`/`fix:`/`refactor:` 接頭辞 + 内容)
- 既存コードの既存メソッドは、テストが要求する場合を除きシグネチャを変えない(WPF側の呼び出しを壊さない)

## Review Focus

仕様書が黙っているが壊れやすい入力と期待挙動。各行のテストを指定タスクに追加する。

1. **存在しない/不正な`ObjectId`**(board/card)→ 500ではなく400(不正形式)または404(未在)。→ Task 4, 6, 7
2. **フィルタ文言がリリース日数字のみ**(例 `20261113`、テキスト0語)→ 既存WPFと同じ「その日のリリースのカードのみ」が返る。既存`CreateWhereFilter`は語ゼロだと`Empty`を返し`AddReleaseDateFilter(null,…)`経路を踏むため、APIは文言なし=全件・数字のみ=日付条件として回帰テストする。→ Task 5
3. **範囲外`ProcessStepSeqNo`へのmove**(削除された列・負数)→ 400。→ Task 6
4. **列の削除を、カードが残っている/列数3未満の状態で要求**→ 409(既存`CanDeleteProcessStep`のガードと同じ条件)。→ Task 4
5. **空ボード/アクティビティゼロのボードのメトリクス**→ 例外でなく全系列ゼロ・点数0を返す。→ Task 7
6. **同一列への同時move**(2リクエストが交互に)→ last-write-winsで可。ただしどの順序でも列のSeqNoは0..N-1の連続に収まる(読み取り時に`AdjustCardSeqNo`が再圧縮するため、最終状態の整合をテストで固定)。→ Task 6

## ファイル構成

```
Kanban.slnx                         # 4プロジェクト化(Kanban, Kanban.Domain, Kanban.Api, Kanban.Api.Tests)+既存TestKanban
Kanban/Kanban.csproj                # WPF本体: Model/Adapter/Utilのソースを削除し Kanban.Domain へProjectReference置換
Kanban.Domain/                      # 新規 net10.0 クラスライブラリ
  Kanban.Domain.csproj              # MongoDB.Driver 3.8.1 のみ参照
  Model/…                           # src/Model/* をそのまま移動(アセンブリ名変更なしのためnamespaceは Kanban.Model のまま)
  Repository/…                      # src/Adapter/KanbanDB/* をそのまま移動(namespace Kanban.Repository)
  Infrastructure/…                  # src/Adapter のうち BoardRepository/CardRepository/ActivityRepository/BoardFactory/CardFactory を移動(namespace Kanban.Infrastructure)
  Util/…                            # WPF非依存のUtilのみ移動(namespace Kanban.Util)
  Services/BoardCommandService.cs   # 新規: ボード作成/削除/プロセスステップ編集
  Services/CardCommandService.cs    # 新規: カード作成/編集/move/削除(不変条件の唯一の実装)
  Services/MetricsQueryService.cs   # 新規: メトリクス5種の計算エントリ
  Services/CardsActivitiesCacheInstance.cs # 新規: 既存静的CardsActivitiesCacheのインスタンス版(計算単位でnew)
Kanban.Api/                         # 新規 ASP.NET Core net10.0
  Kanban.Api.csproj                 # Microsoft.AspNetCore.OpenApi
  Program.cs                        # DI登録 + ミニマルAPIマッピング + OpenAPI(wwwroot同梱はSPA実装の次計画で行う)
  appsettings.json                  # Kanban:DB_HOST / Kanban:DB_NAME / Kanban:CumulateCardsOnBackBoard / Kanban:LimitNum4QueringLastProcess
  Contracts/BoardDto.cs  CardDto.cs  ProcessStepDto.cs  MetricsDtos.cs   # 往復DTO
  Endpoints/BoardsEndpoints.cs  CardsEndpoints.cs  MetricsEndpoints.cs
Kanban.Api.Tests/                   # 新規 net10.0 MSTest
  Kanban.Api.Tests.csproj           # MSTest 4.0.2, ProjectReference: Kanban.Api
  TestDatabase.cs                   # kanban_test 固定の構築/破棄fixture
  Services/*Tests.cs  Endpoints/*Tests.cs
```

移動の原則: **ファイルは移動するだけで中身は書換えない**(namespace/usingは元の名前を維持するため変更不要)。WPFから見て`Kanban.Model`/`Kanban.Repository`/`Kanban.Infrastructure`/`Kanban.Util`の名前空間が変わらなければ、WPFの`using`はそのまま通る。

---

### Task 1: Kanban.Domain クラスライブラリ抽出

WPFからModel/Adapter/KanbanDB/Util-cleanを切り出し、WPF・既存テストがそのままビルドできる状態にする。**このタスクでロジックは1行も変更しない**(移動のみ)。テスト基線(`dotnet test`のCreateIndexesクラッシュ)はTask 2で直すため、このタスクの受け入れはビルド成功まで。

**Files:**
- Create: `Kanban.Domain/Kanban.Domain.csproj`
- Move: `Kanban/src/Model/**`(26ファイル)→ `Kanban.Domain/Model/`、`Kanban/src/Adapter/KanbanDB/**` → `Kanban.Domain/Repository/`、`Kanban/src/Adapter/{BoardRepository,CardRepository,ActivityRepository,BoardFactory,CardFactory}.cs` → `Kanban.Domain/Infrastructure/`、`Kanban/src/Util/{ConfigReader,DateTimeUtil,StringExtension,EnumerableExtension,EventAggregator,ILocalization,KanbanDefinitions}.cs` → `Kanban.Domain/Util/`
- Keep in WPF(移動しない): `Kanban/src/Adapter/{DBBackup,Process*,IProcess*}.cs`、`Kanban/src/Adapter/AppSettings.cs`、`Kanban/src/Model/{IProcessExecutorClient,IDBBackup}.cs`、`Kanban/src/Util/{Util,EnumUtil,HandWriteFontFamily,CircleNumImages,IAppSettings}.cs`、`Kanban/src/View/**`、`Kanban/src/ViewModel/**`
- Modify: `Kanban/Kanban.csproj`(パッケージ参照からMongoDB.Driverを削除し`<ProjectReference Include="..\Kanban.Domain\Kanban.Domain.csproj" />`追加。SharpCompress/Unityは残す)
- Modify: `Kanban/src/Util/Util.cs` の `PackImageURI`/`RoundUpByDivision`/`IfNotNull` はModelから呼ばれる(`EstimatedWorkEffortExtension.cs:83`、`BoardHistory.cs:147`、`CycleTimeMetrics.cs:41`、`CycleTimeDistribution.cs:49`)ため、**これら4メソッドのみを新ファイル `Kanban.Domain/Util/DomainUtil.cs` の `public static class DomainUtil` にコピー**し、Model側4呼び出し箇所を `DomainUtil.` に書き換える(`[assembly: InternalsVisibleTo("Friend1a")]`は`DBClient.cs`にあるのでDomainへ移動される)
- Modify: `Kanban.slnx`(Kanban.Domainプロジェクト追加)

**Interfaces:**
- Consumes: なし(既存コードの移動)
- Produces: `Kanban.Domain` アセンブリの公開型 `Kanban.Model.*`、`Kanban.Repository.*`、`Kanban.Infrastructure.{BoardRepository,CardRepository,ActivityRepository,BoardFactory,CardFactory}`、`Kanban.Util.{ConfigReader,DateTimeUtil,StringExtension,EventAggregator,ILocalization,KanbanDefinitions}`、`Kanban.Util.DomainUtil` — すべて既存と同一シグネチャ

- [ ] **Step 1: Kanban.Domain.csproj を作成**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MongoDB.Driver" Version="3.8.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: ファイルを移動**(git mv で履歴を残す。上記Move/Keepの対応表どおり)。`IAppSettings.cs`はWPFに残すため、`DBClient(IAppSettings)`コンストラクタはWPF側 partial で維持できない — 代わりに **`DBClient.cs` の `IAppSettings` 依存を切り離す最小変更を許可された例外として実施**: `public DBClient(IAppSettings setting)` を `public DBClient(string userDatabase)` に変更し、`DBName = GetCurrentDBName(userDatabase)` とする。WPFの`DependencyInjector.cs`の `IDBClient→DBClient` 登録を `new DBClient(AppSettings.Database)` を返すファクトリ登録に書き換える(既存Unity登録の書き換えはここだけ)
- [ ] **Step 3: DomainUtil 分割**(上記4メソッドを`DomainUtil.cs`へコピー、Model側4ファイルの呼び出しを置換)
- [ ] **Step 4: ビルド確認**

Run: `dotnet build d:\Kanban\Kanban.slnx`
Expected: Kanban.Domain・Kanban(WPF)・TestKanbanすべて 0 error(WPFの`dotnet build`成功=移行期間の共存要件)

- [ ] **Step 5: コミット**

```bash
git add -A && git commit -m "refactor: extract Kanban.Domain class library from WPF project"
```

---

### Task 2: テスト基線の修复(DBActivities async void + kanban_test 固定)

既知のクラッシュ(`DBActivities` コンストラクタの `async void CreateIndexes()` が `[TestInitialize]` の `DropDatabase` と競合し testhost を落とする)を消し、テスト専用DB `kanban_test` に接続する基盤を作る。**このタスクで `dotnet test` が初めてグリーンになる。**

**Files:**
- Modify: `Kanban.Domain/Repository/DBActivity.cs`(`DBActivities` クラス。コンストラクタの `CreateIndexes()` 呼び出しを削除し `public async Task EnsureIndexesAsync()` に変更。現在の`CreateIndexes`は`async void`→`async Task`へ。既存呼び出し箇所はコンストラクタのみ)
- Modify: `Kanban/src/View/DependencyInjector.cs`(DI4Repositoryの`IDBClient`登録のところで、`EnsureIndexesAsync()`をアプリ起動時`App.xaml.cs OnStartup`でawaitして呼ぶ。`DBUpgrader.UpgradeDB()`の直後でOK)
- Create: `Kanban.Api.Tests/Kanban.Api.Tests.csproj`(net10.0 / MSTest 4.0.2 / ProjectReference: `Kanban.Domain`)と `Kanban.Api.Tests/TestDatabase.cs`
- Modify: `Kanban.slnx`
- Test: `Kanban.Api.Tests/Services/SmokeTest.cs`

**Interfaces:**
- Consumes: `Kanban.Repository.DBClient(string dbHost, string dbName)` — 追加が必要なら新設: 現パラメータなし`DBClient()`(test only・localhost:27017固定)に加え **`public DBClient(string dbHost, string dbName)`**(ConfigReaderを通さない)を`DBClient.cs`に追加。既存`DBClient()`はTestKanbanが使うため残す
- Produces: `TestDatabase` クラス(`Kanban.Api.Tests`): `public static string DBHost` / `public static string DBName => "kanban_test"` / `public static DBClient NewClient()`(環境変数`KANBAN_DB_HOST`??`localhost:27017`で`new DBClient(host,"kanban_test")`) / `public static void Reset()`(DropDatabase("kanban_test") + `EnsureIndexesAsync().GetAwaiter().GetResult()`を`DBActivities(new DBClient…)`経由)

- [ ] **Step 1: 失敗するスモークテストを書く**

```csharp
[TestClass] public class SmokeTest {
  [TestMethod] public async Task ConnectAndDropKanbanTest() {
    var client = TestDatabase.NewClient();
    await client.DB.GetCollection<Repository.Board>("Boards").InsertOneAsync(new Repository.Board("t","d"));
    var list = await client.DB.GetCollection<Repository.Board>("Boards").FindAsync(_ => true);
    Assert.AreEqual(1, (await list.ToListAsync()).Count);
    TestDatabase.Reset(); // Dropして消えること
    var after = await client.DB.GetCollection<Repository.Board>("Boards").CountDocumentsAsync(_ => true);
    Assert.AreEqual(0, after);
  }}
```

- [ ] **Step 2: 実行して失敗を確認**(CreateIndexesクラッシュまたはTestDatabase未定義でビルド失敗)

Run: `dotnet test d:\Kanban\Kanban.Api.Tests\Kanban.Api.Tests.csproj -v m`
Expected: FAIL(未定義 or `MongoCommandException … dropDatabase`)

- [ ] **Step 3: `CreateIndexes` を `EnsureIndexesAsync` へ変更**(Files欄参照)+ WPF起動時にawaitして呼ぶ。既存テスト(TestKanban)の`new DBClient()`経路はindex作成なしで動く(機能的に不要・`Sort/Find`は集合作成時に暗黙作成)
- [ ] **Step 4: `DBClient(string dbHost, string dbName)` 追加 + TestDatabase.cs 実装**(上記Interfaces欄のとおりの最小実装)
- [ ] **Step 5: 全テスト実行してグリーンを確認**

Run: `dotnet test d:\Kanban\Kanban.slnx -v m`
Expected: Kanban.Api.Tests パス + TestKanban の既存テストもクラッシュしない(mongodump/mongorestore必須の ProgramExistTest/RunDBDumpRestoreTest は環境依存で失敗してよい — 失敗するなら`[Ignore("requires mongodump on PATH and C:\\Work path")]`を付けて明示的にスキップ化し、その旨をコミットメッセージに残す)

- [ ] **Step 6: コミット**

```bash
git add -A && git commit -m "fix: replace async-void CreateIndexes with awaited EnsureIndexesAsync; add kanban_test fixture"
```

---

### Task 3: Kanban.Api 足場(DI・設定・OpenAPI・起動時初期化)

**Files:**
- Create: `Kanban.Api/Kanban.Api.csproj`(SDK Microsoft.NET.Sdk.Web、net10.0、`Microsoft.AspNetCore.OpenApi` 同梱バージョン、ProjectReference: Kanban.Domain)
- Create: `Kanban.Api/Program.cs`
- Create: `Kanban.Api/appsettings.json`
- Modify: `Kanban.Domain/Util/ConfigReader.cs` — **追加変更**: `public static class ConfigOverrides { public static readonly Dictionary<string, string> Values = new(); }` を同ファイルに新設し、`GetValue<T>` は第一に `ConfigOverrides.Values` を参照(未登録なら従来どおり `AppSettingsReader`)。既存 `DBCards.LimitNum4QueringLastProcess`(DBCards.cs:13)と `BoardHistoryBuilder` の `CumulateCardsOnBackBoard`(BoardHistoryBuilder.cs:116)はこのConfigReader経由のままなので、サーバー側は `AddKanbanServices` で appsettings の2値を `ConfigOverrides.Values` に書き込む。**呼び出し側コードは1行も変えない**。WPFは`Values`が空のまま=従来動作
- Modify: `Kanban.slnx`
- Test: `Kanban.Api.Tests/Endpoints/HealthEndpointTest.cs`(2つのフラグが上書きされることもここで検証: ConfigOverrides経由でLimit=3に上書き→最終列Done取得が3件で切る)

**Interfaces:**
- Consumes: `DBClient(string dbHost, string dbName)`(Task 2)、`DBActivities.EnsureIndexesAsync()`(Task 2)、`DBUpgrader(DBVersion, IDBCard).UpgradeDB()`(既存)、`Model.Board.BoardRepository` 等の静的プロパティ(既存・起動時1回だけシングルトンを代入)
- Produces:
  - `Program`(テストから `WebApplicationFactory<Program>` で起動可能にするため `public partial class Program {}` を末尾に追加)
  - DI登録メソッド `public static IServiceCollection AddKanbanServices(this IServiceCollection services, IConfiguration config)`(`Kanban.Api/ServiceCollectionExtensions.cs`)— Task 4以降はこれに追加していく形になる
  - 設定バインド: `KanbanApiOptions { string DBHost; string DBName; bool CumulateCardsOnBackBoard; int LimitNum4QueringLastProcess; }`(appsettings の `Kanban` セクション)
  - **静的プロパティ起動時結線**: `Model.Board.BoardRepository`、`Model.Card.CardRepository`、`Model.Activity.ActivityRepository`、`Model.DevProcess.Localization` にシングルトンを代入(既存WPFの`DependencyInjector.DI4Model`と同じ)。サーバー用 `ILocalization` 実装 `ServerLocalization`(`Kanban.Api/ServerLocalization.cs`)は **リクエストごと言語が異なるため静的結線しない** — DevProcessの既定列名生成はTask 4の`BoardCommandService`が明示的に言語引数を受け取る設計とし、`Model.DevProcess.Localization` は EN 固定(`LocalizationResources.en.json`由来)の簡易実装で結線する

- [ ] **Step 1: 失敗するヘルスチェックテストを書く**

```csharp
[TestClass] public class HealthEndpointTest {
  private WebApplicationFactory<Program> _app = new();
  [TestMethod] public async Task GetHealthReturns200() {
    var resp = await _app.CreateClient().GetAsync("/api/health");
    Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
  }
  // 注意: テストでは環境変数 KANBAN_DB_HOST / KANBAN_DB_NAME=kanban_test を
  // _app.WithWebHostBuilder(b => b.UseSetting("Kanban:DB_HOST", TestDatabase.DBHost)) で上書きする
  // (TestDatabase に ConfigureApp(IConfigurationBuilder相当)用の文字列定数を置く)
}
```

- [ ] **Step 2: 実行して失敗を確認**(`dotnet test Kanban.Api.Tests` — プロジェクト不存在で失敗)
- [ ] **Step 3: Kanban.Api を実装**: `Program.cs` は `builder.Services.AddKanbanServices(builder.Configuration)` → `EnsureIndexesAsync` と `UpgradeDB()` を起動時にawait(※テスト時はDB名が`kanban_test`なので本番DBに触れない)→ `MapGet("/api/health", () => Results.Ok(new { status = "ok" }))` → `MapOpenApi()` → `app.Run()`。`appsettings.json` に Global Constraints の4キー(既定値: `192.168.21.150:27017` / `kanban` / `true` / `20`)
- [ ] **Step 4: 実行してグリーンを確認**(`dotnet test Kanban.Api.Tests -v m`)
- [ ] **Step 5: OpenAPIが生成されることの確認**

Run: `dotnet run --project Kanban.Api --urls http://localhost:5093` を別シェルで起動し `curl http://localhost:5093/openapi/v1.json`
Expected: JSONに `/api/health` が含まれる

- [ ] **Step 6: コミット** `git commit -am "feat: scaffold Kanban.Api with DI, options, OpenAPI and startup init"`

---

### Task 4: ボード系エンドポイント + BoardCommandService

`GET/POST /api/boards`、`DELETE /api/boards/{id}`、`PUT /api/boards/{id}/process-steps`。列編集は既存 `Model.Board` の `AddProcessStep/DeleteProcessStep/SaveProcessStep/ChangeCardsAndActivitySeqNo` をそのまま使う。

**Files:**
- Create: `Kanban.Domain/Services/BoardCommandService.cs`、`Kanban.Domain/Services/ILocalizationFactory.cs`
- Create: `Kanban.Api/Contracts/BoardDto.cs`、`Contracts/ProcessStepDto.cs`、`Endpoints/BoardsEndpoints.cs`
- Modify: `Kanban.Api/ServiceCollectionExtensions.cs`、`Program.cs`(Map)
- Test: `Kanban.Api.Tests/Services/BoardCommandServiceTest.cs`、`Endpoints/BoardsEndpointTest.cs`

**Interfaces:**
- Consumes: `IBoardFactory.CreateBoard()`(既存・既定5列生成)、`IBoardRepository.LoadBoards()/Save()/Delete()/ChangeCardsAndActivitySeqNo(board,from,to)`(既存)、`IDBBoard.Save/GetBoards`(既存)、既存 `Repository.ProcessStep` フィールド(`PhaseSeqNo/LabelColor/Name/WIPTitle/DoneTitle/IsStartOfCycleTime/IsEndOfCycleTime`)、`LabelColorNames`(既存、8色)
- Produces:
  - `public class BoardCommandService(IBoardFactory, IBoardRepository, IDBBoard)`:
    `Task<List<Model.Board>> GetBoardsAsync()` / `Task<Model.Board> CreateBoardAsync(string uiLanguage)` / `Task DeleteAsync(ObjectId boardId)` / `Task<UpdateResult> UpdateProcessStepsAsync(ObjectId boardId, List<Repository.ProcessStep> newSteps)`
  - `ILocalizationFactory.Create(Languages lang) : ILocalization` — resxではなく**JSONリソース**(`Kanban.Domain/Resources/locales/{en,ja,zh}.json`)から生成。キーは既存`ILocalization`の13プロパティ名(`ProcessBackLogTitle`等)。WPF既存の`Kanban.Localization`は使わない(Domainがresxに依存しないため)
  - DTO(camelCase JSON): `BoardDto { string Id; int SeqNo; string Title; string Description; string Developers; List<ProcessStepDto> ProcessSteps; }`、`ProcessStepDto { int PhaseSeqNo; string LabelColor; string Name; string WIPTitle; string DoneTitle; bool IsStartOfCycleTime; bool IsEndOfCycleTime; }`
  - REST: `GET /api/boards` → 200 `List<BoardDto>`(SeqNo昇順) / `POST /api/boards {uiLanguage:"ja"|"zh"|"en"}` → 201 BoardDto / `DELETE /api/boards/{id}` → 204 / `PUT /api/boards/{id}/process-steps` → 200 + 更新後BoardDto。`UpdateProcessStepsAsync` の戻りは `ProcessStepUpdateResult { bool Success; UpdateResult? Update; GuardReason? Guard; }`(`GuardReason { TooFewSteps, StepInUse }`)
  - 注意: 既存 `BoardRepository.LoadBoards()` は**ボードが1件も無いと1枚自動作成する**(WPF動作)。この副作用はパリティのため維持する(テストは「Reset後のGETで1枚できる」ことまで固定)
  - `TestDatabase` にヘルパーを追加: `WireModel(DBClient)`(静的プロパティ結線を差し替え) / `NewBoardCommandService()` / `NewCardCommandService()` / `NewMetricsQueryService()` / `GetActivities(ObjectId cardId)`
  - `UpdateProcessStepsAsync` のガード(**Review Focus 4**): 新ステップ数が3未満→`GuardResult.TooFewSteps`、削除されようとする`PhaseSeqNo`に`ICardRepository.ExistCards(board, step)`がtrue→`GuardResult.StepInUse`、いずれかなら更新せずAPIは409

- [ ] **Step 1: 失敗するサービステストを書く**(`BoardCommandServiceTest`)

```csharp
[TestInitialize] public void Setup() { TestDatabase.Reset(); /* 静的結線は TestDatabase.WireModel(新規DBClient) で */ }
[TestMethod] public async Task CreateBoardJp_GetsFiveLocalizedSteps() {
  var svc = TestDatabase.NewBoardCommandService();
  var board = await svc.CreateBoardAsync("ja");
  Assert.AreEqual(5, board.DevProcess.ProcessSteps.Count);
  Assert.AreEqual(1, board.DevProcess.ProcessSteps[1].IsStartOfCycleTime ? 1 : 0);
  Assert.AreEqual(3, board.DevProcess.ProcessSteps[3].IsEndOfCycleTime ? 1 : 0);
  StringAssert.Contains(board.DevProcess.ProcessSteps[2].Name, "開発"); // ja.json の Development 列名
}
[TestMethod] public async Task DeleteSteps_WhenGuardFails_ReturnsGuardWithoutChange() { /* 3件未満→TooFewSteps、カードあり列削除→StepInUse */ }
[TestMethod] public async Task InvalidObjectId_ThrowsFormatException() { /* Endpoints側で400マッピング、サービスはFormatException */ }
```

- [ ] **Step 2: 実行して失敗を確認**(`dotnet test Kanban.Api.Tests`、型未定義でビルド失敗)
- [ ] **Step 3: locales JSON 3言語 + ILocalizationFactory + BoardCommandService + DTO + BoardsEndpoints を実装**。既定列名の数値(WPF `Resources.resx` と同一にする: 英 `Backlog(Limit:∞)`/`Requirement(Limit:3)`/`Development(Limit:5)`/`Test(Limit:3)`/`Release(Limit:∞)` — ja/zhも既存resxから**値を写す**こと。WIP小列の`WIPTitle`は既定5列のみ `ProcessPreparingTitle`(Preparing/準備中)、他は `ProcessWIPTitle`)
- [ ] **Step 4: サービステストでグリーン確認**
- [ ] **Step 5: エンドポイントテストを書く→実装→グリーン**(WebApplicationFactoryでPOST→GET→PUT→DELETEの往復。不正id→400、存在しないid→404、ガード→409。**Review Focus 1** のテストはここで固定)
- [ ] **Step 6: WPFビルド維持確認** `dotnet build Kanban.slnx`(0 error)
- [ ] **Step 7: コミット** `git commit -am "feat: board CRUD and process-step endpoints with guards"`

---

### Task 5: カード読み取りエンドポイント(列別取得・フィルタ・バックボード)

`GET /api/boards/{id}/cards?processStepSeqNo=&isWip=&filter=&onBoard=front|back|all`。クエリは既存 `CardRepository.GetCards(board, workState, cardFilter, onBoardFilter)` を**そのまま**使う(フィルタ分割・リリース日抽出・180日cut-off・最終列Limit=20はすべて既存DBCardsの実装)。サーバーが新規にフィルタロジックを書かないことがこのタスクの要。

**Files:**
- Create: `Kanban.Api/Contracts/CardDto.cs`、`Endpoints/CardsEndpoints.cs`(読み取りのみ)
- Modify: `Kanban.Api/ServiceCollectionExtensions.cs`(`ICardRepository→CardRepository` 登録追加)、`Program.cs`(Map)
- Test: `Kanban.Api.Tests/Endpoints/CardsReadEndpointTest.cs`

**Interfaces:**
- Consumes: `ICardRepository.GetCards(Board, WorkState, string, OnBoardFilter)`(既存・SeqNo圧縮`AdjustCardSeqNo`込み)、`IBoardRepository.LoadBoards()`(ボード解決用)、既存 enum `OnBoardFilter {OnFrontBoard, OnBackBoard, All}`
- Produces:
  - `CardDto { string Id; int SeqNo; string Title; string Tag; string Executors; string Summary; string TestCases; string DesignOverview; string CardType; int StoryPoints; DateTime CreateDate; DateTime StateChangedDate; DateTime ReleaseDate; bool IsOnBackBoard; bool IsBlocked; bool IsMergedIntoMaster; bool IsMergedIntoMajorBranch; bool IsScheduledToRelease; WorkStateDto WorkState; }`、`WorkStateDto { int ProcessStepSeqNo; bool IsWIP; bool IsBlocked; bool IsMergedIntoMaster; bool IsMergedIntoMajorBranch; }`
  - REST: `GET /api/boards/{id}/cards` — クエリ: `processStepSeqNo`(既定0)、`isWip`(既定true)、`filter`(既定空)、`onBoard`(`front`既定|`back`|`all`)→ 200 `List<CardDto>`(SeqNo昇順。最終列DoneのみStateChangedDate降順+Limit。これは既存実装の挙動そのまま)
  - `public static class CardDtoMapper { public static CardDto From(Model.Card card); }`(`Kanban.Api/Contracts/CardDto.cs` — 以降のタスクも共通使用)

- [ ] **Step 1: 失敗するテストを書く**(`CardsReadEndpointTest`、`TestDatabase`でボード+カードを直接リポジトリ投入してHTTP照合)

テストケース(すべてこの1ファイル):
  1. 列(0,WIP)に3枚(投入は`SeqNo` 2,0,1でバラバラに)→ 返却は `SeqNo` 0,1,2 順
  2. `filter=abc def` → Title/Executors/Tagの部分一致(既存语义: 語間はOR、Title/Tagはlowercase、Executorsはそのまま)で絞り込まれる
  3. **Review Focus 2**: `filter=20261201`(リリース日そのものの日付カード1枚+別カード2枚)→ 日付カードのみ / `filter=`空→全件
  4. `onBoard=back`→`IsOnBackBoard=true`かつ`OnBackBoardDate`が180日以内のみ
  5. 最終列 Done(`processStepSeqNo=4&isWip=false&onBoard=front`)25枚投入 → 20件でStateChangedDate降順(既存LimitNum4QueringLastProcess)

- [ ] **Step 2: 実行して失敗を確認**
- [ ] **Step 3: CardDto/Mapper/読み取りエンドポイント実装**。レスポンスの`WorkState`はDTOへ写像。ボード未在→404、id不正→400
- [ ] **Step 4: グリーン確認** `dotnet test Kanban.Api.Tests -v m`
- [ ] **Step 5: コミット** `git commit -am "feat: card read endpoint delegating to existing DBCards query"`

---

### Task 6: カード書き込みエンドポイント + CardCommandService(不変条件の中心)

`POST /api/cards`、`PATCH /api/cards/{id}`、`PATCH /api/cards/{id}/move`、`DELETE /api/cards/{id}`。Activity監査証跡とSeqNo不変条件は **`CardCommandService` のみが実装する**(フロントエンドも将来ここしか使えない)。

**Files:**
- Create: `Kanban.Domain/Services/CardCommandService.cs`
- Modify: `Kanban.Api/Endpoints/CardsEndpoints.cs`、`ServiceCollectionExtensions.cs`
- Modify: `Kanban.Domain/Model/Card.cs`(**追加のみ**: `public async Task CreateNewActivitiesAsync(WorkState workState)` を新設し、既存 `public async void CreateNewActivities(WorkState)` は `{ CreateNewActivitiesAsync(workState).GetAwaiter().GetResult(); }` 相当の同期待ちラッパーに変更 — 呼び出し側WPFの挙動は「完了までブロック」に変わるが、WPFはUIスレッドでawaitしない同期呼び出しのため実害ない。リスク詳細は下記Note参照)
- Test: `Kanban.Api.Tests/Services/CardCommandServiceTest.cs`、`Endpoints/CardsWriteEndpointTest.cs`

**Interfaces:**
- Consumes: `ICardFactory.CreateCard(ObjectId boardID, WorkState, Card srcCard)`(既存)、既存 `DBCards.ExecMoveCards/ChangeCardsSeqNo`、`ICardRepository.Update/Delete`、`IActivityRepository`、`IBoardRepository.LoadBoards`
- Produces — `public class CardCommandService(ICardFactory, ICardRepository, IActivityRepository, IDBCard, IBoardRepository)`:
  - `Task<Model.Card> CreateCardAsync(ObjectId boardId, int processStepSeqNo, bool isWip)` — 既存ファクトリで插入後、その列の現存カード数=新`SeqNo`を**列の末尾**として `Update(card, seqNo, nameof(SeqNo))` で確定(WPFはObservableCollection追加時に0..N-1再採番するが、APIは末尾追加なら追加採番で同等。既存`AdjustCardSeqNo`が次回読取時に正規化する)
  - `Task MoveCardAsync(ObjectId cardId, MoveCardRequest req)` — `MoveCardRequest { int ProcessStepSeqNo; bool IsWip; int Index; }`:
    1. 対象列(current以外)から`GetCards`で列カード取得(SeqNo圧縮込み)
    2. 移動元・移動先それぞれ `List<Card>` からremove/insert(index=clamp(0..Count))し、変更されたものだけ `card.SeqNo` をリポジトリUpdate
    3. 移動元列と移動先列の `WorkState` が異なる場合のみ `await card.CreateNewActivitiesAsync(newWorkState)`(Forward移動の中間Activity合成は既存Modelの`CreateActivities4IntermediateProcesses`が担う。同一列内reorderはActivityなし=既存WPFと同じ)
    4. 同一カードへの並行moveはlast-write-winsで可(**Review Focus 6**: 2リクエスト交互実行後、列SeqNoが0..N-1連続で収まることをアサート)
  - `Task UpdateFieldsAsync(ObjectId cardId, CardPatch patch)` — `CardPatch` は nullable フィールド(Title/Tag/Executors/Summary/TestCases/DesignOverview/CardType/StoryPoints/ReleaseDate/IsBlocked/IsMergedIntoMaster/IsMergedIntoMajorBranch/IsScheduledToRelease)。null以外の各フィールドのみ `ICardRepository.Update(card, value, propertyName)`。**blocked/merged系は `Model.Card` のsetter経由で呼ぶ**(同一ステップの新Activityが積まれる既存挙動)。→ サービスはこれらのフラグを `card.IsBlocked = v` とsetter代入し、setterのasync void Updateを待たずに済むよう **`Model.Card` に `public Task SetFlagAsync(string flagName, bool value)` を追加**(setterと同じ`CreateWith*`+`CreateNewActivitiesAsync`経路をawait可能にしたもの。プロパティsetter自体はWPF互換のため温存)
  - `Task DeleteCardAsync(ObjectId cardId)` — 既存 `Model.Card.DeleteFromDB()` の**await可能版** `Task DeleteFromDBAsync()` をModel.Cardに追加(カード削除+`ActivityRepository.DeleteActivitiesOf`。既存同期版は温存)し、削除後に移動元列のSeqNoを再圧縮
  - 列範囲外move(**Review Focus 3**): `req.ProcessStepSeqNo` がボードの`ProcessSteps`の`PhaseSeqNo`集合にない/負 → `ArgumentOutOfRangeException` → APIは400

- [ ] **Step 1: 失敗するサービステストを書く**(`CardCommandServiceTest` — 下記がこの計画で最重要のテスト群)

```csharp
[TestMethod] public async Task MoveCard_Forward_TwoSteps_SynthesizesGapFreeActivities() {
  // (0,true)のカードを (2,false) へ。Activitiesは
  // (0,true)->(1,true),(1,false),(2,true)[,(2,false)] の順で StateChangedDate が5ms刻みで単調増加
  var acts = await TestDatabase.GetActivities(cardId);
  CollectionAssert.AreEqual(
    new[]{ "(0,W)", "(1,W)", "(1,D)", "(2,W)" },
    acts.Select(a => $"{a.WorkState.ProcessStepSeqNo},{(a.WorkState.IsWIP?"W":"D")}").ToArray());
}
[TestMethod] public async Task MoveCard_Backward_SingleActivity() { /* (2,false)->(0,true): 追加Activityは1件だけ */ }
[TestMethod] public async Task ReorderWithinSameColumn_UpdatesSeqNoOnly() { /* Activity件数変化なし・SeqNoは0..N-1 */ }
[TestMethod] public async Task MoveCard_InvalidStep_ThrowsOutOfRange() { /* Review Focus 3 */ }
[TestMethod] public async Task ConcurrentMoves_LeaveColumnCompacted() { /* Review Focus 6 */ }
[TestMethod] public async Task SetBlocked_EmitsActivityWithBlockedSnapshot() { /* WorkState.IsBlocked=true のActivityが増える */ }
[TestMethod] public async Task DeleteCard_RemovesCardAndItsActivities() { /* DBCards.Delete側とActivityRepository側の両方で消えている */ }
```

- [ ] **Step 2: 実行して失敗を確認**
- [ ] **Step 3: CardCommandService 実装**。既存の`WIPDoneViewModel.OnCollectionChanged`(`UpdateCardSeqNo`/`AddActivityToCard`、WIPDoneViewModel.cs:181-222)が移植元。**同一列判定は `Model.WorkState.Equals`(ProcessStepSeqNo+IsWIPのみ)を使うこと**(既存と同じ)
- [ ] **Step 4: サービステスト・エンドポイントテスト(POST→PATCH move→PATCH edit→DELETEの往復+400/404)でグリーン確認**
- [ ] **Step 5: WPF回帰確認** `dotnet build Kanban.slnx` + 既存TestKanban実行(グリーン維持)
- [ ] **Step 6: コミット** `git commit -am "feat: card write endpoints with activity-audit and seqno invariants"`

> **Note(async void→同期待ち変更に伴うWPF挙動)**: `CreateNewActivities`は元々UIスレッドでfire-and-forgetだった。同期待ちに変更するとUIの体感がわずかに遅くなる(1回あたり数十ms)。受け入れられるか判断に迷う場合は、既存`async void`を `{ _ = CreateNewActivitiesAsync(ws); }` の火-and-forget維持に戻し、API側は必ず`CreateNewActivitiesAsync`をawaitする(既存呼び出し側のWPF挙動は現状と完全一致)。この判断は実行担当者に委ねる — どちらでもテストは通る。

---

### Task 7: メトリクス5エンドポイント + CardsActivitiesCache のインスタンス化

既存 `BoardHistoryBuilder → BoardHistory → CycleTimeMetrics/ThroughputMetrics` の計算フローをそのままサーバーで実行し、EChartsが食える数値系列にする。**静的 `CardsActivitiesCache` は並行リクエストで汚染するため、計算単位で new するインスタンス版に置き換える(設計書リスク表の「放置は不可」項目)。**

**Files:**
- Create: `Kanban.Domain/Services/CardsActivitiesCacheInstance.cs`(既存静的クラスを元にしたインスタンス版 + `ICardsActivitiesCache` インターフェース)
- Modify: `Kanban.Domain/Model/CardsActivitiesCache.cs`(静的クラスは残すが、内部を `public static ICardsActivitiesCache Current` の差し替え可能なセッター経由の間接参照に変更 — **既存Model/静的利用箇所(BoardHistory.cs:251、CycleTimeMetrics.cs:57、CardMetrics.cs:18-20、ThroughputMetrics.cs:45-48)のコードは変更不要**。`MetricsQueryService` は計算前に `CardsActivitiesCache.Current = new CardsActivitiesCacheInstance()` をselfにバインドし、`GetBoardHistory()` 後に自身のインスタンスから読む。並行対策は **`MetricsQueryService` に `SemaphoreSlim(1,1)` を1つ置き計算全体を直列化**(データ量は小規模と確認済み・設計書§6)
- Create: `Kanban.Domain/Services/MetricsQueryService.cs`
- Create: `Kanban.Api/Contracts/MetricsDtos.cs`、`Endpoints/MetricsEndpoints.cs`
- Modify: `ServiceCollectionExtensions.cs`(singleton登録)、`Program.cs`
- Test: `Kanban.Api.Tests/Services/MetricsQueryServiceTest.cs`、`Endpoints/MetricsEndpointTest.cs`

**Interfaces:**
- Consumes: `BoardHistoryBuilder(Board, DateTime from, DateTime to, IBoardRepository, ICardRepository, IActivityRepository)`(既存)、`BoardHistory` のProcessHistory群(`ProcessHistory.CardCount/CumulativeFlowCardCount/WIPInfo` 系)、`ThroughputPerWeek()`、`Velocity`。**`BoardHistory`/`ProcessHistory` の公開メンバー名は実物(`Kanban.Domain/Model/BoardHistory.cs`)から確認して使うこと — この計画のDTOマッピング名は仮**(既存メンバーの名前・数式は変更禁止)。既定ウィンドウ = 今日23:59:59まで/8週間前(WPF `FromDate4Metrics` の実値を写す)
- Produces — `public class MetricsQueryService(IBoardRepository, ICardRepository, IActivityRepository)`, 5メソッド各 `Task<XxxDto> Get…(ObjectId boardId)`:
  - `GET /api/boards/{id}/metrics/cfd` → `CfdDto { List<string> Days; List<CfdSeriesDto> Series; CfdSummaryDto Summary; }`、`CfdSeriesDto { string StepName; int[] CumulativeFlowCounts; }`(`ProcessHistory.CumulativeFlowCardCount` を日付順)、Summary = `{ TotalDays, MaxWip, AverageWip, MaxWipDate, WorkingDays4MaxWip }`
  - `GET …/metrics/cycle-time` → `CycleTimeDto { List<string> WeekEnds; List<CycleTimeBucketDto> Buckets; double[] AverageCycleTimeDays; }`、`CycleTimeBucketDto { string StepName; int Percentage; }`(既存`CycleTimePercentage4Step.Percentage`)
  - `GET …/metrics/throughput` → `ThroughputDto { List<string> WeekEnds; int[] ItemCounts; WorkPerformanceCounts[] Performance; ThroughputSummaryDto Summary; }`(`CardCompletionPerformance.ItemCount` と Ahead/OnTime/Delayed 内訳、Summary = `RealAverageThroughputPerWeek`, 推定サイクル=`CardCountInWIP/avg`)
  - `GET …/metrics/bugs-blocked` → `BugsBlockedDto { List<string> Days; int[] BugFixCounts; int[] BlockedCounts; }`(既存`VisualBugFixAndBlockedBuilder`と同じ2系列)
  - `GET …/metrics/velocity` → `VelocityDto { List<string> WeekEnds; int[] StoryPoints; bool[] HasUnknownStoryPoints; double AverageVelocity4Every2Weeks; }`
  - 空ボード/活動ゼロ(**Review Focus 5**):全系列空またはゼロ・件数0・例外なし
  - 不正boardId→400、未在→404

- [ ] **Step 1: 失敗するサービステストを書く**。データ投入は Task 6 の `CardCommandService` を使い「(1,W)→(1,D)→(3,W)→(3,D)まで進んだカードを今週中に活動付きで作る」フィクスチャ(メソッド化して `TestDatabase.SeedDoneCard(...)` へ)から。CFDの特定日の`CumulativeFlowCounts`、ベロシティ週次合計(StoryPoints 5のカードを1枚リリース→対象週=5)、bugs-blocked(blocked活動のあるカード→BlockedCounts≥1)を各1アサート+空ボード全5エンドポイントのゼロ応答
- [ ] **Step 2: 実行して失敗を確認**
- [ ] **Step 3: `ICardsActivitiesCache`/`CardsActivitiesCacheInstance`/静的間接化/MetricsQueryService/DTO/エンドポイント実装**。**計算は既存Modelクラスをそのまま new して使う — メトリクス数式の再実装禁止**(既存`Visual*Builder`は描画のみで移植しない。数値の出し方は`VisualCFDBuilder`等が読んでいる`BoardHistory`のプロパティをそのままDTOへ)
- [ ] **Step 4: グリーン確認 + `dotnet test Kanban.slnx` 全体グリーン**
- [ ] **Step 5: コミット** `git commit -am "feat: five metrics endpoints over existing BoardHistory pipeline"`

---

### Task 8: 変更通知の口 + OpenAPI→TSクライアント生成 + 最終検証

**Files:**
- Create: `Kanban.Domain/Services/IBoardChangeNotifier.cs`(インターフェースのみ)
- Create: `Kanban.Api/NoOpBoardChangeNotifier.cs`
- Modify: `Kanban.Api/Endpoints/BoardsEndpoints.cs`、`CardsEndpoints.cs`(すべての書き込みハンドラの成功経路末で `notifier.BoardChanged(boardId)`)、`ServiceCollectionExtensions.cs`
- Create: `frontend/openapi/README.md` + `package.json` スクリプトの種火(本物のSPAは次計画。ここでは `openapi-typescript` で型生成できることまでを確認)
- Test: `Kanban.Api.Tests/Endpoints/ChangeNotifierTest.cs`

**Interfaces:**
- Consumes: Task 3-7 の全エンドポイント
- Produces: `public interface IBoardChangeNotifier { void BoardChanged(ObjectId boardId); }` + `NoOpBoardChangeNotifier`。DIはsingleton。テストでは差し替え可能(`TestNotifier`が呼ばれたboardIdを記録)— 将来のSignalR実装はこの差し替えのみで済むこと(設計書§4.4)

- [ ] **Step 1: 失敗するテストを書く**: `POST /api/cards` 成功後 `TestNotifier.BoardChanged` がそのボードIDで1回呼ばれる / GETでは呼ばれない
- [ ] **Step 2: 失敗確認 → 実装 → グリーン**
- [ ] **Step 3: OpenAPIからTS型生成の実演**: `npx openapi-typescript http://localhost:5093/openapi/v1.json -o frontend/openapi/schema.d.ts`(API起動した状態で)。生成物に `BoardDto`/`CardDto`/`CfdDto` が含まれることを確認してコミット(この生成物はフロント計画までの参照用)
- [ ] **Step 4: ダブルクライアント互換の最終回帰テスト**(`TestDatabase`経由、**設計書リスク表「WPF版現役共存」の検証**): APIでカード作成→move→blocked→削除したボードを、**Model層(WPFと同じ`CardRepository.GetCards`/`BoardHistoryBuilder`)で読み直して** SeqNo 0..N-1 連続・Activities_gap-free・メトリクス例外なし をアサートする `DualClientCompatTest` を追加しグリーン
- [ ] **Step 5: 全体実行**: `dotnet build Kanban.slnx`(0 error)→ `dotnet test Kanban.slnx`(全グリーン、環境依存Ignoreを除く)→ `dotnet run --project Kanban` でWPFが起動しボード一覧が表示できること(手動確認、起動を確認できたら閉じる)
- [ ] **Step 6: コミット** `git commit -am "feat: board-change notifier seam, openapi typegen, dual-client regression"`

---

## 完了基準(この計画のDefinition of Done)

- `dotnet test Kanban.slnx` がグリーン(環境依存の2テストは`[Ignore]`明示可)
- `GET http://<dev-host>:5093/api/boards` がブラウザから叩け、既存MongoDB(`kanban` DB)の実データが返る
- WPFアプリが従来どおり起動・全機能動作(移行期間要件)
- OpenAPI JSONからTS型が生成でき、次計画(フロントエンドSPA)がAPI実装を読まずに契約だけで着手できる
