# Kanban アプリケーション - アーキテクチャドキュメント

> **注意**: このドキュメントは 2026-11-17 時点の実装（WPF + ASP.NET Core API + React SPA のマルチプロジェクト構成）に合わせて改定済み。

## 1. プロジェクト概要

リポジトリは**単一の共有ドメイン（`Kanban.Domain`）の上に 2 つのフロントエンド**を載せる構成。両者は同じ MongoDB を使う。

| 項目 | 詳細 |
|------|------|
| **アーキテクチャ** | マルチプロジェクトソリューション（`Kanban.slnx`）: WPF デスクトップ + ASP.NET Core REST API + React SPA |
| **共有ドメイン** | `Kanban.Domain`（`net10.0` クラスライブラリ）- Model / Repository / Infrastructure / Services / Util。WPF と API の両方から参照 |
| **デスクトップ UI** | WPF (`net10.0-windows`)、XAML ビュー、MVVM。出力 WinExe |
| **Web API** | ASP.NET Core Minimal API (`net10.0`)、`Microsoft.AspNetCore.OpenApi`。SPA の静的配信（`wwwroot`）も兼任 → 単一プロセスで Web 全体をホスト |
| **Web フロント** | React 19 + TypeScript + Vite SPA（`frontend/app`、MUI・TanStack Query・@dnd-kit・ECharts・i18next） |
| **ソリューションファイル** | `Kanban.slnx`（新規ソリューション形式: `Kanban` / `Kanban.Domain` / `Kanban.Api` / `Kanban.Api.Tests` / `TestKanban`） |
| **認証** | なし（単一ユーザー、資格情報なしで MongoDB へ直接接続） |
| **著作権** | FST Co., Ltd. (2016-2026)、バージョン 1.0.0.0 |

## 2. プロジェクト構造

リポジトリルート（`D:\Kanban\`）には 5 つの .NET プロジェクト + フロントエンド 2 群が入る。ドメイン/DB/インフラ層はかつて `Kanban/src/` 配下にあったが、**`Kanban.Domain` に抽出**され WPF と API で共有されている。

```
D:\Kanban\
├── Kanban.slnx                    # ソリューション（新規 .slnx 形式）
├── Kanban.Domain\                 # ★共有ドメイン（net10.0 クラスライブラリ）
│   ├── Model\                     # ドメインエンティティ（Board/Card/Activity/…メトリクス）
│   ├── Repository\                # MongoDB リポジトリ実装（DBClient/DBBoards/DBCards/…）
│   ├── Infrastructure\            # 上位リポジトリ（BoardRepository/CardRepository/…Factory）
│   ├── Services\                  # API 用のアプリサービス層
│   │   ├── BoardCommandService.cs / CardCommandService.cs
│   │   ├── MetricsQueryService.cs
│   │   ├── IBoardChangeNotifier.cs     # 変更通知の口（既定 NoOp、SignalR 差し替え可）
│   │   ├── ILocalizationFactory.cs     # JsonLocalizationFactory（組込み locales）
│   │   └── CardsActivitiesCacheInstance.cs
│   ├── Util\                      # ConfigReader + ConfigOverrides、DateTimeUtil、DomainUtil
│   └── Resources\locales\*.json   # 組込みローカライズ（EN/JA/ZH、WPF resx から生成）
├── Kanban\                        # WPF デスクトップ（net10.0-windows）→ §3〜§17 は主にここ
│   ├── Kanban.csproj              # ProjectReference → Kanban.Domain
│   ├── App.config                 # DB_HOST/DB_NAME ほかアプリ設定（ConfigReader が読む）
│   ├── Architecture.md            # 本書
│   ├── fonts/ images/ ReleaseNote/ Board.ico
│   └── src\
│       ├── App.xaml(.cs)          # エントリポイント（§3）
│       ├── Adapter\               # WPF 専用インフラのみ残存
│       │   ├── AppSettings.cs     # IAppSettings 実装（ApplicationSettingsBase）
│       │   ├── DBBackup.cs        # mongodump/mongorestore（§14）
│       │   └── ProcessExecutor*.cs / ProcessDecorator.cs / HtmlConverter\
│       ├── Model\                 # 残りは WPF 専用インターフェースのみ
│       │   ├── IDBBackup.cs
│       │   └── IProcessExecutorClient.cs
│       ├── ViewModel\             # MVVM ViewModels + VisualMetrics\（WPF Canvas 描画）
│       ├── View\                  # XAML ビュー + DragAndDropLib\ + DependencyInjector.cs + Localization.cs
│       │   └── Properties\Resources*.resx  # EN/JA/ZH（SPA locales の生成元）
│       └── Util\                  # WPF 専用: Util.cs(IAppSettings 参照)/EnumUtil/CircleNumImages/HandWriteFontFamily
├── Kanban.Api\                    # ★ASP.NET Core Minimal API（net10.0）→ §18
│   ├── Kanban.Api.csproj          # ProjectReference → Kanban.Domain、Microsoft.AspNetCore.OpenApi
│   ├── Program.cs                 # 起動・静的配信・SPA フォールバック
│   ├── ServiceCollectionExtensions.cs  # AddKanbanServices（MS.DI 登録 + KanbanApiOptions）
│   ├── ServerLocalization.cs / NoOpBoardChangeNotifier.cs
│   ├── Contracts\                 # BoardDto/CardDto/ProcessStepDto/MetricsDtos
│   ├── Endpoints\                 # BoardsEndpoints / CardsEndpoints / MetricsEndpoints
│   ├── appsettings.json           # Kanban:DB_HOST / DB_NAME ほか
│   └── wwwroot\                   # vite build 出力（gitignore、§19）
├── Kanban.Api.Tests\              # API 統合テスト（WebApplicationFactory + MSTest）
├── TestKanban\                    # ドメイン/WPF テスト（net10.0-windows + MSTest）
└── frontend\
    ├── app\                       # React SPA（Vite/TS/Vitest）→ §19
    │   └── src\ (api/ charts/ components/ i18n/ locales/ pages/ util/)
    └── openapi\                   # OpenAPI→TS 型生成（openapi-typescript）
