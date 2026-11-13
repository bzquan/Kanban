# カンバンWebアプリケーション移植 提案書(設計ドキュメント)

- 日付: 2026-11-13
- 状態: レビュー・質疑反映済み(承認待ち)
- 対象: WPF製カンバン管理システム(`D:\Kanban`)のWebアプリケーションへの移植

## 1. 意図と背景

現在のシステムはWPF(.NET 10)による単一デスクトップアプリで、MongoDB(Boards / Cards / Activities / Version コレクション)を直接参照して動作している。

問題は、**WPFアプリをインストールしていない人がボードを見られない**こと。社内ローカルネットワークでカンバンを共有したいが、閲覧のためだけに各マシンへアプリを配布・維持するのが負担になっている。

本提案は、これを**ブラウザで動くWebアプリケーションへ移植し、WPFアプリを廃止する**もの。Web版は単なる閲覧専用ではなく、カードの追加・編集・ドラッグ移動まで行える**完全な置き換え**とする。

## 2. ゴール / 非ゴール / アンチゴール

### ゴール(利用者が決めた事項)

- ブラウダだけでボード閲覧・操作ができるようにする
- 既存WPF版の機能は**バックアップ系を除いてすべて**Web版に残す(メトリクスチャート5種を含む)
- サーバー側はC#で作成する
- 複数人が同時にボードを開いてフル操作できる(カードのドラッグ移動・編集・追加)

### 非ゴール(明示的にスコープ外)

- **認証・認可・通信セキュリティ**: 社内ローカルネットワーク利用のため考慮しない(利用者指示)
- **リアルタイム同期**: 誰かがカードを動かしても他ブラウザへの即時反映は今回の対象外(利用者指示。「保留」)。更新操作で反映される動きで始める
- **DBバックアップ/リストア/DB削除**: WPF版の`mongodump`/`mongorestore`連携・`DropDBDialog`は**廃止**する(利用者指示)。万一の復旧はアプリ外の運用(担当者が手動でmongodumpを実行)に委ねる
- MongoDBのスキーマ変更・データ移行(既存DBをそのまま使うため不要)
- モバイルネイティブアプリ

### アンチゴール(これが起きたら失敗)

- 既存のMongoDBデータがWeb版で読み込めない、またはWeb版が既存データを書き換えられない
- カード移動(ドラッグ&ドロップ)がデスクトップ版より遅い・不安定になる
- 多言語(EN/JP/ZH)が欠落する
- WPF版が「まだ必要」な状態のまま残る(完全置き換えにならない)

## 3. 制約

- サーバー側はC#(ASP.NET Core)。利用者の明示的な指定
- .NET 10(既存アプリと同一。ASP.NET Coreで利用可)
- MongoDB 8.x / 既存コレクション構成(Boards, Cards, Activities, Version)をそのまま使用。データ移行はしない
- 社内サーバー1台に常駐させ、社内LANからのみアクセス
- 認証はない(＝誰でも編集できる。この事実はアンチゴール側のリスクとして受け入れ済み)
- フロントエンドはTypeScript。それを保守できる personnel をチームに確保する(利用者がC案採用により受諾)

## 4. 方式(決定済みの設計)

### 4.1 全体構成

```
ブラウザ(社内ユーザー)
  └─ React + TypeScript SPA  … Viteでビルドした静的ファイル
        │  JSON (HTTP/REST)
        ▼
ASP.NET Core (.NET 10) Web API   ← 社内サーバー1台で常駐
  ├─ Model層(既存C#をそのまま参照: Board/Card/Activity/metrics計算)
  ├─ リポジトリ層(既存 MongoDB.Driver 実装をそのまま)
  └─ MongoDB(既存DB・既存コレクションをそのまま)
```

SPAのビルド成果物(`dist/`)をASP.NET Coreアプリの`wwwroot`以下に配置し、`http://<サーバー>:<ポート>` の1エントリポイントでSPAとAPIの両方を配信する。社内LANのためHTTPS・CORSは設定しない。

**データベースの scope(利用者決定)**: デプロイあたり1つのMongoDBデータベースに固定する。接続先はサーバー側の`appsettings.json` `DB_NAME`だけで決まり、ブラウザからDBを切り替える仕組みは提供しない(同一ホスト`192.168.21.150`にはカンバン用DBが複数存在することが確認済み。誤って別DBに繋がないためにも、この方針をAPI/画面の両方で徹底する)。これによりリポジトリ層・`IMongoDatabase`はシングルトンのまま、既存WPF版と同じ構成で使える。

