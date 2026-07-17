# Kanban アプリケーション - アーキテクチャドキュメント

## 1. プロジェクト概要

| 項目 | 詳細 |
|------|------|
| **プロジェクト種別** | 単一 WPF デスクトップアプリケーション（ASP.NET Core、Blazor、MAUI ではない） |
| **フレームワーク** | .NET 10.0-windows (`net10.0-windows`) |
| **UI テクノロジ** | XAML ベースのビューを持つ WPF (Windows Presentation Foundation) |
| **出力種別** | WinExe |
| **プロジェクトファイル** | `Kanban.csproj`（単一プロジェクト、.sln ファイルなし） |
| **Nullable/ImplicitUsings** | 有効 |
| **著作権** | FST Co., Ltd. (2016-2026)、バージョン 1.0.0.0 |

## 2. プロジェクト構造

```
D:\Kanban\Kanban\
├── Kanban.csproj              # 単一プロジェクトファイル
├── App.config                 # MongoDB 接続設定、アプリ設定
├── Board.ico                  # アプリケーションアイコン
├── fonts/                     # カスタムフォント (BRADHITC.ttf)
├── images/                    # ~70+ の UI アセット画像
├── ReleaseNote/               # HTML リリースノート（英語 + 日本語）
│   ├── ReleaseNote.html
│   └── ReleaseNote_jp.html
└── src/                       # すべてのソースコード
    ├── App.xaml / App.xaml.cs     # アプリケーションエントリポイント
    ├── AssemblyInfo.cs            # アセンブリメタデータ
    ├── Adapter\                   # インフラストラクチャ層（Repository + DB）
    │   ├── BoardFactory.cs
    │   ├── CardFactory.cs
    │   ├── BoardRepository.cs
    │   ├── CardRepository.cs
    │   ├── ActivityRepository.cs
    │   ├── AppSettings.cs
    │   ├── DBBackup.cs
    │   ├── ProcessExecutor*.cs
    │   ├── HtmlConverter\         # HTML-to-XAML コンバーター（Microsoft 由来コード）
    │   │   ├── HtmlSchema.cs
    │   │   ├── HtmlParser.cs
    │   │   ├── HtmlLexicalAnalyzer.cs
    │   │   ├── HtmlCssParser.cs
    │   │   └── HtmlToXamlConverter.cs
    │   └── KanbanDB\              # MongoDB リポジトリ実装
    │       ├── Board.cs (Repo)
    │       ├── Card.cs (Repo)
    │       ├── Activity.cs (Repo)
    │       ├── WorkState.cs (Repo)
    │       ├── ProcessStep.cs (Repo)
    │       ├── KanbanItem.cs (ベースクラス)
    │       ├── CollectionNames.cs
    │       ├── DBClient.cs
    │       ├── IDBClient.cs
    │       ├── IDBBoard.cs
    │       ├── IDBCard.cs
    │       ├── IDBActivity.cs
    │       ├── DBBoards.cs
    │       ├── DBCards.cs
    │       ├── DBActivities.cs
    │       ├── DBUpgrader.cs
    │       └── DBVersion.cs
    ├── Model\                   # ドメインモデル層
    │   ├── Board.cs
    │   ├── Card.cs
    │   ├── Activity.cs
    │   ├── KanbanItem.cs (ベースクラス)
    │   ├── WorkState.cs
    │   ├── DevProcess.cs
    │   ├── BoardHistory.cs
    │   ├── BoardHistoryBuilder.cs
    │   ├── BoardHistoryWeeklyDecorator.cs
    │   ├── CardMetrics.cs
    │   ├── CycleTimeMetrics.cs
    │   ├── ThroughputMetrics.cs
    │   ├── CardsActivitiesCache.cs
    │   ├── ProcessCycleTime.cs
    │   ├── CycleTimeDistribution.cs
    │   ├── CycleTimePercentage4Step.cs
    │   ├── CardCompletionPerformance.cs
    │   ├── EstimatedWorkEffortExtension.cs
    │   ├── IBoardRepository.cs
    │   ├── ICardRepository.cs
    │   ├── IActivityRepository.cs
    │   ├── IProcessExecutorClient.cs
    │   └── IDBBackup.cs
    ├── ViewModel\               # MVVM ViewModels
    │   ├── Board.cs (ViewModel)
    │   ├── Card.cs (ViewModel)
    │   ├── BoardListViewModel.cs
    │   ├── BoardPageViewModel.cs
    │   ├── ProcessStepViewModel.cs
    │   ├── WIPDoneViewModel.cs
    │   ├── NotifyPropertyChangedBase.cs
    │   ├── DelegateCommand*.cs
    │   ├── ContainerExtension4ViewModel.cs  # Delegate 型定義
    │   ├── VisualMetrics\       # チャート/メトリクス可視化（21 ファイル）
    │   └── ...
    ├── View\                    # WPF Views およびユーティリティ
    │   ├── xaml/                # XAML ビューファイル
    │   │   ├── MainWindow.xaml
    │   │   ├── BoardListPage.xaml
    │   │   ├── BoardPage.xaml
    │   │   ├── DrawingCanvas4Metrics.xaml
    │   │   ├── EditCardDialog.xaml
    │   │   ├── DropDBDialog.xaml
    │   │   ├── SystemSetting.xaml
    │   │   └── AboutControlView.xaml
    │   ├── DragAndDropLib/      # カスタムドラッグ&ドロップライブラリ（6 ファイル）
    │   ├── DependencyInjector.cs
    │   ├── Localization.cs
    │   └── ... (コンバーター、ヘルパー)
    └── Util\                    # 共有ユーティリティ
        ├── EventAggregator.cs
        ├── ConfigReader.cs
        ├── EnumUtil.cs
        ├── DateTimeUtil.cs
        ├── KanbanDefinitions.cs
        └── StringExtension.cs
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

## 4. アーキテクチャパターン

### MVVM (Model-View-ViewModel) + リポジトリパターン

| レイヤー | ネームスペース / ディレクトリ | 目的 |
|---------|---------------------------|------|
| **View** | `Kanban.View.*` + XAML ファイル | WPF UI、コンバーター、ドラッグ&ドロップライブラリ |
| **ViewModel** | `Kanban.ViewModel.*` | INotifyPropertyChanged、DelegateCommand を持つ MVVM ViewModels |
| **Model (ドメイン)** | `Kanban.Model.*` | ドメインエンティティとビジネスロジック |
| **Repository/Adapter** | `Kanban.Infrastructure`, `Kanban.Repository` | MongoDB データアクセス層 |

### 追加で採用されているパターン:

- **Event Aggregator** (`src/Util/EventAggregator.cs`) - 型付きイベントパブリッシャー/サブスクライバーによる非同期コンポーネント間通信（シングルトン per イベントタイプ）
- **ファクトリパターン** - Board および Card の作成用（`BoardFactory`, `CardFactory`）
- **デコレータパターン** - プロセス実行用（`ProcessDecorator` は `System.Diagnostics.Process` を拡張、`ProcessExecuteCompositeCommand`）
- **ビルダーパターン** - メトリクス可視化用（`VisualCFDBuilder`, `VisualTeamVelocityBuilder` など）

## 5. データモデル / エンティティ

### ドメインモデル（`Kanban.Model` 内）

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

### リポジトリモデル（`Kanban.Repository` 内、MongoDB シリアライズ用）

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

**Entity Framework Core は使用していません。** このプロジェクトは **MongoDB.Driver** NuGet パッケージを直接使用しています（バージョン 3.8.1）。

### 接続設定（`App.config`）

```xml
<add key="DB_HOST" value="192.168.21.150:27017" />
<add key="DB_NAME" value="kanban" />
<add key="CumulateCardsOnBackBoard" value="true" />
<add key="CanDropDatabase" value="true" />
<add key="LimitNum4QueringLastProcess" value="20" />
<add key="DB_BACKUP_TOOL_PATH" value="C:\Program Files\MongoDB\Server\8.3\bin" />
<add key="CardWidth" value="260" />
```

### MongoDB クライアント（`src/Adapter/KanbanDB/DBClient.cs`）

- `"mongodb://" + DBHost` 接続文字列で `MongoClient` を作成
- `IMongoDatabase DB` プロパティを提供
- データベースのリスト取得、起動時のコレクションアップグレード
- データベース名の解決: DB が存在する場合はユーザー設定を優先し、それ以外の場合は設定ファイルの値を使用

### コレクション名（`CollectionNames.cs`）

| コレクション | 目的 |
|-------------|------|
| `"Boards"` | ボードドキュメント |
| `"Cards"` | カードドキュメント |
| `"Activities"` | アクティビティ/監査証跡ドキュメント |
| `"Version"` | スキーマバージョン管理 |

### リポジトリインターフェースと実装（リポジトリ層）

| インターフェース | 実装 | 場所 |
|-----------------|------|------|
| `IDBClient` | `DBClient` | Adapter/KanbanDB/ |
| `IDBBoard` | `DBBoards` | Adapter/KanbanDB/ |
| `IDBCard` | `DBCards` | Adapter/KanbanDB/ |
| `IDBActivity` | `DBActivities` | Adapter/KanbanDB/ |

### ドメインリポジトリインターフェースと実装（ドメイン層）

| インターフェース | 実装 | ネームスペース |
|-----------------|------|---------------|
| `IBoardRepository` | `BoardRepository` | Kanban.Infrastructure |
| `ICardRepository` | `CardRepository` | Kanban.Infrastructure |
| `IActivityRepository` | `ActivityRepository` | Kanban.Infrastructure |

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

**DI コンテナ**: Unity（バージョン 5.11.10）- Microsoft.Extensions.DependencyInjection ではない。

**ファイル**: `src/View/DependencyInjector.cs`

登録は5つのスコープグループに分割されています:

```
DependencyInjector
├── DI4Repository     → IDBClient/DBClient, IDBBoard/DBBoards, IDBCard/DBCards, IDBActivity/DBActivities（すべて ContainerControlledLifetime = シングルトン）
├── DI4Infrastructure → IAppSettings/AppSettings, ILocalization/Localization, IProcessExecutor/ProcessExecutor, IBoardRepository/BoardRepository, ICardRepository/CardRepository, IBoardFactory/BoardFactory, ICardFactory/CardFactory, DBBackupFactory
├── DI4Model          → 静的プロパティインジェクション: Board.BoardRepository, Card.CardRepository, Activity.ActivityRepository（シングルトン静的参照）
├── DI4ViewModel      → CardFactory デリゲート, WIPDoneViewModelFactory デリゲート, ProcessStepViewModelFactory デリゲート
└── DI4View           → MainWindow (ContainerControlled), IViewModelProperties インスタンス, BoardPageFactory デリゲート
```

**主要パターン**: ドメインモデル（`Model.Board`, `Model.Card`, `Model.Activity`）は起動時に設定される**静的**リポジトリ参照を保持しています。これはアンチパターンですが、同時実行インスタンスがないデスクトップアプリでは機能します。

## 8. フロントエンドコンポーネント（XAML Views）

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

**認証も認可もありません。** これはログイン、ロール、権限のない単一ユーザー向けデスクトップアプリケーションです。データベース接続は資格情報なしで構成された MongoDB ホストに直接接続します。

## 12. 主要なアーキテクチャ観察点

- **単一プロジェクト**。複数のプロジェクトからなるソリューションではない
- **MongoDB ネイティブドライバー**（EF Core ではない）- ドキュメントは `[BsonId]` 属性付きの POCO
- **Unity DI コンテナ**による依存関係注入（レガシーな選択；Microsoft.Extensions.DependencyInjection がよりモダン）
- ドメインモデルへの**静的プロパティインジェクション**（`Board.BoardRepository = ...`）。コンストラクタインジェクションではない
- ViewModel と View の間の緩結合のための **Event Aggregator パターン**
- **API コントローラーや REST エンドポイントなし** - 純粋なデスクトップクライアントアプリケーション
- リッチテキストカード説明用の **HTML-to-XAML コンバーター**を含む（Microsoft 由来コード）
- `mongodump`/`mongorestore` 経由で MongoDB バックアップ/復元コマンドを実行するための**プロセス実行システム**。コンポジット/デコレータパターン採用

## 13. パッケージ依存関係

| パッケージ | バージョン | 目的 |
|-----------|-----------|------|
| `MongoDB.Driver` | 3.8.1 | NoSQL データベースドライバー |
| `SharpCompress` | 0.48.0 | アーカイブ圧縮（DB バックアップ用） |
| `Unity` | 5.11.10 | 依存関係注入コンテナ |

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
