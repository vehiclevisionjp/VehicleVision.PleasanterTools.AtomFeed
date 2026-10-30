using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;
using VehicleVision.PleasanterTools.AtomFeed.Data;
using VehicleVision.PleasanterTools.Core.Mail;
using VehicleVision.PleasanterTools.Core.Web;
using SecretProtector = VehicleVision.PleasanterTools.AtomFeed.Security.SecretProtector;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;
using VehicleVision.PleasanterTools.AtomFeed.Pleasanter;
using VehicleVision.PleasanterTools.AtomFeed.Security;
using VehicleVision.PleasanterTools.AtomFeed.Admin;
using VehicleVision.PleasanterTools.AtomFeed.Web;
using VehicleVision.PleasanterTools.AtomFeed.Web.Endpoints;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

// **設定に書く鍵を作るための道。** 手で乱数を用意させると、短い値や使い回しが混ざる。
// コンソールへ出す文字列なので英語（.github/copilot-instructions.md）
if (args.Contains("--generate-secret-key"))
{
    Console.WriteLine(SecretProtector.GenerateKey());
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ---- App_Data/Parameters の設定ファイル ----------------------------------------
// **一番先に積む。** 以降の読み取りがすべてこれを見る。
// 優先順位は {名前}.local.json ＞ 環境変数 ＞ {名前}.json（App_Data/Parameters/README.md）
builder.Configuration.AddParameterFiles();

// ---- 秘密の鍵が無ければ、初回の起動で作って Security.local.json へ保存する（保存できなければ作らない） ----
var generatedKeyFile = SecretKeyBootstrap.EnsureKey(builder.Configuration, builder.Configuration[ParameterFiles.DirectoryKey]!);

// ---- 設定は DI の生成時に読む（テストからの上書きを効かせるため） ---------------
// ---- アプリケーション設定（管理画面から編集できる設定。Questionnaire に則る） -------------------------
// 優先順位は 外部設定（環境変数・Parameters。固定）＞ DB ＞ 既定値。使う側は、使うたびに RuntimeSettings.Current を読む。
// **不正な外部設定は、ここで（最初に RuntimeSettings を作るとき）起動を止める**
builder.Services.AddSingleton<IAppSettingStore, AppSettingStore>();
builder.Services.AddSingleton<IAppSettingsProvider, AppSettingsProvider>();
builder.Services.AddSingleton<RuntimeSettings>();
builder.Services.AddSingleton<IPleasanterOptionsSource>(sp => sp.GetRequiredService<RuntimeSettings>());
builder.Services.AddSingleton<AppSettingsMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AppSettingsMonitor>());
// エンドポイントの引数で受ける設定は、要求ごとに最新の値を渡す
builder.Services.AddTransient(sp => sp.GetRequiredService<RuntimeSettings>().Current.Pleasanter);
builder.Services.AddTransient(sp => sp.GetRequiredService<RuntimeSettings>().Current.Feed);
builder.Services.AddTransient(sp => sp.GetRequiredService<RuntimeSettings>().Current.AdminAuth);
builder.Services.AddTransient(sp => sp.GetRequiredService<RuntimeSettings>().Current.Mail);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PleasanterClient>();
builder.Services.AddSingleton<FeedFailureNotifier>();
builder.Services.AddSingleton<FeedService>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var provider = AtomFeedDatabase.Instance.ParseProvider(AppSettings.Text(configuration, AppSettings.DbProvider), DatabaseProvider.Sqlite);
    return new DatabaseConnectionInfo(
        provider,
        AtomFeedDatabase.Instance.ResolveConnectionString(
            provider,
            AppSettings.Text(configuration, AppSettings.DbConnectionString),
            sp.GetRequiredService<IHostEnvironment>().ContentRootPath));
});
builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    var database = sp.GetRequiredService<DatabaseConnectionInfo>();
    return new DbConnectionFactory(database.Provider, database.ConnectionString);
});
builder.Services.AddSingleton(sp => AppSettings.ReadSecretProtector(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IFeedStore, FeedStore>();
builder.Services.AddSingleton<ISecretCipher, SecretCipherAdapter>();
builder.Services.AddSingleton<IAdminUserStore>(sp => new AdminUserStore(
    sp.GetRequiredService<IDbConnectionFactory>(),
    sp.GetRequiredService<ISecretCipher>(),
    sp.GetRequiredService<TimeProvider>(),
    AtomFeedDatabase.Instance.AppId));
builder.Services.AddSingleton<IAdminSessionStore, AdminSessionStore>();
builder.Services.AddSingleton<IAuditLogStore, AuditLogStore>();
builder.Services.AddSingleton<IAdminInvitationStore, AdminInvitationStore>();

// ---- メール（招待）。送信待ちへ暗号化して積み、アプリの中のワーカーが送る ----------------------
// **無効（既定）なら何も送らない。** 招待は画面の URL を手で渡す。設定は管理画面から編集できる
builder.Services.AddSingleton<IMailOutbox, MailOutbox>();
builder.Services.AddSingleton<IRateLimitStore, RateLimitStore>();
builder.Services.AddSingleton<SharedRateLimiter>();
builder.Services.AddSingleton<RateLimitPurgeService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RateLimitPurgeService>());
builder.Services.AddSingleton<IMailPayloadProtector, MailPayloadProtector>();
builder.Services.AddSingleton<IMailTransport, ConfiguredMailTransport>();
builder.Services.AddSingleton<MailSender>();
builder.Services.AddSingleton<AdminInvitationMailer>();
builder.Services.AddHostedService<MailSenderHostedService>();
builder.Services.AddSingleton<AuditLogService>();
builder.Services.AddSingleton<AuditLogRetentionService>();
builder.Services.AddHostedService<AuditLogRetentionWorker>();
builder.Services.AddSingleton<AdminSessionFilter>();
builder.Services.AddSingleton<SamlAuthenticator>();
builder.Services.AddSingleton<PleasanterSsoAuthenticator>();
// ---- Pleasanter のログインの本人確認（Core.Web。Issue #47） ----
// HttpClient（⚠️ cookie を覚えない・転送を追わない・時間切れは要求ごとに設定の値で掛ける）と検証器は Core.Web が登録する。
// 本アプリの値は今までと同じものを渡す（sessions/set の鍵・ログで案内する設定名・ログのカテゴリ）
builder.Services.AddSingleton<IPleasanterApiConnectionSource, RuntimePleasanterApiConnectionSource>();
builder.Services.AddPleasanterSessionVerifier(new PleasanterSessionVerifierSettings
{
    SessionProbeKey = "VehicleVision.AtomFeed.SsoProbe",
    BaseUrlSettingName = AppSettingDefinitions.PleasanterBaseUrl,
    ApiKeySettingName = AppSettingDefinitions.PleasanterApiKey,
    LoggerCategory = "VehicleVision.PleasanterTools.AtomFeed.Web.Services.PleasanterSessionVerifier",
});
builder.Services.AddSingleton<PleasanterSsoSessionRevalidator>();

// **購読 URL の総当たりを遅くする。** トークンは 256 bit なので当たらないが、無駄な負荷も止める
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // **ログインの試行だけ別枠**（IP ごと 5 分に 10 回）。全体の枠に紛れさせると総当たりが通る
    // **ログインの試行・Pleasanter のログインの確認の枠は、複数台で数えを共有する**（SharedRateLimits。DB の数え。CalendarFeed の Issue #49）。
    // ここに残るのは、要求が多く DB への書き込みが重い購読 URL の枠（台ごとの数え）
    o.AddPolicy("feed", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// 設定の読み間違い（外部設定）は、最初の要求ではなく起動時に分かるようにする
var runtimeSettings = app.Services.GetRequiredService<RuntimeSettings>();

// ⚠️ **何を読んだかを残す。** 設定ファイルは optional なので、置き場を間違えても黙って既定で動いてしまう
app.Logger.LogInformation(
    "設定ファイルの置き場: {Directory} / 見つかったファイル: {Files}",
    app.Configuration[ParameterFiles.DirectoryKey],
    app.Configuration[ParameterFiles.FoundFilesKey]);
if (generatedKeyFile is not null)
{
    app.Logger.LogWarning(
        "秘密の鍵（ATOMFEED_SECRET_KEY）が無かったので作り、{File} へ保存しました。本番では、この値をデプロイ先の秘密の保管先へ移してください（失うと保管した API キーを復号できません）。",
        generatedKeyFile);
}

if (string.IsNullOrEmpty(app.Configuration[ParameterFiles.FoundFilesKey]))
{
    app.Logger.LogWarning("設定ファイルが 1 つも見つかりません。置き場を確かめてください。");
}

// ---- DB のマイグレーション ---------------------------------------------------------
// 起動時に自動で当てる（ATOMFEED_DB_AUTOMIGRATE=false で無効。その場合は手で当てる）
var db = app.Services.GetRequiredService<DatabaseConnectionInfo>();
app.Logger.LogInformation("DB: {Provider}", db.Provider);

// マイグレーションの適用・状態確認だけで終わる（AKS の起動前フック・自動適用を止めた導入先のため）
if (MigrationCommand.IsRequested(args))
{
    Environment.ExitCode = await MigrationCommand.RunAsync(
        AtomFeedDatabase.Instance.Migrator, db.Provider, db.ConnectionString, args,
        lockTimeout: AppDatabase.DefaultMigrationLockTimeout);
    return;
}

if (AppSettings.ReadAutoMigrate(app.Configuration))
{
    await AtomFeedDatabase.Instance.Migrator.MigrateUpWithLockAsync(db.Provider, db.ConnectionString, AppDatabase.DefaultMigrationLockTimeout);

    // ⚠️ **自動で作った鍵は、DB に管理者もフィードも無いときだけ使ってよい。**
    // 鍵を失ったまま作り直すと、DB に暗号化して置いた API キーや TOTP の共有鍵を復号できない
    if (generatedKeyFile is not null
        && (await app.Services.GetRequiredService<IAdminUserStore>().CountAsync() > 0
            || (await app.Services.GetRequiredService<IFeedStore>().ListAsync()).Count > 0))
    {
        SecretKeyBootstrap.Discard(generatedKeyFile);
        throw new InvalidOperationException(
            "ATOMFEED_SECRET_KEY was not set and a new key was generated, but the database already has administrators or feeds "
            + "encrypted with the original key. Set ATOMFEED_SECRET_KEY (or ATOMFEED_SECRET_KEY_FILE) to the original key. The generated key was discarded.");
    }
}
else
{
    app.Logger.LogWarning("起動時のマイグレーションは無効です。DB に表が無いと動きません。");
}

// ---- 転送ヘッダ（リバースプロキシの背後でクライアントの IP・スキーム・Host を見る） ------------
// ⚠️ **信頼するネットワークを設定で明示したときだけ有効にする。** 既定では誰も信頼しない
// （X-Forwarded-For を鵜呑みにすると、IP ごとのレート制限を回避できる）。**レート制限より前に置く**
var forwardedNetworks = AppSettings.ReadForwardedProxyNetworks(app.Configuration);
if (forwardedNetworks.Count > 0)
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    };

    // 既定で信頼されるループバックも外し、設定のネットワークだけにする
    forwarded.KnownProxies.Clear();
    forwarded.KnownIPNetworks.Clear();
    foreach (var network in forwardedNetworks)
    {
        forwarded.KnownIPNetworks.Add(network);
    }

    app.UseForwardedHeaders(forwarded);
}
else
{
    app.Logger.LogInformation(
        "転送ヘッダは無視します（{Setting} が未設定）。プロキシの背後ではレート制限が全員で共有になります。",
        AppSettings.ForwardedProxyNetworks);
}

