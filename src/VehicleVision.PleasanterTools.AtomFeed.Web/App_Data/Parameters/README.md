# Parameters（設定ファイル）

**Pleasanter 本体・Questionnaire と同じ方式**で、設定は `App_Data/Parameters/*.json` に置く。
読み込みは 1 か所（`Web/Services/ParameterFiles.cs`）で、アプリの一番先で積む。

## ファイルの分け方（Pleasanter に倣う）

**関心ごとにファイルを分け、関連する設定は入れ子（オブジェクト）にまとめる。** Pleasanter の `Rds.json` / `Security.json` / `Mail.json` /
`Authentication.json` / `Service.json` と同じ名前・同じ考え方で、本アプリ固有のもの（`Feed.json`・`Pleasanter.json`）だけを足している。

| ファイル | 内容 | 階層 |
| --- | --- | --- |
| `Service.json` | 本アプリ全体（公開 URL・既定のタイムゾーン） | 平ら |
| `Rds.json` | 本アプリ専用の DB | 平ら |
| `Security.json` | 2 要素・パスワードの条件・秘密の鍵・監査ログ・信頼するプロキシ | 平ら（`PasswordPolicies` は配列） |
| `Authentication.json` | 管理画面へのログインの外部連携 | `Saml{}` / `PleasanterSso{}` |
| `Mail.json` | 招待メールの送信 | 経路ごと: `Smtp{}` / `AwsSes{}` / `Acs{}`（Pleasanter の `Mail.json` の `AwsSes{}` などに倣う） |
| `Feed.json` | フィードの取得・配信 | `FailureNotify{}`（取得失敗の通知） |
| `Pleasanter.json` | 接続先の Pleasanter | 平ら |

- **ファイルごとに別々に読む**ので、`Rds.json` の `Provider`（RDBMS）と `Mail.json` の `Provider`（送信経路）のように、同じ名前のキーを別のファイルに持てる。
- 入れ子のキーは、下の表では `Smtp:Host` のように `:` でつないで書く（JSON では `"Smtp": { "Host": … }`）。

## 規約

- **キーの説明は、同じ階層に `"//キー名"` の項目を置いて書く。**
- **資格情報（API キー・管理キー・接続文字列）の実値をこのフォルダへコミットしないこと。**
  実値は次のいずれかで与える
    1. 環境変数（Azure App Service のアプリケーション設定など）
    2. `{名前}.local.json`（`.gitignore` 済み。ローカル開発用）
- 上書きの優先順位は **`{名前}.local.json` ＞ 環境変数 ＞ `{名前}.json`**
- **起動時に、読んだ置き場と見つかったファイルをログへ出す。** 置き場を間違えても黙って既定で動くのを防ぐ
  （1 つも見つからなければ警告）

## 管理画面から編集できる設定

**Questionnaire の「アプリケーション設定」と同じ方式。** 次の設定は DB（`AppSettings` 表）へ保管し、管理画面（`GET/PUT /admin/api/settings/`・権限 `settings.manage`＝Administrator のみ）から編集できる。

- **優先順位は 外部設定（このフォルダのファイル・環境変数）＞ DB ＞ 既定値。**
- **外部設定に値があるものは「固定」**になり、画面からは変えられない（画面にも固定と出る）。**同梱のファイルは、画面から編集できる設定の値を `null` にしてある**（値を書くと固定される）。
  既定値はコードにある。運用で画面から変えさせたくない設定だけ、ここか環境変数に書く。
- **保存したら、その台へはすぐ反映され、他の台へは 30 秒以内に反映される**（再起動は要らない）。
- **秘密（SMTP パスワード・Pleasanter の共通 API キー）は暗号化して保管し**（`ATOMFEED_SECRET_KEY`）、**画面へは返さない**（登録済みかどうかだけ）。保存するとき、空なら変えない。`null` なら消す。
  ⚠️ **秘密の鍵を失うと保管した秘密は復号できない**: 起動は止まらず、その設定は空として扱われ、画面で再入力を求める。
- 変えた設定の**名前だけ**を監査ログへ残す（値・秘密は残さない）。

