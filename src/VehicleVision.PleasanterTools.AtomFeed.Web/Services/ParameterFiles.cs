using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Services;

/// <summary>
/// <c>App_Data/Parameters/*.json</c> を設定として積む。
/// </summary>
/// <remarks>
/// <para>
/// **Pleasanter 本体・Questionnaire と同じ置き場・同じ書き方**にしてある
/// （<c>App_Data/Parameters/README.md</c>）。**優先順位は
/// <c>{名前}.local.json</c> ＞ 環境変数 ＞ <c>{名前}.json</c>。**
/// </para>
/// <para>
/// **1 か所にまとめてある。** 設定ごとに「JSON → 環境変数」を繰り返すと、
/// 後から積んだ環境変数が**前の <c>.local.json</c> を追い越す**（Questionnaire で踏んだ）。
/// </para>
/// <para>
/// ⚠️ **ファイルのキーと環境変数の名前が違うものがある。** ファイルは Pleasanter 本体に合わせた短い名前
/// （<c>BaseUrl</c> など）、環境変数は衝突を避けて <c>ATOMFEED_*</c>。そのままでは片方しか効かないので、
/// <see cref="Aliases"/> で正式な名前（環境変数と同じ）へ写している。
/// </para>
/// <para>
/// **値が <c>null</c> や空のキーは写さない。** <c>ApiKey</c> は既定で <c>null</c>（＝ここへ書かせない）なので、
/// 写すと環境変数で与えた API キーを空で塗り潰してしまう。
/// </para>
/// </remarks>
public static class ParameterFiles
{
    /// <summary>既定の置き場（content root からの相対）。</summary>
    public const string DefaultDirectory = "App_Data/Parameters";

    /// <summary>実際に読んだ置き場。**起動時の記録に出す。**</summary>
    public const string DirectoryKey = "ATOMFEED_PARAMETERS_DIRECTORY";

    /// <summary>実際に見つかったファイル。**空なら 1 つも読めていない。**</summary>
    /// <remarks>
    /// ⚠️ **「読めているつもりで読めていない」を起動時に見せるため。**
    /// 設定ファイルは <c>optional</c> なので、置き場を間違えても黙って既定で動いてしまう。
    /// </remarks>
    public const string FoundFilesKey = "ATOMFEED_PARAMETERS_FOUND";

    /// <summary>読む対象（Pleasanter の <c>App_Data/Parameters</c> に倣った、関心ごとのファイル）。</summary>
    private static readonly ImmutableArray<string> Names =
        ["Service", "Pleasanter", "Feed", "Rds", "Security", "Authentication", "Mail"];