// ---- 保存済みのアプリケーション設定を反映する（DB を使えるようになったので） ---------------------------
try
{
    runtimeSettings.Apply(await app.Services.GetRequiredService<IAppSettingsProvider>().ReloadAsync());
}
catch (Exception ex)
{
    // 表が無い（自動マイグレーションを止めている）・秘密の鍵の不整合など。起動は止めず、外部設定と既定値で動く
    app.Logger.LogWarning(ex, "保存済みのアプリケーション設定を読めませんでした。外部設定と既定値で動きます。");
}

app.UseAdminSecurityHeaders();
app.UseRateLimiter();

// ---- 生存確認・準備の確認（AKS のプローブ用。認証なし・レート制限なし） --------------------------
// `/healthz`: プロセスが応答できるか（DB は見ない。DB の一時的な不調で Pod を再起動させないため）。
// `/ready`: DB に繋がるか（繋がらない間は、負荷分散の対象から外す）。
// **ここで見る依存先は本アプリの DB だけ。** 例外の中身は接続先や資格情報を含み得るので応答へ出さない
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
    .DisableRateLimiting();
app.MapGet("/ready", async (DatabaseConnectionInfo database, CancellationToken cancellationToken) =>
{
    var failure = await AtomFeedDatabase.Instance.Migrator.WaitForDatabaseAsync(
        database.Provider, database.ConnectionString, TimeSpan.Zero, cancellationToken);
    return failure is null
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
})
    .DisableRateLimiting();

// ---- 購読フィード（Atom / RSS） ---------------------------------------------------
app.MapFeeds();

// ---- 管理者の認証・管理 API ----------------------------------------------------------
// 認証は Questionnaire の方式（ログイン ID ＋ パスワード ＋ TOTP・サーバ側セッション）。
// フィードの管理 API は、ログイン済みの管理者（Session）だけが使える
app.MapAdminAuth();
app.MapAdminFeeds();
app.MapAdminAudit();
app.MapAdminUsers();
app.MapAdminSettings();
app.MapAdminSaml();
app.MapAdminPleasanterSso();
app.MapAdminUi();

app.Run();

/// <summary>テストが <c>WebApplicationFactory</c> で起動するための宣言。</summary>
public partial class Program;
