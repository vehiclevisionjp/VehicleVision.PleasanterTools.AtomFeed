# エージェント向けリポジトリ指示

このファイルは `AGENTS.md` 標準（<https://agents.md/>）を読むコーディングエージェント向けの指示です。
GitHub 上のエージェントは `.github/copilot-instructions.md`、Claude Code は `CLAUDE.md` を読みます。

**規約の全文は [`.github/copilot-instructions.md`](.github/copilot-instructions.md) にあります。
以下は同ファイルの「必ず守る規約」の写しです。作業前に参照元を必ず開いてください。**

<!-- この節は .github/copilot-instructions.md から生成される。ここを直接編集しないこと。
     編集後は bash scripts/sync-agents.sh を実行すること。 -->

## 必ず守る規約

**この節は例外なく守ること。** 詳細は同ファイル内の各節を参照。

1. **日本語で書くこと。** Issue・PR のタイトルと本文、コミットメッセージ、コードレビューコメント、
   CodeQL やセキュリティ警告の解説。すべて日本語
2. **PR の base は分岐元の `develop`。** `master` を base にしないこと
3. **PR 本文に `Closes #N` を書くこと。** Issue と PR を必ず紐付ける
   作業に着手する前に、これからやること・やるべきこと（作業中に見つけたものを含む）を、先に Issue にする。
   会話の「残りの作業」も Issue に起こし、リポジトリをまたぐ作業は、中心のリポジトリに親の Issue を起こして各リポジトリの Issue から参照する
4. **コミットメッセージに `[skip ci]` を含めないこと。** 引用しただけでも push 全体の CI がスキップされる
5. **作業中の PR タイトルには `WIP:` を付ける**（`[WIP]` ではない）。完了したら外す
6. **Pleasanter 本体（AGPL v3）のコードを取り込まないこと。** コピー・流用・リンク・
   プロジェクト参照のいずれも禁止。本体は必要時に取得し、Git では追跡しない。
   `_reference/` は事実確認のための参照専用で、CI・ビルド・配布物へ含めない
   （[`_reference/README.md`](_reference/README.md)）
7. **依存 OSS は寛容ライセンス（MIT / BSD 系 / Apache-2.0）に限定すること。**
   追加時にライセンスを一次情報で確認し、[`NOTICE`](NOTICE) へ追記する
    - **フォントは SIL OFL 1.1 も可**（2026-09-08 決定。AtomFeed は 2026-10-15 に管理画面の書体で採用）。日本語の自由フォントは実質すべて
      OFL であり、これを外すと同梱できるものが無くなる。**OFL はフォント以外へ
      copyleft が伸びない**ので、商用ライセンスでの提供と両立する。
      ⚠️ **フォント以外へこの例外を広げないこと**
8. **Pleasanter の API キーを、購読側（フィードリーダー）へ渡さないこと。**
   資格情報はサーバ側だけで保持する。購読 URL のトークンは Pleasanter の資格情報とは別物
9. **`{}` を省略しない。** `if` / `else` / `for` / `foreach` / `while` / `using` / `lock` などの本文も、
   1 行で済む場合も同じ。C# は `.editorconfig` とビルドで、TypeScript・Svelte は ESLint の `curly` で強制する。