    /// <summary>ファイル名 → （ファイル内のキーの階層 → 正式な設定名＝環境変数と同じ名前）。</summary>
    /// <remarks>
    /// <para>
    /// **ファイルごとに写す。** Pleasanter と同じく、ファイルごとに同じ名前のキー（<c>Provider</c> や <c>Enabled</c>）を
    /// 持てるようにするため（全部を 1 つに混ぜると衝突する）。入れ子は <c>:</c> で辿る（<c>Smtp:Host</c>）。
    /// </para>
    /// <para>**ここに無いキーはそのままの名前で積まれる**（読まれない）。</para>
    /// </remarks>
    private static readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> Aliases =
        new Dictionary<string, ImmutableDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Service"] = Map(
                ("TimeZoneDefault", AppSettings.TimeZoneDefault),
                ("PublicBaseUrl", AppSettings.PublicBaseUrl)),

            ["Pleasanter"] = Map(
                ("BaseUrl", AppSettings.PleasanterBaseUrl),
                ("ApiKey", AppSettings.PleasanterApiKey),
                ("ApiVersion", AppSettings.PleasanterApiVersion),
                ("TimeoutSeconds", AppSettings.PleasanterTimeoutSeconds),
                ("ApiKeyUserTimeZoneId", AppSettings.PleasanterTimeZone),
                ("LinkBaseUrl", AppSettings.PleasanterLinkBaseUrl)),

            ["Feed"] = Map(
                ("CacheSeconds", AppSettings.FeedCacheSeconds),
                ("MaxEntries", AppSettings.FeedMaxEntries),
                ("PageSize", AppSettings.FeedPageSize),
                ("FailureNotify:Enabled", AppSettingDefinitions.FeedFailureNotify),
                ("FailureNotify:Threshold", AppSettingDefinitions.FeedFailureNotifyThreshold),
                ("FailureNotify:RepeatHours", AppSettingDefinitions.FeedFailureNotifyRepeatHours)),

            ["Rds"] = Map(
                ("Provider", AppSettings.DbProvider),
                ("ConnectionString", AppSettings.DbConnectionString),
                ("AutoMigrate", AppSettings.DbAutoMigrate)),

            ["Security"] = Map(
                ("AdminTwoFactor", AppSettings.AdminTwoFactor),
                ("PasswordMinimumLength", AppSettings.PasswordMinimumLength),
                ("PasswordAllowSameAsLoginId", AppSettings.PasswordAllowSameAsLoginId),
                ("SecretKey", AppSettings.SecretKey),
                ("SecretKeyAutoGenerate", SecretKeyBootstrap.AutoGenerateKey),
                ("AuditLogRetentionDays", AppSettings.AuditLogRetentionDays),
                ("ForwardedProxyNetworks", AppSettings.ForwardedProxyNetworks)),

            // Pleasanter の Authentication.json に倣う。SAML と Pleasanter のログインをここへ置く
            ["Authentication"] = Map(
                ("Saml:Enabled", AppSettingDefinitions.SamlEnabled),
                ("Saml:EntityId", AppSettingDefinitions.SamlEntityId),
                ("Saml:IdpEntityId", AppSettingDefinitions.SamlIdpEntityId),
                ("Saml:SingleSignOnUrl", AppSettingDefinitions.SamlSingleSignOnUrl),
                ("Saml:SingleLogoutUrl", AppSettingDefinitions.SamlSingleLogoutUrl),
                ("Saml:IdpCertificate", AppSettingDefinitions.SamlIdpCertificate),
                ("Saml:UnknownUser", AppSettingDefinitions.SamlUnknownUser),
                ("Saml:RegisterRole", AppSettingDefinitions.SamlRegisterRole),
                ("Saml:LoginIdSource", AppSettingDefinitions.SamlLoginIdSource),
                ("Saml:LoginIdClaim", AppSettingDefinitions.SamlLoginIdClaim),
                ("Saml:ButtonLabel", AppSettingDefinitions.SamlButtonLabel),
                ("PleasanterSso:Enabled", AppSettingDefinitions.PleasanterSsoEnabled),
                ("PleasanterSso:InternalBaseUrl", AppSettingDefinitions.PleasanterSsoInternalBaseUrl),
                ("PleasanterSso:LoginUrl", AppSettingDefinitions.PleasanterSsoLoginUrl),
                ("PleasanterSso:LogoutUrl", AppSettingDefinitions.PleasanterSsoLogoutUrl),
                ("PleasanterSso:CookieNames", AppSettingDefinitions.PleasanterSsoCookieNames),
                ("PleasanterSso:AllowedDeptIds", AppSettingDefinitions.PleasanterSsoAllowedDeptIds),
                ("PleasanterSso:AllowedGroupIds", AppSettingDefinitions.PleasanterSsoAllowedGroupIds),
                ("PleasanterSso:UnknownUser", AppSettingDefinitions.PleasanterSsoUnknownUser),
                ("PleasanterSso:RegisterRole", AppSettingDefinitions.PleasanterSsoRegisterRole),
                ("PleasanterSso:RevalidateMinutes", AppSettingDefinitions.PleasanterSsoRevalidateMinutes),
                ("PleasanterSso:TimeoutSeconds", AppSettingDefinitions.PleasanterSsoTimeoutSeconds),
                ("PleasanterSso:ButtonLabel", AppSettingDefinitions.PleasanterSsoButtonLabel)),

            // Pleasanter の Mail.json に倣い、経路ごとの設定（Smtp / AwsSes / Acs）を入れ子にする
            ["Mail"] = Map(
                ("Enabled", "ATOMFEED_MAIL_ENABLED"),
                ("Provider", "ATOMFEED_MAIL_TRANSPORT"),
                ("FromAddress", "ATOMFEED_MAIL_FROM_ADDRESS"),
                ("FromName", "ATOMFEED_MAIL_FROM_NAME"),
                ("ReplyToAddress", "ATOMFEED_MAIL_REPLYTO_ADDRESS"),
                ("SendTimeoutSeconds", "ATOMFEED_MAIL_TIMEOUT_SECONDS"),
                ("SendPerMinute", MailSenderOptions.MaxSendsPerMinuteKey),
                ("Worker", MailSenderOptions.WorkerKey),
                ("Smtp:Host", "ATOMFEED_MAIL_SMTP_HOST"),
                ("Smtp:Port", "ATOMFEED_MAIL_SMTP_PORT"),
                ("Smtp:Security", "ATOMFEED_MAIL_SMTP_SECURITY"),
                ("Smtp:User", "ATOMFEED_MAIL_SMTP_USER"),
                ("Smtp:Password", "ATOMFEED_MAIL_SMTP_PASSWORD"),
                ("AwsSes:Region", "ATOMFEED_MAIL_SES_REGION"),
                ("Acs:Endpoint", "ATOMFEED_MAIL_ACS_ENDPOINT")),
        }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

    private static ImmutableDictionary<string, string> Map(params (string Path, string Setting)[] pairs) =>
        pairs.ToImmutableDictionary(p => p.Path, p => p.Setting, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 設定を積む。**アプリの一番先で 1 度だけ呼ぶ**（以降の読み取りが全てこれを見る）。
    /// </summary>
    public static IConfigurationBuilder AddParameterFiles(
        this IConfigurationBuilder builder,
        string directory = DefaultDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        // ⚠️ **絶対パスへ直してから積む。** AddJsonFile は content root を基準にするが、
        // 別名を写すために自分で読む側は現在のディレクトリを基準にする。基準が食い違うと片方だけ読めて気付けない
        var resolved = Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(BasePathOf(builder), directory);

        // ---- 1. {名前}.json（一番弱い）
        foreach (var name in Names)
        {
            builder.AddJsonFile(Path.Combine(resolved, $"{name}.json"), optional: true, reloadOnChange: false);
        }

        builder.AddInMemoryCollection(AliasesOf(resolved, local: false));

        // ---- 2. 環境変数（と、Docker secrets などの `{名前}_FILE`＝値を読むファイルの置き場）
        builder.AddEnvironmentVariables();
        builder.AddInMemoryCollection(SecretFiles(EnvironmentOf().AsReadOnly()));

        // ---- 3. {名前}.local.json（一番強い。git 管理外）
        foreach (var name in Names)
        {
            builder.AddJsonFile(Path.Combine(resolved, $"{name}.local.json"), optional: true, reloadOnChange: false);
        }

        builder.AddInMemoryCollection(AliasesOf(resolved, local: true));

        // ---- 4. 何を読んだかを残す
        return builder.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(DirectoryKey, resolved),
            new KeyValuePair<string, string?>(FoundFilesKey, string.Join(" / ", FoundIn(resolved))),
        ]);
    }

    /// <summary>値をファイルから読むときの、環境変数の名前の末尾（<c>ATOMFEED_SECRET_KEY_FILE</c> など）。</summary>
    public const string FileSuffix = "_FILE";

    /// <summary>環境変数の接頭辞。<c>ATOMFEED_</c> で始まるものだけを対象にする。</summary>
    private const string EnvironmentPrefix = "ATOMFEED_";

    /// <summary>秘密のファイルに許す最大サイズ（Questionnaire と同じ 64 KiB）。</summary>
    public const int MaximumFileSizeBytes = 64 * 1024;

    /// <summary>プロセスの環境変数（名前 → 値）。</summary>
    private static Dictionary<string, string?> EnvironmentOf() =>
        Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value as string, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <c>ATOMFEED_X_FILE=/run/secrets/x</c> の形で、<c>ATOMFEED_X</c> の値を**ファイルから読む**（Docker secrets・Kubernetes の Secret のマウントなど）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **値は環境変数に載せずに済む**（<c>docker inspect</c> や <c>/proc/…/environ</c> に出ない）。
    /// **Questionnaire（<c>QUESTIONNAIRE_*_FILE</c>）・IdentityProvider（<c>IDP_*_FILE</c>）と同じ挙動にそろえてある。**
    /// </para>
    /// <list type="bullet">
    /// <item>末尾の改行（<c>\n</c> か <c>\r\n</c> を 1 つ）だけを落とす。複数行（証明書など）はそのまま読める</item>
    /// <item>**絶対パスだけ**を受ける（相対パスは、起動した場所で指す先が変わる）</item>
    /// <item>**64 KiB を超えるファイルは受けない**（秘密ではなく、別のファイルを指している）</item>
    /// <item>**同じ設定に <c>ATOMFEED_X</c> と <c>ATOMFEED_X_FILE</c> の両方があれば、起動時に落とす**（どちらが効くか分からないため）</item>
    /// <item>ファイルが読めない・空のときも落とす（**黙って未設定で動くと、鍵の自動生成などが働いて気付けない**）</item>
    /// <item>**値は例外のメッセージへ出さない。** パスだけを出す</item>
    /// </list>
    /// <para>設定の優先順位は、環境変数と同じ位置（<c>{名前}.local.json</c> より弱く、<c>{名前}.json</c> より強い）。</para>
    /// </remarks>
    /// <param name="environment">環境変数（名前 → 値）。</param>
    /// <exception cref="InvalidOperationException">上のいずれかに当たるとき。</exception>
    public static IEnumerable<KeyValuePair<string, string?>> SecretFiles(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var (name, path) in environment)
        {
            if (!name.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)
                || name.Length <= EnvironmentPrefix.Length + FileSuffix.Length
                || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var setting = name[..^FileSuffix.Length];
            if (environment.TryGetValue(setting, out var direct) && direct is not null)
            {
                throw new InvalidOperationException(
                    $"環境変数 '{setting}' と '{name}' は同時に指定できません。");
            }

            yield return new KeyValuePair<string, string?>(setting, ReadSecret(name, path));
        }
    }

    private static string ReadSecret(string name, string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"環境変数 '{name}' には絶対パスを指定してください。");
        }

        string value;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                throw new InvalidOperationException($"環境変数 '{name}' のファイルが見つかりません: {path}");
            }

            if (file.Length > MaximumFileSizeBytes)
            {
                throw new InvalidOperationException($"環境変数 '{name}' のファイルが大きすぎます（上限 {MaximumFileSizeBytes / 1024} KiB）: {path}");
            }

            value = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 例外のメッセージは出さない（値・パスの余計な情報を避ける）
            throw new InvalidOperationException($"環境変数 '{name}' のファイルを読み取れません: {path}");
        }

        // 末尾の改行を 1 つだけ落とす（echo やエディタが付ける）
        if (value.EndsWith("\r\n", StringComparison.Ordinal))
        {
            value = value[..^2];
        }
        else if (value.EndsWith('\n'))
        {
            value = value[..^1];
        }

        return value.Length == 0
            ? throw new InvalidOperationException($"環境変数 '{name}' のファイルは空です: {path}")
            : value;
    }

    /// <summary>実際に置いてあるファイルの名前。</summary>
    public static IEnumerable<string> FoundIn(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        foreach (var name in Names)
        {
            foreach (var suffix in new[] { ".json", ".local.json" })
            {
                if (File.Exists(Path.Combine(directory, name + suffix)))
                {
                    yield return name + suffix;
                }
            }
        }
    }

    /// <summary>相対パスの基準。**content root に合わせる。**</summary>
    private static string BasePathOf(IConfigurationBuilder builder) =>
        builder.Properties.TryGetValue("FileProvider", out var provider)
            && provider is Microsoft.Extensions.FileProviders.PhysicalFileProvider physical
            ? physical.Root
            : Directory.GetCurrentDirectory();

    /// <summary>ファイルのキーを正式な名前へ写したものを作る。</summary>
    /// <remarks>
    /// **写す前に一度だけ読む。** 積んだ後の設定から読むと、環境変数の値まで巻き込んで写してしまう。
    /// **ファイルごとに別々に読む**（同じ名前のキーが別のファイルにあっても混ざらない）。
    /// </remarks>
    [SuppressMessage(
        "Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "IConfigurationRoot は読み終えた時点で用済み")]
    private static IEnumerable<KeyValuePair<string, string?>> AliasesOf(string directory, bool local)
    {
        var suffix = local ? ".local.json" : ".json";
        foreach (var name in Names)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(directory, $"{name}{suffix}"), optional: true, reloadOnChange: false)
                .Build();

            foreach (var (path, setting) in Aliases[name])
            {
                var value = configuration[path];

                // **null と空は写さない。** 環境変数で与えた値を塗り潰さないため
                if (!string.IsNullOrEmpty(value))
                {
                    yield return new KeyValuePair<string, string?>(setting, value);
                }
            }
        }
    }
}
