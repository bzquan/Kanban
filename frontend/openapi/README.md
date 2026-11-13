# Kanban API — OpenAPI 型生成

ASP.NET Core Minimal API(`Kanban.Api`)が `/openapi/v1.json` で公開する OpenAPI 記述から、
TypeScript 型 `schema.d.ts` を生成する。次計画のフロントエンド SPA は、API 実装を読まずに
この型(契約)だけで着手できる(計画 完了基準)。

## 使い方

1. API を起動する(テスト用 DB を使う例。既定は appsettings の本番 DB):

   ```
   dotnet run --project ../../Kanban.Api -- --urls http://localhost:5093 --Kanban:DB_HOST localhost:27017 --Kanban:DB_NAME kanban_test
   ```

2. 型を生成する:

   ```
   npm install
   npm run generate
   ```

   生成物: このディレクトリの `schema.d.ts`(ルート参照用)。

## 含まれる型(抜粋)

- `BoardDto` / `ProcessStepDto` — ボードと列
- `CardDto` / `WorkStateDto` — カード(読み取り・書き込み共通)
- `CfdDto` / `CycleTimeDto` / `ThroughputDto` / `BugsBlockedDto` / `VelocityDto` — メトリクス5系統

## 注意

- `schema.d.ts` はコミット済み(フロント計画までの参照用)。API 変更後は `npm run generate` で再生成すること。
- 認証/CORS はこのバージョンでは意図的に未実装(localhost 開発用途、設計書 Global Constraints)。