| 分類 | 設定（環境変数名） |
| --- | --- |
| サービス | `ATOMFEED_PUBLIC_BASEURL`、`ATOMFEED_TIMEZONE_DEFAULT` |
| Pleasanter | `ATOMFEED_PLEASANTER_BASEURL`・`_APIKEY`（秘密）・`_APIVERSION`・`_TIMEOUTSECONDS`・`_TIMEZONE`・`_LINKBASEURL` |
| フィード | `ATOMFEED_FEED_CACHESECONDS`・`_MAXENTRIES`・`_PAGESIZE`・`_FAILURENOTIFY`・`_FAILURENOTIFYTHRESHOLD`・`_FAILURENOTIFYREPEATHOURS`（取得の失敗の通知） |
| セキュリティ | `ATOMFEED_ADMIN_TWOFACTOR`、`ATOMFEED_PASSWORD_MINIMUMLENGTH`・`_ALLOWSAMEASLOGINID`、`ATOMFEED_AUDITLOG_RETENTION_DAYS` |
| SAML ログイン | `ATOMFEED_SAML_ENABLED`・`_ENTITYID`・`_IDPENTITYID`・`_SINGLESIGNONURL`・`_IDPCERTIFICATE`・`_UNKNOWNUSER`・`_REGISTERROLE`・`_LOGINIDSOURCE`・`_LOGINIDCLAIM`・`_BUTTONLABEL`・`_SINGLELOGOUTURL` |
| Pleasanter のログイン | `ATOMFEED_PLEASANTERSSO_ENABLED`・`_INTERNALBASEURL`・`_LOGINURL`・`_LOGOUTURL`・`_COOKIENAMES`・`_ALLOWEDDEPTIDS`・`_ALLOWEDGROUPIDS`・`_UNKNOWNUSER`・`_REGISTERROLE`・`_REVALIDATEMINUTES`・`_TIMEOUTSECONDS`・`_BUTTONLABEL` |
| メール | `ATOMFEED_MAIL_ENABLED`・`_TRANSPORT`・`_SMTP_HOST`・`_SMTP_PORT`・`_SMTP_SECURITY`・`_SMTP_USER`・`_SMTP_PASSWORD`（秘密）・`_FROM_ADDRESS`・`_FROM_NAME`・`_REPLYTO_ADDRESS`・`_SES_REGION`・`_ACS_ENDPOINT`・`_TIMEOUT_SECONDS`・`_SEND_PER_MINUTE` |

**SAML・Pleasanter のログインの設定をファイルで固定するとき**は、`Authentication.json` の `Saml{}` / `PleasanterSso{}` に書く
（環境変数の正式名 `ATOMFEED_SAML_ENABLED` などを、どのファイルへそのまま書いても効く）。環境変数で与えるのが普通で、複数行の証明書（`Saml:IdpCertificate`）はファイルの方が書きやすい。
実値を含むときは、読む対象のファイル（`Service` / `Pleasanter` / `Feed` / `Rds` / `Security` / `Authentication` / `Mail`）の `.local.json` に書く
（上の一覧に無い名前のファイルは読まれない）。

**画面から編集しない設定（起動に要る、またはインスタンスごとの設定）**は、これまでどおりファイル／環境変数だけ:
`ATOMFEED_DB_*`（接続先・自動マイグレーション）、`ATOMFEED_SECRET_KEY`、`ATOMFEED_FORWARDED_PROXY_NETWORKS`、`PasswordPolicies`（正規表現の条件）、`ATOMFEED_MAIL_WORKER`（`Mail.json` の `Worker`。この台で送信ワーカーを動かすか）、`ATOMFEED_PARAMETERS_DIRECTORY`。

## 値をファイルから読む（`*_FILE`）

**どの設定も、`ATOMFEED_X` の代わりに `ATOMFEED_X_FILE=<パス>` でファイルの中身を値にできる**（Docker secrets・Kubernetes の Secret のマウント）。
値が環境変数に載らない。末尾の改行は 1 つ落とす。**直接の値と `_FILE` の両方があれば、ファイルが読めない・空のときも、起動時に落ちる。**
優先順位は環境変数と同じ位置。手順は [`導入-更新運用手順書.md`](../../../../_documents/導入-更新運用手順書.md) の「値をファイルから読む」。

## ファイルと環境変数

ファイルのキーは Pleasanter 本体に合わせた短い名前で、環境変数は `ATOMFEED_*`。
**読み込み時に下表のとおり写している**ので、どちらで書いても効く。