### 4.2 サーバー側: 既存C#コードの流用

| 既存アセット | 行方 |
|---|---|
| `src/Model/`(Board, Card, Activity, WorkState, DevProcess, BoardHistoryBuilder, ThroughputMetrics, CycleTimeMetrics 等) | **そのままサーバー側のクラスライブラリとして再利用**。メトリクスはサーバーで計算し、チャート用の数値系列(JSON)を返す |
| `src/Adapter/`(BoardRepository, CardRepository, ActivityRepository, KanbanDB/* のMongoDB.Driver実装, MongoDB.Driver 3.8.1) | そのままサーバー内部で使用する。ブラウザがMongoに直接触れる経路は作らない |
| Unity DI(5.11.10) | ASP.NET Core標準の`Microsoft.Extensions.DependencyInjection`へ置換 |
| `App.config` | `appsettings.json`へ移行(保持キー: `DB_HOST`, `DB_NAME`, `CumulateCardsOnBackBoard`, `LimitNum4QueringLastProcess`)。`IAppSettings`/`AppSettings`は`ApplicationSettingsBase`(System.Windows依存)なので、設定機構ごとサーバー側実装に置き換える |
| `src/Util/`(DateTimeUtil, StringExtension, KanbanDefinitions, EventAggregator 等) | そのまま再利用。ただし検証済みで`Util.cs`/`EnumUtil.cs`/`HandWriteFontFamily.cs`/`CircleNumImages.cs`/`IAppSettings.cs`は`System.Windows`(Media/Controls)参照のため、クラスライブラリ抽出時にサーバー側で不要なWPF依存ファイルを分離・削除する |
| `.resx` (EN/JP/ZHの`Resources*.resx`) | 文字列をJSONにエクスポートし、フロントエンドのi18nextで読み込む |
| `DBUpgrader` / `DBVersion`(v1.0→v1.2マイグレーション) | サーバー起動時の初期化処理として保持(実行タイミング・方法はビルダーの裁量) |

**置き換える/廃止するもの**

| 既存 | 扱い |
|---|---|
| `DBBackup`, `ProcessExecutor*`一式, `mongodump`/`mongorestore`呼び出し, `DropDBDialog`, `DB_BACKUP_TOOL_PATH`, `DBBackupFolder`, `CanDropDatabase` | **廃止**(バックアップ系全体)。サーバーにMongoDB Toolsは不要 |
| `ViewModel/`, `View/`(XAML, DragAndDropLib, DrawingVisual系チャート, コンバーター), スプラッシュスクリーン, WPF MessageBox回避用ダミーウィンドウ | 廃止。フロントエンドへ書き直し |
| `Adapter/HtmlConverter/`(HTML→XAML) | 廃止。カード概要のリッチテキストHTMLはブラウザが直接表示する( sanitization なし。セキュリティスコープ外のため。要出所のHTMLを入れない運用前提) |
| モデルの静的リポジトリ参照(`Board.BoardRepository = ...` 等の静的プロパティインジェクション) | 廃止し、サーバー側はコンストラクタインジェクション + リクエストスコープに修正する |
| Unity DIの`ContainerControlledLifetime`シングルトン前提 | ASP.NET CoreのDIスコープ設計に合わせて見直し(ビルダーの裁量) |

### 4.3 API設計(骨子)

URLはプレフィックス `/api`。DTOは既存リポジトリ層のモデルをベースに新設し、OpenAPI定義からフロントエンド側TS型・APIクライアントを`openapi-typescript`で自動生成する(C#/TSの型ズレ対策)。

```
GET    /api/boards                          ボード一覧
POST   /api/boards                          ボード作成(デフォルトプロセスステップ付き)
DELETE /api/boards/{id}                     ボード削除
PUT    /api/boards/{id}/process-steps       プロセスステップ追加/削除/並べ替え/改名
GET    /api/boards/{id}/cards?filter=...    カード取得(WorkState・フィルタ文言・バックボード対応)
POST   /api/cards                           カード追加(初期Activityレコードも作成)
PATCH  /api/cards/{id}                      カード編集(タイトル/タグ/担当者/概要/テストケース/
                                            設計概要/ストーリーポイント/カード種別/リリース日等)
PATCH  /api/cards/{id}/move                 列間移動・列内並び替え(workState + seqNo、Activity記録)
DELETE /api/cards/{id}                      カード削除(既存WPF版`DeleteCardCommand`に対応)
GET    /api/boards/{id}/metrics/cfd         累積フローダイアグラム用データ
GET    /api/boards/{id}/metrics/cycle-time  サイクル時間分布用データ
GET    /api/boards/{id}/metrics/throughput  スループット用データ
GET    /api/boards/{id}/metrics/bugs-blocked バグ&ブロック用データ
GET    /api/boards/{id}/metrics/velocity    チームベロシティ用データ
```

- メトリクス5種の計算は既存の`BoardHistoryBuilder`→`ThroughputMetrics`/`CycleTimeMetrics`等のフローをサーバー側でそのまま実行する。フロントエンドは「受け取った数値系列をチャートライブラリに渡すだけ」の薄い層にする
- `PATCH /api/cards/{id}/move` は、既存WPF版の`Helper.AddItem()/RemoveItem()`によるSeqNo再採番 + `Card.CreateNewActivities()` + `CardRepository.Update()`と同じ不変条件を維持する(移動履歴がActivitiesに積まれ、メトリクスが正しく計算されること)
- `POST /api/boards` には作成者のUI言語を添える。デフォルトのプロセスステップ名(Backlog/Requirement/…)は作成時の言語でローカライズされて永続化する既存仕様(`DevProcess` + `ILocalization`)をWebでも維持するため

### 4.4 リアルタイム同期(保留)への布石

APIの書き込みエンドポイント(POST/PATCH/DELETE)成功後に「どのボードが変わったか」を通知するインターフェース(`IBoardChangeNotifier`程度の薄い口)をサーバー側に用意し、実装は今回はno-opとする。将来SignalR(hub)を実装してつなげば、API構造を変えずに即時反映を追加できる。フロントエンドにも「変更通知を購読する口」を予約だけしておく(今回はTanStack Queryのinvalidateで手動更新に対応)。

### 4.5 フロントエンド

- **React + TypeScript + Vite** を採用(利用者にReactで提示し受諾済み)
- 主要ライブラリ:

| 課題 | 選定 | 理由 |
|---|---|---|
| カードのドラッグ&ドロップ | **dnd-kit** | 列間移動・列内並び替え・ドラッグ中の自動スクロールが標準機能。既存WPF版`DragAndDropLib`(ドラッグゴースト、挿入位置表示)相当のUIを再現可能 |
| メトリクスチャート5種 | **Apache ECharts** | CFD(積み上げ面)・サイクル時間分布(積み上げ棒)・スループット(棒+実績パフォーマンス表示)・バグ&ブロック(面)・ベロシティ(折れ線)が1ライブラリで揃い、ツールチップ・凡例も標準 |
| サーバー状態管理 | **TanStack Query** | 編集成功後のinvalidateが「更新」ボタンの代替。保留中のリアルタイム通知の受け口にもなる |
| UIコンポーネント | MUI or shadcn/ui 等 | ビルダーの裁量 |
| 多言語 | **i18next** | 既存`.resx`由来のJSONをロード。言語検出は`navigator.language`(ja / zh / enデフォルト)から判定し、システム設定で手動上書き可 |

- 画面構成(WPF版ナビゲーションの対応物):
  - **ボード一覧ページ**(旧BoardListPage): ボードリスト + 作成/削除 + プロセスステップ設定
  - **ボードページ**(旧BoardPage): ツールバー(カード追加・ズーム・更新・フィルタ)、プロセスステップ列(各列にWIP/Doneサブ列)、バック/フロント切替
  - **カード編集ダイアログ**(旧EditCardDialog)
  - **システム設定ページ**(旧SystemSetting): 言語切替のみ(DB名選択とバックアップセクションは廃止)
  - **About/リリースノート**(旧AboutControlView): リリースノートHTMLをそのまま表示
- カード表示の既存要素は維持: ストーリーポイント表示、作業日プログレスバー、マージ状態インジケーター、ブロック表示、種別別色分け、ズーム
- カード概要(リッチテキスト)の編集は**プレーンなtextareaでのHTMLテキスト編集**(WPF版`EditCardDialog`が生テキストのTextBoxだったこととのパリティ)とする(筆者の判断)。WYSIWYGリッチテキストエディタの導入は今回スコープ外
- カード幅(旧`CardWidth=260`)等のUI設定はフロントエンド側定数/localStorageへ

### 4.6 ユーザー設定の扱い

WPFの`ApplicationSettingsBase`(ユーザー設定)はブラウザ`localStorage`へ移行:

| 旧設定 | 移行先 |
|---|---|
| `Language` | localStorage + i18next |
| `Database` | 廃止(DBはデプロイ単位で`appsettings.json`に固定。Webに切り替え口は作らない) |
| `IsBoardZoomed` / ズームレベル | localStorage |
| `LastUsedMetricChartType` | localStorage |
| `LastUsedBoardID` | localStorage |
| `MainWindowSize` / 最大化状態 | 不要(Webでは消滅) |
| `DBBackupFolder` | 廃止 |

### 4.7 デプロイ

- 社内サーバー1台で`dotnet run`/Windowsサービス等によりASP.NET Coreアプリを常駐
- SPAはビルドして`wwwroot`に同梱済み(`dotnet publish` 1本でデプロイ)
- アプリは常に既存MongoDB(`192.168.21.150:27017`等の`appsettings.json`設定)に接続
- 開発時はVite dev server → APIプロキシが通常形態
- **前提(筆者の仮定・要確認)**: 社内サーバーはWindowsであり、.NET 10ランタイムのインストールとLAN向けポート開放をチーム自身で行える。この前提が崩れる場合(Linuxサーバー、運用管理制度あり等)は常駐形態の計画を作り直す
- **開発・テストのデータ(筆者の判断)**: 開発マシンから`192.168.21.150:27017`へ到達できることは確認済み。既存TestKanbanはセットアップ/-teardownでDBをDropする作りのため、自動テストは専用テストDB名(例: `kanban_test`)のみを接続・Drop対象とし、運用中のDB(複数存在するカンバン用DB)に絶対に触れない設定にする

## 5. 実装順の提案(あくまで提案。確定計画は次工程の計画書で作成)

1. サーバー基盤: クラスライブラリ化(Model/Adapterの抽出)、ASP.NET Coreプロジェクト新設、DI置換、静的リポジトリ参照の解消
2. 読み取りAPI(ボード一覧・カード取得) + SPA足場(Vite/React/ROUTER/i18next)
3. ボードページ描画 + ドラッグ&ドロップ移動・並び替え(SeqNo/Activity不変条件)
4. カード作成/編集/削除、フィルタ、バックボード、ズーム
5. メトリクスAPI + EChartsチャート5種
6. プロセスステップ編集、システム設定、About、多言語仕上げ
7. ダブルクライアント(WPF/Web)データ互換回帰、並行利用の調整(last-write-wins確認)、テスト移行(専用テストDBで既存TestKanbanの趣旨を維持)、デプロイ自動化

## 6. 已知のリスクと対処方針

| リスク | 方針 |
|---|---|
| リアルタイム同期なしで複数人が同時編集すると、他ユーザーの変更が画面に出るまでタイムラグが生まれ、後から保存した側が上書き(last-write-wins)し得る | 今回受け入れ。`IBoardChangeNotifier`の口を予約しておき、将来のSignalR導入で解消する道を示す |
| 移行期間中もWPF版が現役で同じデータを使い続ける(利用者確認済み)。Web版APIの書くActivities/SeqNoがWPF版と非互換だと、WPFユーザーのメトリクスや並び順が壊れる | サーバー側でWPF版と同じ書き込み経路(`CreateNewActivities()`、SeqNo再採番)を再利用し、「Web版で操作→WPF版で表示」のダブルクライアント回帰テストを実装順の早期に追加する |
| 移行期間中にWPF版へ新機能が追加され続けると、機能パリティの目標が動き続ける | 仮定(筆者): 移植中、WPF版は機能凍結(バグ修正のみ受け入れる)。これを変える場合はその都度パリティ範囲を再合意する |
| メトリクスAPIが遅いと「デスクトップ版より遅い」というアンチゴールに抵触 | データ量は利用者確認済みで小規模(表示カード100件以内、アクティビティはカードあたり約30件)。Activitiesからのリクエスト毎再計算で十分であり、キャッシュ・事前集計は不要。必要になった時点で追加する余地を残す |
| dnd-kitで既存WPF版と同等以上の操作感(自動スクロール・挿入位置表示)が作れるか | 早期(実装順3)に検証。同等でなければ別途ドラッグ専用ライブラリの差し替えを検討 |
| 静的リポジトリ参照(Model層の静的プロパティ)がマルチスレッドWebサーバーで破綻する | 実装順1で必ず解消。Model層のDI方式変更是設計の前提 |
| メトリクス計算のC#再利用時に、WPF版`CardsActivitiesCache`の静的キャッシュがスコープ間で汚染される | サーバー側ではリクエストスコープのキャッシュインスタンスへ置換(ビルダーの裁量、ただし放置は不可) |

## 7. ビルダーに委ねる事項(決定していないこと)

- ソリューション/プロジェクト構成(クラスライブラリ分割の粒度、`Kanban.Domain`/`Kanban.Api`/`kanban-web`等の名前)
- APIコントローラーの細部(URL微調整、POST/PUT/PATCHの責務分担)、DTOのフィールド名
- UIコンポーネントライブラリの最終選定(MUI / shadcn/ui / 自作)
- 画面の細部レイアウト、スタイル(既存WPF版の見た目との同等性どこまで追求するか)
- テスト方針(既存TestKanbanプロジェクトの移植範囲、API/E2Eテストの追加手段)
- DBUpgraderの起動時実行の具体的な仕組み(HostedService / ミドルウェア)
- Windowsサービス化するか、常駐方法の運用形態
- `appsettings.json` の最終キー体系

## 8. 決定の帰属一覧

**利用者が決めた事項**
- 目的: WPF未導入者にもボードを見せる
- WPFアプリの完全廃止(Web版が置き換え)
- ローカルネットワーク利用。セキュリティ考慮不要
- サーバー側はC#
- リアルタイム同期は保留
- Webユーザーはフル操作可(ドラッグ移動・編集・追加)
- 既存WPF版の機能はバックアップ系を除きすべて残す(メトリクスチャート含む)
- バックアップ系(DBバックアップ/リストア/DB削除)の廃止
- C案(JS SPA + Web API)採用、フロントはReact
- DBはデプロイ単位で1つ(設定画面からのDB選択は廃止)。ホスト上にはカンバン用DBが複数存在する
- 開発マシンから`192.168.21.150:27017`へ到達可能。データ量は表示カード100件以内・アクティビティはカードあたり約30件
- 移行期間中、WPF版は同じデータで現役のまま(したがってWeb版の書き込みデータはWPF版と完全互換が必要)

**筆者の判断(利用者は「いいか」以上の明示的同意なし)**
- React + Vite + TanStack Query + i18next
- dnd-kit / Apache ECharts の選定
- 型自動生成にOpenAPI + openapi-typescript
- 既存MongoDB・既存コレクションのそのまま利用(データ移行なし)
- `wwwroot`同梱の1プロセスデプロイ
- HTTPS/CORSなし
- `IBoardChangeNotifier` no-opによるリアルタイム布石
- 静的リポジトリ参照・静的キャッシュの解消方針
- UIコンポーネントライブラリはビルダー裁量
- 実装順の提案
- カード概要編集はプレーンtextareaでWPF版とパリティ(WYSIWYGエディタはスコープ外)
- デプロイ先サーバーはWindows・.NET 10ランタイム導入はチーム自身が実施可能と仮定(要確認)
- `src/Util`のWPF依存ファイル(`Util.cs`/`EnumUtil.cs`/`HandWriteFontFamily.cs`/`CircleNumImages.cs`/`IAppSettings.cs`)は分離・削除
- APIへの`DELETE /api/cards/{id}`追加と`POST /api/boards`へのUI言語添付
- 自動テストは専用テストDB(例: `kanban_test`)のみを接続・Drop対象とする
- 移植中のWPF版は機能凍結(バグ修正のみ)と仮定
- 確認済みの小規模データ量を前提に、メトリクスはリクエスト毎再計算としキャッシュ・事前集計を設けない
