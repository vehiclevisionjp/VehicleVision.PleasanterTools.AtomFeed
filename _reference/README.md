# _reference（参考専用・AGPL）

Pleasanter 本体（**GNU AGPL v3**）は AI の事実確認専用のソースであり、
Git で追跡せず必要時に上流 `main` の先頭を取得する。
CI は取得せず、ビルド・テスト・配布にも使わない。日次更新やポインタ更新 PR は廃止する。

- 上流: <https://github.com/Implem/Implem.Pleasanter>
- 既定の取得先: `_reference/Implem.Pleasanter/`（Git 管理外）
- `_reference/README.md`（このファイル）は引き続き追跡する

## 厳守事項

- **コードをコピー・流用・リンク・プロジェクト参照しない。** 本アプリは本体を改造せず、
  **標準 API を叩く別アプリ**として実装する
- 取得したソースは API 仕様・サイト設定のスキーマ・列定義・挙動の事実確認だけに使い、編集しない
- ソリューション・ビルド・配布物へ含めない。共有先を使ってもローカルへのリンクは作らない
- デュアルライセンス（AGPL ＋ 商用）の前提は変わらない。
  本体のコードを取り込むと商用での提供の前提が崩れる（[ライセンス](../LICENSING.md)）

## 読む前に取得・同期する

```bash
bash scripts/sync-reference.sh
```

取得済みなら `main` の先頭へ fast-forward する。ローカル変更・異なる接続先などの異常は失敗として止める。
成功時は取得先・短い SHA・コミット日・状態が出る。取得不能な内容は未確認とし、推測しない。
旧 gitlink の削除に伴う取得済みソースの削除や `git submodule deinit` は不要。

複数のアプリで共有する場合は、環境変数で取得先を指定し、その場所を直接読む。
既定先とのリンクは作らない。ビルド・配布対象の外に置くこと。

```powershell
$env:PLEASANTER_REFERENCE_DIR = 'D:\references\Implem.Pleasanter'
bash scripts/sync-reference.sh
Remove-Item Env:PLEASANTER_REFERENCE_DIR
```

## 根拠を記録する

**確認した完全なコミット SHA・ソースの固定リンク・参照日**を文書へ残す。
親リポジトリの gitlink や現在の `main` は過去の確認結果の根拠にしない。

```bash
git -C _reference/Implem.Pleasanter rev-parse HEAD
git -C _reference/Implem.Pleasanter log -1 --format='%H %ad' --date=short
```

固定リンクは `https://github.com/Implem/Implem.Pleasanter/blob/<完全なSHA>/<ファイルパス>#L<行>` とする。
共有先を指定した場合は上記コマンドの取得先も置き換える。
`main` はリリース前の変更を含むため、必要なら本体の `<Version>` とリリースタグとの対応も確認する。
過去の文書の版だけを上げず、変更後のソースで再確認する。**実機未検証ならその旨を明記する。**

ソースだけでは実際の API 応答（日時の書式・タイムゾーン変換・未設定値）や運用時の権限は確定しない。
これらは実機で確認する。`tools/pleasanter-testenv` の公式イメージは独立した固定版として維持する。

## 事実確認の調査先

既存の調査先の案内を維持する。同期した時点のソースで実在と内容を確認してから根拠にすること。

| 対象 | 調査項目 |
| --- | --- |
| `Implem.Pleasanter/Controllers/Api/ItemsController.cs` | 標準 API のエンドポイントとルーティング |
| `Implem.Pleasanter/Models/Results/ResultUtilities.cs` | 一覧取得の応答とページ上限 |
| `Implem.Pleasanter/Models/Issues/IssueApiModel.cs` | 記録テーブルの API 上の項目 |
| `Implem.Pleasanter/Libraries/Requests/Context.cs` | API キーの受け渡し方式 |

## 移行の根拠

Template の承認済みコミット
[`3312f003fbe9827090e20bc643800824bdf30e22`](https://github.com/vehiclevisionjp/VehicleVision.PleasanterTools.Template/commit/3312f003fbe9827090e20bc643800824bdf30e22)
の配布手順に基づく（参照日: 2026-10-27）。
背景は vehiclevisionjp/VehicleVision.PleasanterTools.Template#61・
vehiclevisionjp/VehicleVision.PleasanterTools.Template#63。