```

## 3. エントリポイント & 起動フロー

**ファイル**: `src/App.xaml.cs`

エントリポイントは `App` クラスの静的 `Main()` メソッドです:

```csharp
[STAThread]
public static void Main()
{
    try {
        ShowSplashScreen();                    // ランダムスプラッシュスクリーン（6 種類）
        Kanban.App app = new Kanban.App();
        app.InitializeComponent();
        app.RegisterDependencies();            // Unity DI コンテナ設定
        app.ConfigLocalizations();             // OS カルチャから言語検出
        app.Run();
    } catch (Exception ex) {
        MessageBox.Show($"Error:  {ex.Message}\r\n\r\n{ex.StackTrace}", ...);
    }
}
```

**起動シーケンス**:

1. ランダムスプラッシュスクリーンを表示（`images/kanban_splash_window*.jpg` の6種類から）
2. 隠し「ダミー」ウィンドウを作成（WPF バグ回避策：実際のウィンドウが開かれる前に MessageBox を表示するため）
3. スキーママイグレーションを処理する `DBUpgrader.UpgradeDB()` を実行
4. DI コンテナ経由で `MainWindow` を解決して表示
5. ローカライズ設定（OS カルチャからの言語検出: ja-JP、zh-CN、または英語デフォルト）

**WPF バグ回避策**: WPF では、少なくとも1つのウィンドウが開かれるまで MessageBox を表示できません。起動時に隠し 1x1 ダミウィンドウを作成し、`MainWindow` が正常に表示された後に閉じます。

### Web API エントリポイント（`Kanban.Api/Program.cs`）

WPF とは別に、API は独立的に起動する（`dotnet run --project Kanban.Api`）。トップレベルステートメント:

1. `builder.Services.AddKanbanServices(config)`（`ServiceCollectionExtensions.cs`）で MS.DI に共有ドメインを登録
2. `builder.Services.AddOpenApi()` + `app.MapOpenApi()` で OpenAPI 仕様を配信
3. 起動時に `IDBActivity.EnsureIndexesAsync()` と `DBUpgrader.UpgradeDB()` を **await**（旧 WPF の async-void 投げっぱなしを修正、§7・§6）
4. 静的プロパティ結線: `Board.BoardRepository` / `Card.CardRepository` / `Activity.ActivityRepository` / `DevProcess.Localization`（既存 WPF の `DI4Model` と同じ内容・起動時1回）
5. 静的配信（`UseDefaultFiles` + `UseStaticFiles`）— `index.html` は `no-store`、ハッシュ済み assets は cache
6. `/api/health` と `MapBoardsEndpoints()` / `MapCardsEndpoints()` / `MapMetricsEndpoints()`
7. `MapFallback` — `/api/*` 未一致は 404 JSON 意味論を維持、それ以外は `wwwroot/index.html` を `no-store` で返す（SPA ディープリンク対応）

## 4. アーキテクチャパターン

### MVVM (Model-View-ViewModel) + リポジトリパターン

| レイヤー | ネームスペース / ディレクトリ | プロジェクト | 目的 |
|---------|---------------------------|--------------|------|
| **View (WPF)** | `Kanban.View.*` + XAML ファイル | Kanban | WPF UI、コンバーター、ドラッグ&ドロップライブラリ |
| **ViewModel** | `Kanban.ViewModel.*` | Kanban | INotifyPropertyChanged、DelegateCommand を持つ MVVM ViewModels |
| **Model (ドメイン)** | `Kanban.Model.*` | Kanban.Domain | ドメインエンティティとビジネスロジック（WPF/API 共有） |
| **Repository/Adapter** | `Kanban.Repository`, `Kanban.Infrastructure` | Kanban.Domain | MongoDB データアクセス層（WPF/API 共有） |
| **Services** | `Kanban.Services.*` | Kanban.Domain | API 用の command/query サービス層 + 変更通知・ローカライズ口 |
| **API (Minimal)** | `Kanban.Api.Endpoints.*` / `Contracts` | Kanban.Api | REST エンドポイントと DTO |
| **Web フロント** | `frontend/app/src/*` | (npm) | React SPA（MVVM 的なページ/クエリ + コンポーネント） |

### 追加で採用されているパターン:

- **Event Aggregator** (`src/Util/EventAggregator.cs`) - 型付きイベントパブリッシャー/サブスクライバーによる非同期コンポーネント間通信（シングルトン per イベントタイプ）
- **ファクトリパターン** - Board および Card の作成用（`BoardFactory`, `CardFactory`）
- **デコレータパターン** - プロセス実行用（`ProcessDecorator` は `System.Diagnostics.Process` を拡張、`ProcessExecuteCompositeCommand`）
- **ビルダーパターン** - メトリクス可視化用（`VisualCFDBuilder`, `VisualTeamVelocityBuilder` など）

## 5. データモデル / エンティティ

### ドメインモデル（`Kanban.Domain/Model`、名前空間 `Kanban.Model`）

| クラス | 目的 |
|--------|------|
| `Board` | DevProcess、プロセスステップ、説明、開発者を持つ Kanban ボード |
| `Card` | タイトル、タグ、実行担当、概要、テストケース、設計概要、ストーリーポイント、カード種別、作業状態、リリース日、ブロック/マージフラグを持つワークアイテム |
| `Activity` | カードの状態遷移履歴（プロセスステップ間の移動を追跡） |
| `KanbanItem` | `_id`, `SeqNo`, `Title` を持つ抽象基底クラス |
| `WorkState` | カードの現在状態: ProcessStepSeqNo, IsWIP, IsBlocked, IsMergedIntoMaster, IsMergedIntoMajorBranch |
| `DevProcess` | サイクル時間追跡付きプロセスステップコレクション |
| `BoardHistory` | メトリクス用履歴ボードデータ（プロセス別日次カード数、累積フロー） |
| `CycleTimeMetrics` | プロセス間サイクル時間の分析 |
| `ThroughputMetrics` | スループット/ベロシティの計算 |

### リポジトリモデル（`Kanban.Domain/Repository`、名前空間 `Kanban.Repository`、MongoDB シリアライズ用）

| クラス | 目的 |
|--------|------|
| `Board` (Repo) | `[BsonIgnoreExtraElements]` - Description, Developers, ProcessSteps リスト付き Board |
| `Card` (Repo) | `[BsonIgnoreExtraElements]` - WorkState、日付、フラグを含むすべてのフィールド付き Card |
| `Activity` (Repo) | BoardID, CardID, WorkState, StateChangedDate 付き Activity |
| `KanbanItem` (基底) | `[BsonId] ObjectId _id`, SeqNo, Title |
| `WorkState` (Repo) | ProcessStepSeqNo, IsWIP, IsBlocked, IsMergedIntoMaster, IsMergedIntoMajorBranch |
| `ProcessStep` | PhaseSeqNo, LabelColor, Name, WIPTitle, DoneTitle, IsStartOfCycleTime, IsEndOfCycleTime |

### 列挙型

| 列挙型 | 値 |
|--------|-----|
| `CardType` | Feature, Improvement, BugFix, DefectInvestigation, TechnicalDebt, Cancelled, Evaluation, TestDesign, Task |
| `EstimatedWorkEffort` | Small (<=2日), Medium (<=1週間), Large (<=2週間), LargeExtra (>2週間), Unknown |
| `Languages` | Japanese, Chinese, English |
| `MetricChartType` | CumulativeFlowDiagram, Throughput, CycleTimeDistribution, BugsAndBlocked, TeamVelocity |
| `WorkPerformance` | AheadOfSchedule, OnTime, Delayed |

## 6. データベース層 - MongoDB（EF Core ではない）

**Entity Framework Core は使用していません。** **MongoDB.Driver** NuGet パッケージを直接使用しています（バージョン 3.8.1、`Kanban.Domain` が参照）。DB 層は WPF 専用ではなく **`Kanban.Domain` に抽出され WPF と API で共有**されています。

### 接続設定（フロントエンドごとに別ソース）

**WPF** は `Kanban/App.config` の appSettings を `ConfigReader` が読む:

```xml
<add key="DB_HOST" value="192.168.21.150:27017" />
<add key="DB_NAME" value="kanban" />
<add key="CumulateCardsOnBackBoard" value="true" />
<add key="CanDropDatabase" value="true" />
<add key="LimitNum4QueringLastProcess" value="20" />
<add key="DB_BACKUP_TOOL_PATH" value="C:\Program Files\MongoDB\Server\8.3\bin" />
<add key="CardWidth" value="260" />
```

**API** は `Kanban.Api/appsettings.json` の `Kanban` セクション（`DB_HOST`/`DB_NAME` はアンダースコア表記なので名前の一致に頼らず `ServiceCollectionExtensions` で手動バインド）。`CumulateCardsOnBackBoard` / `LimitNum4QueringLastProcess` は `ConfigOverrides.Values` に書き込み、`ConfigReader` 経由で既存ドメインコードに反映する（呼び出し側は無変更。WPF は `Values` 空＝ConfigReader が App.config を読む）。

```json
"Kanban": { "DB_HOST": "192.168.21.150:27017", "DB_NAME": "kanban",
            "CumulateCardsOnBackBoard": true, "LimitNum4QueringLastProcess": 20 }
```

### MongoDB クライアント（`Kanban.Domain/Repository/DBClient.cs`）

- `"mongodb://" + DBHost` 接続文字列で `MongoClient` を作成
- `IMongoDatabase DB` プロパティを提供
- データベース名の解決: 設定名が新規（未作成）なら設定名、既存ならユーザー設定を優先
- **コンストラクタ系が 3 系統**:
  - WPF 用 `DBClient(string userDatabase)` — `ConfigReader` が App.config から DB_HOST/DB_NAME を解決し、起動時に `UpgradeCollections()`
  - API/テスト用 `DBClient(string dbHost, string dbName)` — 明示接続（`ConfigReader` を通さない。`AddKanbanServices` が options を渡す）
  - テストのみ `DBClient()`（無設定）
- WPF は `DependencyInjector` の `RegisterFactory` で `IAppSettings.Database` を渡して構築（§7）

### コレクション名（`CollectionNames.cs`）

| コレクション | 目的 |
|-------------|------|
| `"Boards"` | ボードドキュメント |
| `"Cards"` | カードドキュメント |
| `"Activities"` | アクティビティ/監査証跡ドキュメント |
| `"Version"` | スキーマバージョン管理 |

### リポジトリインターフェースと実装（`Kanban.Domain/Repository/`）

| インターフェース | 実装 | 場所 |
|-----------------|------|------|
| `IDBClient` | `DBClient` | Kanban.Domain/Repository/ |
| `IDBBoard` | `DBBoards` | Kanban.Domain/Repository/ |
| `IDBCard` | `DBCards` | Kanban.Domain/Repository/ |
| `IDBActivity` | `DBActivities` | Kanban.Domain/Repository/ |

### ドメインリポジトリインターフェースと実装（`Kanban.Domain/`、名前空間 `Kanban.Infrastructure`）

| インターフェース | 実装 | ネームスペース / ディレクトリ |
|-----------------|------|------------------------------|
| `IBoardRepository` | `BoardRepository` | Kanban.Infrastructure / `Kanban.Domain/Infrastructure/` |
| `ICardRepository` | `CardRepository` | Kanban.Infrastructure / `Kanban.Domain/Infrastructure/` |
| `IActivityRepository` | `ActivityRepository` | Kanban.Infrastructure / `Kanban.Domain/Infrastructure/` |

### `DBCards.cs` の主要 MongoDB 操作

- WorkState、カードフィルターテキスト（タイトル/担当者/タグ）、リリース日によるカードのフィルタリング
- カラム内のカード順序付け用のシーケンシャル番号管理
- 最終プロセスステップでのクエリ制限による WIP リメント強制
- 180日カットオフ付きバックボード（アーカイブ済みカード）の取得
- Where フィルター: 検索テキストをスペース、全角スペース、カンマ、日本語カンマで分割；数値パターンからリリース日も抽出

### スキーママイグレーション（`DBUpgrader`）

`Version` コレクションに保存されたバージョンアップグレードを処理します:

| バージョン | マイグレーション |
|-----------|-----------------|
| v1.0 → v1.1 | 旧 `EstimatedWorkEffort` フィールドを新しい `StoryPoints` 整数に変換（Small→2, Medium→5, Large→8, LargeExtra→13） |
| v1.1 → v1.2 | Card コレクションから非推奨の `EstimatedWorkEffort` フィールドを削除 |

## 7. DI 登録パターン

フロントエンドごとに DI コンテナが異なる。**共有ドメインの型は同じ**だが結線方法が違う。

### WPF: Unity コンテナ

**DI コンテナ**: Unity（バージョン 5.11.10）。

**ファイル**: `Kanban/src/View/DependencyInjector.cs`

登録は5つのスコープグループに分割されています:

```
DependencyInjector
├── DI4Repository     → IDBClient/DBClient は RegisterFactory（IAppSettings.Database を渡して構築）,
│                        IDBBoard/DBBoards, IDBCard/DBCards, IDBActivity/DBActivities（ContainerControlled = シングルトン）
├── DI4Infrastructure → IAppSettings/AppSettings, ILocalization/Localization, IProcessExecutor/ProcessExecutor, IBoardRepository/BoardRepository, ICardRepository/CardRepository, IBoardFactory/BoardFactory, ICardFactory/CardFactory, DBBackupFactory
├── DI4Model          → 静的プロパティインジェクション: Board.BoardRepository, Card.CardRepository, Activity.ActivityRepository（シングルトン静的参照）
├── DI4ViewModel      → CardFactory デリゲート, WIPDoneViewModelFactory デリゲート, ProcessStepViewModelFactory デリゲート
└── DI4View           → MainWindow (ContainerControlled), IViewModelProperties インスタンス, BoardPageFactory デリゲート
```

### Web API: Microsoft.Extensions.DependencyInjection

**ファイル**: `Kanban.Api/ServiceCollectionExtensions.cs`（`AddKanbanServices`）

同じ共有ドメイン型を **MS.DI のシングルトン**で結線する。Unity は使わない。

```
AddKanbanServices(config)
├── KanbanApiOptions（appsettings Kanban セクションを手動バインド）+ ConfigOverrides へ 2 値書込み
├── IDBClient → new DBClient(DBHost, DBName)（明示接続・ConfigReader 経由しない）
├── IDBActivity/IDBCard/IDBBoard/DBVersion/DBUpgrader（明示コンストラクタ結線）
├── ILocalization → ServerLocalization（EN 固定。req ごとの言語は BoardCommandService が明示引数で受ける）
├── IBoardFactory/ICardFactory/IBoardRepository/ICardRepository/IActivityRepository
├── Services → ILocalizationFactory(JsonLocalizationFactory), BoardCommandService, CardCommandService, MetricsQueryService
└── IBoardChangeNotifier → NoOpBoardChangeNotifier（既定。SignalR 実装はこの差し替えだけで導入可）
```

起動時に `Program.cs` が静的プロパティ結線（`Board.BoardRepository` ほか）を行う点は WPF と同じ。

**主要パターン**: ドメインモデル（`Model.Board`, `Model.Card`, `Model.Activity`）は起動時に設定される**静的**リポジトリ参照を保持しています。これはアンチパターンですが、デスクトップアプリ／プロセス単位の API では機能します。

## 8. フロントエンドコンポーネント（WPF: XAML Views）

> Web フロント（React SPA）は §19 を参照。

| XAML ファイル | タイプ | 目的 |
|--------------|--------|------|
| `App.xaml` | アプリケーションルート | リソース、イベントハンドラー |
| `MainWindow.xaml` | ウィンドウ | Frame ナビゲーション付きメインシェル |
| `BoardListPage.xaml` | ページ | ボードリストサイドバー + メトリクスチャートキャンバス |
| `BoardPage.xaml` | ページ | Kanban ボードビュー（各プロセスステップの列） |
| `DrawingCanvas4Metrics.xaml` | カスタムコントロール | WPF Canvas ベースのメトリクス可視化 |
| `EditCardDialog.xaml` | ダイアログ | カード編集ポップアップ |
| `DropDBDialog.xaml` | ダイアログ | データベース削除/復元ダイアログ |
| `SystemSetting.xaml` | ページ | システム設定ページ（言語、DB 選択、バックアップ） |
| `AboutControlView.xaml` | コントロール | リリースノート付き著作権情報 |

### 主要 UI 機能:

- **カスタムドラッグ&ドロップライブラリ** (`DragAndDropLib`) - カードの列間移動用。`ItemsControlDragDropDecorator`, `DragAdorner`, `InsertAdorner`, ドラッグ中の自動スクロールを含む
- **カードデータテンプレート** - ストーリーポイント画像、作業日プログレスバー、マージ状態インジケーター付き
- **ズーム機能** - ボードビュー（ウィンドウサイズに基づく自動計算）
- **リアルタイムメトリクスチャート**: CFD, サイクル時間分布, スループット, チームベロシティ, WIP インジケーター
- **多言語サポート**: 英語、日本語 (ja-JP)、中国語 (zh-CN) - OS カルチャから検出
- **HTML-to-XAML コンバーター** - About ダイアログ内のリッチテキストカード説明用

### ビューナビゲーションフロー:

```
MainWindow (Frame)
├── BoardListPage（デフォルト）
│   ├── ボードリストサイドバー（ドラッグで並べ替え可能）
│   ├── プロセスステップ設定パネル
│   └── メトリクスチャートキャンバス（CFD, スループット, サイクル時間 など）
└── BoardPage（ボードのダブルクリックまたは GotoBoardPageCommand でナビゲーション）
    ├── ツールバー: カード追加、ズーム、更新、カードフィルターテキストボックス
    ├── プロセスステップ列（DevProcess.ProcessSteps から動的生成）
    │   └── 各列に WIP と Done のサブ列があり、ドラッグ&ドロップでカードを移動可能
    └── ボードのバック/フロント切替
```

## 9. メトリクスと分析

### サポートされるメトリクス種別（`MetricChartType` 列挙型）:

| チャート種別 | ビルダークラス | 説明 |
|------------|--------------|------|
| **累積フローダイアグラム (CFD)** | `VisualCFDBuilder`, `VisualCFDChart` | プロセスステップ別の WIP を塗りつぶし領域で表示 |
| **サイクル時間分布** | `VisualCycleTimeDistributionBuilder` | プロセスステップ別のサイクル時間%を示す積み上げ棒グラフ |
| **スループット** | `VisualThroughputBuilder`, `VisualThroughput` | 週次完了カードと推定パフォーマンス（AheadOfSchedule/OnTime/Delayed） |
| **バグ & ブロック** | `VisualBugFixAndBlockedBuilder`, `VisualBugFixAndBlocked` | バグ修正とブロックアイテムの時間軸エリアチャート |
| **チームベロシティ** | `VisualTeamVelocityBuilder`, `VisualTeamVelocity` | 週次完了ストーリーポイントの折れ線グラフ（2週間ローリング） |

### 可視化アーキテクチャ:

すべての可視化はカスタム WPF `DrawingVisual` 派生クラス（`AbstractVisualMetrics`）を使用し、共有 `Canvas` に描画されます:

```
AbstractVisualMetrics (基底, DrawingVisual)
├── VisualXYGridChart          - グリッドフレーム（軸とラベル付き）
├── VisualCFDChart             - CFD 用塗りつぶしエリアチャート
├── VisualThroughput           - 推定パフォーマンスオーバーレイ付き棒グラフ
├── VisualCycleTimeDistribution - 積み上げ棒グラフ
├── VisualTeamVelocity         - 折れ線グラフ
├── VisualBugFixAndBlocked     - エリアチャート（バグ + ブロック）
└── VisualWIPIndicator         - WIP カウントを示す縦線
```

### メトリクスデータフロー:

```
BoardHistoryBuilder（アクティビティから履歴を構築）
    → BoardHistory（プロセス別日次カード数、累積フロー）
        → ThroughputMetrics / CycleTimeMetrics（分析）
            → Visual* ビルダー（可視化）
```

`CardsActivitiesCache` 静的クラスはメトリクス計算中にカードとそのアクティビティをキャッシュし、冗長なデータベースクエリを回避します。

## 10. 設定ファイル

| ファイル | 目的 |
|---------|------|
| `App.config` | MongoDB ホスト、DB 名、機能フラグ（CumulateCardsOnBackBoard, CanDropDatabase, LimitNum4QueringLastProcess）、DB バックアップツールパス、カード幅 |
| `Kanban.csproj` | プロジェクト SDK、ターゲットフレームワーク、パッケージ参照、埋め込みリソース |
| `src/View/Properties/Settings.settings` | ユーザースコープ WPF 設定（ウィンドウサイズ、最大化状態、ズーム） |
| `src/View/Properties/Resources.resx` / `.ja-JP.resx` / `.zh-CN.resx` | EN/JP/ZH のローカライズ文字列 |

### アプリ設定（`IAppSettings` / `AppSettings`）

WPF ApplicationSettingsBase 経由で永続化されるユーザースコープ設定:
- `Language` - 現在の言語（OS カルチャから自動検出）
- `Database` - 選択された MongoDB データベース名
- `DBBackupFolder` - デフォルトのバックアップ先フォルダー
- `LastUsedMetricChartType` - 最後に選択したメトリクスチャート種別
- `LastUsedBoardID` - クイック再開用の最後に使用したボード ID
- `MainWindowSize` / `IsMainWindowStateMaximized` - ウィンドウ状態の永続化
- `IsBoardZoomed` - ボードズーム状態

## 11. 認証 / 認可

**認証も認可もありません。** WPF・Web ともにログイン/ロール/権限を持たない単一ユーザー向けで、REST API エンドポイントにも認可はありません。データベース接続は資格情報なしで構成された MongoDB ホストに直接接続します（`appsettings.json` / `App.config` の `DB_HOST`）。

## 12. 主要なアーキテクチャ観察点

- **マルチプロジェクトソリューション**（`Kanban.slnx`）: 共有ドメイン `Kanban.Domain` の上に WPF と ASP.NET Core API が乗り、API が React SPA も配信（単一プロセス Web）
- **共有ドメイン** `Kanban.Domain` が Model/Repository/Infrastructure/Services/Util を集約。WPF と API が同一のドメイン・DB 層を再利用（旧 `Kanban/src` の Adapter/Model/KanbanDB はここへ移動）
- **MongoDB ネイティブドライバー**（EF Core ではない）- ドキュメントは `[BsonId]` 属性付きの POCO
- **二重の DI 実装**: WPF は Unity（レガシー）、API は Microsoft.Extensions.DependencyInjection。共有ドメイン型を両方が別々に結線
- **REST API（Minimal API）** が新設 - ボード/カード/メトリクスの CRUD とクエリ。OpenAPI 仕様を配信し SPA は型生成クライアント（`openapi-fetch`）で接続
- **SPA の 1 プロセス同梱** - vite build を `Kanban.Api/wwwroot` へ出力、`index.html` は `no-store`（再デプロイ後のホワイトアウト対策）、ディープリンクは SPA フォールバック
- **設定の二系統** - WPF は `ConfigReader`→App.config、API は `KanbanApiOptions`→appsettings.json + `ConfigOverrides`（呼び出し側コード無変更で両対応）
- ドメインモデルへの**静的プロパティインジェクション**（`Board.BoardRepository = ...`）。コンストラクタインジェクションではない
- ViewModel と View の間の緩結合のための **Event Aggregator パターン**（WPF）
- リッチテキストカード説明用の **HTML-to-XAML コンバーター**を含む（Microsoft 由来コード、WPF）
- `mongodump`/`mongorestore` 経由で MongoDB バックアップ/復元コマンドを実行するための**プロセス実行システム**。コンポジット/デコレータパターン採用（WPF）

## 13. パッケージ依存関係

### .NET（プロジェクト別）

| プロジェクト | パッケージ | バージョン | 目的 |
|-------------|-----------|-----------|------|
| Kanban.Domain | `MongoDB.Driver` | 3.8.1 | NoSQL データベースドライバー |
| Kanban.Domain | `System.Configuration.ConfigurationManager` | 10.0.0 | WPF 同梱だった AppSettingsReader を Domain で使うため |
| Kanban (WPF) | `SharpCompress` | 0.48.0 | アーカイブ圧縮（DB バックアップ用） |
| Kanban (WPF) | `Unity` | 5.11.10 | 依存関係注入コンテナ |
| Kanban.Api | `Microsoft.AspNetCore.OpenApi` | 10.0.15 | OpenAPI 仕様配信 |

> 参照関係: `Kanban → Kanban.Domain`、`Kanban.Api → Kanban.Domain`。`MongoDB.Driver` は WPF からは直接参照せず Domain 経由。

### フロントエンド（`frontend/app/package.json`、主なもの）

| パッケージ | 目的 |
|-----------|------|
| `react` / `react-dom` (19) | UI |
| `react-router` (8) | ルーティング |
| `@mui/material` / `@mui/icons-material` (9) | UI コンポーネント |
| `@tanstack/react-query` (5) | サーバー状態（`['boards']` 等のクエリ） |
| `@dnd-kit/*` | ドラッグ&ドロップ |
| `echarts` (6) | メトリクスチャート |
| `i18next` / `react-i18next` | i18n（WPF resx から生成した `locales/*.json`） |
| `openapi-fetch` | 型付き API クライアント（`frontend/openapi/schema.d.ts`） |
| `vite` / `vitest` / `typescript` | ビルド・テスト |

## 14. プロセス実行システム

アプリケーションは外部 MongoDB ツール（`mongodump.exe`, `mongorestore.exe`）を使用してデータベースのバックアップと復元を行います:

```
DBBackup (IDBBackup インターフェース)
├── DumpDB() → mongodump --host <DB_HOST> --out <folder>
└── RestoreDB() → mongorestore --drop --host <DB_HOST> <folder>

ProcessExecutor (IProcessExecutor)
├── BackgroundWorker ベースの実行
├── ProcessDecorator（System.Diagnostics.Process を拡張）
└── ProcessExecuteCompositeCommand（順次コマンド実行用のデコレータパターン）
```

ツールパスの解決順序:
1. Windows 起動フォルダー
2. `App.config` から `DB_BACKUP_TOOL_PATH`
3. PATH 環境変数

## 15. カードライフサイクル

### カード作成フロー:
```
WIPDoneViewModel.AddNewCard()
    → CardFactory.CreateCard(boardID, workState, srcCard)
        → Cards コレクションに挿入
        → 初期 Activity レコードの作成
        → Model.Card を ViewModel.Card でラップして返す
```

### カード状態遷移（ドラッグ&ドロップ）:
```
ユーザーがカードを列間でドラッグ
    → ItemsControlDragDropDecorator がドラッグイベントを処理
    → Helper.AddItem() / Helper.RemoveItem() が ObservableCollection を更新
    → Card.CreateNewActivities(newWorkState) が Activity レコードを作成
    → CardRepository.Update() が状態変更を MongoDB に永続化
```

### ストーリーポイント & 推定作業効率:
- 有効なストーリーポイント: `{0, 1, 2, 3, 5, 8, 13, 20, 40, 100, 999(不明)}`
- `EstimatedWorkEffort` へのマッピング: Small (0/1/2), Medium (3/5), Large (8/13), LargeExtra (20/40/100), Unknown (999)
- 作業パフォーマンス: 実働日数と閾値の比較に基づき AheadOfSchedule / OnTime / Delayed

## 16. DevProcess（Kanban ボード設定）

新規ボード作成時のデフォルトプロセスステップ:

| Seq | デフォルト名（英語） | サイクル時間マーカー |
|-----|-------------------|-------------------|
| 0 | Backlog | - |
| 1 | Requirement | IsStartOfCycleTime = true |
| 2 | Development | - |
| 3 | Test | IsEndOfCycleTime = true |
| 4 | Release | -（最終ステップ） |

ユーザーはプロセスステップの動的な追加/削除が可能です。`ProcessViewModel` がボード全体のカード/アクティビティシーケンシャル番号の更新とともに並べ替えを処理します。

## 17. ローカライズ

3つの言語をサポート: ja-JP、zh-CN、英語（デフォルト）。

- OS インストールUIカルチャからの言語検出（`CultureInfo.InstalledUICulture`）
- リソースは言語別の `.resx` ファイルに保存
- プロセスステップのデフォルト名は `ILocalization` インターフェースでローカライズ
- すべての列挙型説明が `[Description]` 属性により多言語対応
- フォント選択は言語によって異なる（JP: MS Mincho、ZH: Kaiti、EN: Bradley Hand）

> SPA 側は WPF の `.resx` トリオから生成した `frontend/app/src/locales/*.json`（`npm run i18n:export`）を i18next で使う。サーバー側のデフォルト処理ステップ名のローカライズは `Kanban.Domain` の `JsonLocalizationFactory`（組込み `Resources/locales/*.json`）が担い、`ServerLocalization` は EN 固定（リクエスト単位の言語は `BoardCommandService` が明示引数で受ける）。

---

## 18. Web バックエンド - ASP.NET Core Minimal API（`Kanban.Api`）

WPF と同じ共有ドメイン（`Kanban.Domain`）を MS.DI で結線し、REST を提供する。コントローラーではなく Minimal API（`Map*` 拡張メソッド）。

### エンドポイント一覧

| メソッド | パス | 目的 |
|---------|------|------|
| GET | `/api/health` | ヘルスチェック `{status:"ok"}` |
| GET | `/api/boards` | ボード一覧 |
| POST | `/api/boards` | ボード作成（`uiLanguage` で既定ステップをローカライズ） |
| DELETE | `/api/boards/{id}` | ボード削除 |
| PUT | `/api/boards/{id}/process-steps` | 処理ステップ置換（`phaseSeqNo` は識別子＝並べ替えにも使う） |
| GET | `/api/boards/{boardId}/cards` | カード取得（フィルタ/バックボード） |
| POST | `/api/cards` | カード作成 |
| PATCH | `/api/cards/{id}` | カード更新 |
| PATCH | `/api/cards/{id}/move` | カード移動（列間／並び替え、SeqNo 不変条件は API 側遵守） |
| DELETE | `/api/cards/{id}` | カード削除 |
| GET | `/api/boards/{boardId}/metrics/{cfd,cycle-time,throughput,velocity,bugs-blocked}` | 5 種のメトリクス（既存 `BoardHistory` パイプライン） |

### 分層

- **Endpoints\**`*.cs` — 要求検証と DTO マッピングのみ。ビジネスロジックは呼ばない
- **Contracts\** — `BoardDto` / `CardDto` / `ProcessStepDto` / `MetricsDtos`
- **ServiceCollectionExtensions.cs** — `AddKanbanServices`（options 手動バインド + 共有ドメインの MS.DI 登録）
- **Kanban.Domain/Services\** — `BoardCommandService` / `CardCommandService` / `MetricsQueryService`（実際の書き込み/クエリ、Activity 監査、SeqNo 不変条件）
- **NoOpBoardChangeNotifier.cs** — `IBoardChangeNotifier` の既定実装。将来 SignalR 等に差し替え可能（設計 §4.4）
- **ServerLocalization.cs** — `ILocalization` のサーバー版（EN 固定）

### 静的配信と SPA フォールバック（`Program.cs`）

- `UseDefaultFiles` + `UseStaticFiles`。`index.html` は `Cache-Control: no-store`（ハッシュ済み assets は cache 可）— 再ビルド後に旧 index が消えた旧 JS を読んでホワイトアウトする実障害（2026-11-17）への対策
- `MapFallback`：`/api/*` 未一致は 404（JSON 意味論を維持）、それ以外は `wwwroot/index.html` を `no-store` で返す。`wwwroot` 未存在（gitignore のため CI/クローン）は 404 を返すよう `File.Exists` ガード
- OpenAPI 仕様は `/openapi/v1.json`（`frontend/openapi` の型生成が読む）

### 設定（`appsettings.json`）

`Kanban:DB_HOST` / `DB_NAME` / `CumulateCardsOnBackBoard` / `LimitNum4QueringLastProcess`。テストは `UseSetting` で `DB_NAME=kanban_test` に差し替え、本番 DB を触らない。

## 19. Web フロントエンド - React SPA（`frontend/app`）

Vite + TypeScript + React 19。`npm run build` が `Kanban.Api/wwwroot` へ出力（§18 の単一プロセスで配信）。開発時は `npm run dev`（Vite が `/api` を `localhost:5000` にプロキシ）。

### ディレクトリと責務

| 場所 | 責務 |
|------|------|
| `src/api/client.ts` / `types.ts` | `openapi-fetch` 型付きクライアント（`frontend/openapi/schema.d.ts` ベース） |
| `src/pages/` | `BoardListPage`（一覧＋作成/削除/プロセスステップ編集）, `BoardPage`（列/サブ列/ツールバー/ズーム/取得クエリ）, `MetricsPanel`（ECharts）, `SettingsPage`（言語切替）, `AboutPage`（リリースノート） |
| `src/pages/boardQueries.ts` / `dnd.ts` | TanStack Query のクエリ定義 / dnd-kit の移動ロジック |
| `src/components/` | `CardView`（SP/進捗バー/マージ/ブロック/種別色＋ホバーツールチップ）, `CardEditDialog`（全フィールド WPF パリティ）, `ProcessStepEditor` |
| `src/charts/options.ts` | ECharts オプション生成（メトリクス 5 種） |
| `src/i18n/` + `src/locales/*.json` | i18next 初期化（EN/JA/ZH、WPF resx から生成） |
| `src/util/format.ts` | resx フォーマット `{0}` 展開（`format0`） |
| `src/test/` | Vitest + Testing Library の render ハーネス・fetch スタブ |

### 主要パターン

- **状態管理**: サーバー状態は `@tanstack/react-query`（`['boards']` 等を invalidate）。作成/削除後は必ず invalidate して stale を防ぐ
- **ルーティング**: `react-router`。`/`(一覧) → `/boards/:boardId`(ボード) → `/settings` `/about`。各ページにボード一覧へ戻る矢印ボタン
- **レイアウト**: ボード画面はビューポート固定の flex 列（AppBar 固定、`<main>` が縦スクロール）。横スクロールバーは常時画面下部。フィット表示は全幅収まる比率でスケール
- **WPF パリティ**: カード項目・ホバー内容（`CardContentsInfo` 相当：概要/テストケース/設計概要を HTML 描画）、フィット表示、デフォルトステップ等は WPF 版に合わせる
- **API 契約**: `frontend/openapi` で `openapi-typescript` による型生成（`npm run generate`）。SPA は契約（型）だけで API に依存

### テスト

- Vitest + Testing Library（jsdom）。レイアウト契約は emotion のベンダー重複宣言で `getComputedStyle` が落ちるため `cssRulesFor` でスタイルシート生テキストを検証
- 実ブラウザ検証は headless Edge（playwright-core）で起動・操作・スクショ確認
- API 側は `Kanban.Api.Tests`（WebApplicationFactory + MSTest）で統合テスト。SPA 未導入の手動受け入れは checklist で担保