| ファイル | キー | 環境変数 | 内容 |
| --- | --- | --- | --- |
| `Service.json` | `TimeZoneDefault` | `ATOMFEED_TIMEZONE_DEFAULT` | 既定のタイムゾーン（既定 `Asia/Tokyo`） |
| `Service.json` | `PublicBaseUrl` | `ATOMFEED_PUBLIC_BASEURL` | 本アプリの公開 URL（購読 URL の基点） |
| `Pleasanter.json` | `BaseUrl` | `ATOMFEED_PLEASANTER_BASEURL` | 接続先 Pleasanter の URL |
| `Pleasanter.json` | `ApiKey` | `ATOMFEED_PLEASANTER_APIKEY` | API キー。**実値は書かない** |
| `Pleasanter.json` | `ApiVersion` | `ATOMFEED_PLEASANTER_APIVERSION` | API バージョン（既定 1.1。読めない値は既定へ） |
| `Pleasanter.json` | `TimeoutSeconds` | `ATOMFEED_PLEASANTER_TIMEOUTSECONDS` | 呼び出しのタイムアウト（既定 30） |
| `Pleasanter.json` | `ApiKeyUserTimeZoneId` | `ATOMFEED_PLEASANTER_TIMEZONE` | API キーの利用者のタイムゾーン。未設定なら `TimeZoneDefault`。**運用開始後に変えない** |
| `Pleasanter.json` | `LinkBaseUrl` | `ATOMFEED_PLEASANTER_LINKBASEURL` | 記事に付けるレコードへのリンクの基点 |
| `Feed.json` | `CacheSeconds` | `ATOMFEED_FEED_CACHESECONDS` | 取得した記事を使い回す秒数（既定 300） |
| `Feed.json` | `MaxEntries` | `ATOMFEED_FEED_MAXENTRIES` | 1 フィードの記事の上限（既定 1000） |
| `Feed.json` | `PageSize` | `ATOMFEED_FEED_PAGESIZE` | Pleasanter へ求める 1 ページの件数（既定 1000） |
| `Feed.json` | `FailureNotify:Enabled` | `ATOMFEED_FEED_FAILURENOTIFY` | 取得の失敗が続いたらメールで知らせる（既定 `false`） |
| `Feed.json` | `FailureNotify:Threshold` | `ATOMFEED_FEED_FAILURENOTIFYTHRESHOLD` | 知らせる連続の失敗の回数（既定 3） |
| `Feed.json` | `FailureNotify:RepeatHours` | `ATOMFEED_FEED_FAILURENOTIFYREPEATHOURS` | 失敗が続くときの再通知の間隔（時間。既定 24） |
| `Rds.json` | `Provider` | `ATOMFEED_DB_PROVIDER` | `Sqlite`（既定。簡易セットアップとデバッグ用）/ `SqlServer` / `PostgreSql` / `MySql` |
| `Rds.json` | `ConnectionString` | `ATOMFEED_DB_CONNECTIONSTRING` | 本アプリの DB（Pleasanter の DB とは別）。**実値は書かない** |
| `Security.json` | `SecretKey` | `ATOMFEED_SECRET_KEY` | フィードごとの API キーと管理者の TOTP の共有鍵を DB へ暗号化して置く鍵（Base64・32 バイト）。**実値は書かない。** `dotnet run -- --generate-secret-key` で作る。**失うと保管した API キーは復号できない** |
| `Security.json` | `SecretKeyAutoGenerate` | `ATOMFEED_SECRET_KEY_AUTOGENERATE` | `SecretKey` が無いとき、初回の起動で作って `Security.local.json` へ保存するか（既定 `true`）。保存できなければ作らない。**本番では、作られた鍵を環境変数へ移す** |
| `Security.json` | `AdminTwoFactor` | `ATOMFEED_ADMIN_TWOFACTOR` | 管理者の 2 要素。`required`（登録するまでログインが完了しない）/ `optional`（既定）/ `disabled`（登録の口を閉じる。登録済みの人からは外さない）。**知らない値は起動時に落とす** |
| `Security.json` | `AuditLogRetentionDays` | `ATOMFEED_AUDITLOG_RETENTION_DAYS` | 監査ログを残す日数（既定 365。`0` なら消さない。読めない値は既定へ） |
| `Security.json` | `ForwardedProxyNetworks` | `ATOMFEED_FORWARDED_PROXY_NETWORKS` | 転送ヘッダ（`X-Forwarded-*`）を信頼するプロキシのネットワーク（カンマ区切りの CIDR）。**未設定なら誰も信頼しない**（プロキシ配下ではレート制限が全員で共有になる）。CIDR でない値は起動時に落とす |
| `Mail.json` | `Enabled` | `ATOMFEED_MAIL_ENABLED` | 招待メールの送信を有効にするか（既定 `false`） |
| `Mail.json` | `Provider` | `ATOMFEED_MAIL_TRANSPORT` | 送信経路。`Smtp`（既定）/ `AmazonSes` / `AzureCommunicationServices` |
| `Mail.json` | `FromAddress` / `FromName` / `ReplyToAddress` | `ATOMFEED_MAIL_FROM_ADDRESS` / `_FROM_NAME` / `_REPLYTO_ADDRESS` | 差出人・返信先 |
| `Mail.json` | `SendTimeoutSeconds` / `SendPerMinute` | `ATOMFEED_MAIL_TIMEOUT_SECONDS` / `_SEND_PER_MINUTE` | 1 通あたりのタイムアウト・1 分あたりの送信数の上限 |
| `Mail.json` | `Worker` | `ATOMFEED_MAIL_WORKER` | この台で送信ワーカーを動かすか（既定 `true`） |
| `Mail.json` | `Smtp:Host` / `Smtp:Port` / `Smtp:Security` / `Smtp:User` / `Smtp:Password` | `ATOMFEED_MAIL_SMTP_HOST` / `_PORT` / `_SECURITY` / `_USER` / `_PASSWORD` | SMTP の接続先・ポート（既定 587）・暗号化（`StartTls` / `ImplicitTls` / `None`）・認証。**パスワードの実値は書かない** |
| `Mail.json` | `AwsSes:Region` | `ATOMFEED_MAIL_SES_REGION` | Amazon SES のリージョン（資格情報は IAM ロール） |
| `Mail.json` | `Acs:Endpoint` | `ATOMFEED_MAIL_ACS_ENDPOINT` | Azure Communication Services のエンドポイント（資格情報はマネージド ID） |
| `Authentication.json` | `Saml:Enabled` ほか（`Saml:*`） | `ATOMFEED_SAML_*` | SAML 2.0 のログイン。`EntityId` / `IdpEntityId` / `SingleSignOnUrl` / `SingleLogoutUrl` / `IdpCertificate` / `UnknownUser` / `RegisterRole` / `LoginIdSource` / `LoginIdClaim` / `ButtonLabel` |
| `Authentication.json` | `PleasanterSso:Enabled` ほか（`PleasanterSso:*`） | `ATOMFEED_PLEASANTERSSO_*` | Pleasanter のログイン。`InternalBaseUrl` / `LoginUrl` / `LogoutUrl` / `CookieNames` / `AllowedDeptIds` / `AllowedGroupIds` / `UnknownUser` / `RegisterRole` / `RevalidateMinutes` / `TimeoutSeconds` / `ButtonLabel` |
| `Security.json` | `PasswordMinimumLength` | `ATOMFEED_PASSWORD_MINIMUMLENGTH` | 管理者のパスワードの最低の長さ（既定 12） |
| `Security.json` | `PasswordPolicies` | `PasswordPolicies__0__Regex` など | パスワードの条件の正規表現（Pleasanter 本体と同じ書き方。部分一致・上から順・最初に合わなかった文言を返す）。**先読み・後方参照・原子グループは使えず、起動時に落とす**。後退戻りしない照合器＋100 ミリ秒の上限で動かす（ReDoS 対策） |
| `Security.json` | `PasswordAllowSameAsLoginId` | `ATOMFEED_PASSWORD_ALLOWSAMEASLOGINID` | ログイン ID と同じパスワードを許すか（既定 false） |

> **`ATOMFEED_SECRET_KEY` は環境変数で保持する**（Azure App Service のアプリケーション設定・Key Vault の参照など、
> デプロイ先の秘密の保管先へ）。`Security.local.json` はローカル開発用。失うと保管した API キーを復号できないため、
> コンテナやディスクが作り直されても残る場所に置く。

> ⚠️ **値が `null` や空のキーは無視する。** `ApiKey` は既定で `null`（ここへ書かせない）なので、
> 写すと**環境変数で与えた API キーを空で塗り潰してしまう。**

> ⚠️ **環境変数の名前を `Pleasanter__BaseUrl` のような .NET の階層形式で与えても効かない。**
> 上表の `ATOMFEED_*` を使う。
