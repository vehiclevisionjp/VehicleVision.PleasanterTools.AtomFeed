using VehicleVision.PleasanterTools.AtomFeed.Localization;
using System.Collections.Frozen;
using System.Globalization;
using System.Net.Mail;
using VehicleVision.PleasanterTools.AtomFeed.Admin;
using VehicleVision.PleasanterTools.AtomFeed.Mail;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Services;

/// <summary>設定値の型。</summary>
public enum AppSettingValueType
{
    String,
    Boolean,
    Integer,
}

/// <summary>設定のまとまり（管理画面の見出し）。</summary>
public static class AppSettingGroups
{
    public const string Service = "service";
    public const string Pleasanter = "pleasanter";
    public const string Feed = "feed";
    public const string Security = "security";
    public const string Mail = "mail";
    public const string Saml = "saml";
    public const string PleasanterSso = "pleasanterSso";
}

/// <summary>画面から検証に落ちた設定。メッセージはそのまま利用者へ返す。</summary>
public sealed class AppSettingValidationException : Exception
{
    /// <param name="messageKey">文言の鍵（<see cref="ServerMessageKeys"/>）。</param>
    /// <param name="args">埋め込みの値（<c>{0}</c> から順）。</param>
    public AppSettingValidationException(string messageKey, params object[] args)
        : base(ServerMessages.Get(messageKey, "ja", args))
    {
        MessageKey = messageKey;
        Args = args;
    }

    /// <summary>文言の鍵。管理 API が、要求の言語で返すのに使う（<see cref="Exception.Message"/> は日本語。ログ用）。</summary>
    public string MessageKey { get; }

    public object[] Args { get; }
}

/// <summary>画面から編集できる設定 1 つ分の定義。</summary>
/// <remarks>Questionnaire の <c>AppSettingDefinition</c> に則る。画面の見出し・説明は、画面側が <see cref="Key"/> で引く（多言語）。</remarks>
public sealed record AppSettingDefinition(
    string Key,
    string Group,
    AppSettingValueType Type,
    string DefaultValue,
    string Label,
    bool IsSecret = false,
    int? Minimum = null,
    int? Maximum = null,
    int? MaximumLength = null,
    IReadOnlyList<string>? AllowedValues = null,
    Func<string, string>? StringNormalizer = null,
    bool Multiline = false)
{
    /// <summary>入力を正規化する。範囲・形式が合わなければ <see cref="AppSettingValidationException"/>。</summary>
    public string Normalize(string? requested) => Normalize(requested, enforceRange: true);

    /// <summary>外部設定（環境変数・Parameters）の値を正規化する。<b>範囲は課さない</b>（検証環境の設定で起動できなくならないように）。</summary>
    public string NormalizeExternal(string? value) => Normalize(value, enforceRange: false);

    private string Normalize(string? requested, bool enforceRange)
    {
        var value = requested?.Trim() ?? string.Empty;
        return Type switch
        {
            AppSettingValueType.String => NormalizeString(value, enforceRange),
            AppSettingValueType.Boolean => bool.TryParse(value, out var b)
                ? (b ? "true" : "false")
                : throw new AppSettingValidationException(ServerMessageKeys.SettingBooleanInvalid, Key),
            AppSettingValueType.Integer => NormalizeInteger(value, enforceRange),
            _ => throw new InvalidOperationException($"未対応の設定型です: {Type}"),
        };
    }

    private string NormalizeString(string value, bool enforceRange)
    {
        if (enforceRange && MaximumLength is { } max && value.Length > max)
        {
            throw new AppSettingValidationException(ServerMessageKeys.SettingTooLong, Key, max);
        }

        // 複数行の設定（証明書の PEM など）は、改行とタブだけを許す
        if (value.Any(c => char.IsControl(c) && !(Multiline && c is '\r' or '\n' or '\t')))
        {
            throw new AppSettingValidationException(ServerMessageKeys.SettingControlChars, Key);
        }

        if (AllowedValues is { } allowed)
        {
            return allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
                ?? throw new AppSettingValidationException(ServerMessageKeys.SettingChoiceInvalid, Key, string.Join(" / ", allowed));
        }

        return StringNormalizer?.Invoke(value) ?? value;
    }

    private string NormalizeInteger(string value, bool enforceRange)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new AppSettingValidationException(ServerMessageKeys.SettingIntegerInvalid, Key);
        }

        if (enforceRange && Minimum is { } min && parsed < min)
        {
            throw new AppSettingValidationException(ServerMessageKeys.SettingMinimum, Key, min);
        }

        if (enforceRange && Maximum is { } max && parsed > max)
        {
            throw new AppSettingValidationException(ServerMessageKeys.SettingMaximum, Key, max);
        }

        return parsed.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>画面から編集できる設定の一覧。</summary>
/// <remarks>
/// <b>起動に要る値はここへ入れない</b>: DB の接続・秘密の鍵・自動マイグレーション・転送ヘッダの信頼ネットワーク・
/// パスワード条件の正規表現・メール送信ワーカーの切り替えは、これまでどおりファイル／環境変数。
/// </remarks>
public static class AppSettingDefinitions
{
    public const string PublicBaseUrl = "ATOMFEED_PUBLIC_BASEURL";
    public const string TimeZoneDefault = "ATOMFEED_TIMEZONE_DEFAULT";

    public const string PleasanterBaseUrl = "ATOMFEED_PLEASANTER_BASEURL";
    public const string PleasanterApiKey = "ATOMFEED_PLEASANTER_APIKEY";
    public const string PleasanterApiVersion = "ATOMFEED_PLEASANTER_APIVERSION";
    public const string PleasanterTimeoutSeconds = "ATOMFEED_PLEASANTER_TIMEOUTSECONDS";
    public const string PleasanterTimeZone = "ATOMFEED_PLEASANTER_TIMEZONE";
    public const string PleasanterLinkBaseUrl = "ATOMFEED_PLEASANTER_LINKBASEURL";

    public const string FeedCacheSeconds = "ATOMFEED_FEED_CACHESECONDS";
    public const string FeedMaxEntries = "ATOMFEED_FEED_MAXENTRIES";
    public const string FeedFailureNotify = "ATOMFEED_FEED_FAILURENOTIFY";
    public const string FeedFailureNotifyThreshold = "ATOMFEED_FEED_FAILURENOTIFYTHRESHOLD";
    public const string FeedFailureNotifyRepeatHours = "ATOMFEED_FEED_FAILURENOTIFYREPEATHOURS";
    public const string FeedPageSize = "ATOMFEED_FEED_PAGESIZE";

    public const string AdminTwoFactor = "ATOMFEED_ADMIN_TWOFACTOR";
    public const string PasswordMinimumLength = "ATOMFEED_PASSWORD_MINIMUMLENGTH";
    public const string PasswordAllowSameAsLoginId = "ATOMFEED_PASSWORD_ALLOWSAMEASLOGINID";
    public const string AuditLogRetentionDays = "ATOMFEED_AUDITLOG_RETENTION_DAYS";

    public const string MailEnabled = "ATOMFEED_MAIL_ENABLED";
    public const string MailTransport = "ATOMFEED_MAIL_TRANSPORT";
    public const string MailSmtpHost = "ATOMFEED_MAIL_SMTP_HOST";
    public const string MailSmtpPort = "ATOMFEED_MAIL_SMTP_PORT";
    public const string MailSmtpSecurity = "ATOMFEED_MAIL_SMTP_SECURITY";
    public const string MailSmtpUser = "ATOMFEED_MAIL_SMTP_USER";
    public const string MailSmtpPassword = "ATOMFEED_MAIL_SMTP_PASSWORD";
    public const string MailFromAddress = "ATOMFEED_MAIL_FROM_ADDRESS";
    public const string MailFromName = "ATOMFEED_MAIL_FROM_NAME";
    public const string MailReplyToAddress = "ATOMFEED_MAIL_REPLYTO_ADDRESS";
    public const string MailSesRegion = "ATOMFEED_MAIL_SES_REGION";
    public const string MailAcsEndpoint = "ATOMFEED_MAIL_ACS_ENDPOINT";
    public const string MailTimeoutSeconds = "ATOMFEED_MAIL_TIMEOUT_SECONDS";
    public const string MailSendPerMinute = "ATOMFEED_MAIL_SEND_PER_MINUTE";

    public const string SamlEnabled = "ATOMFEED_SAML_ENABLED";
    public const string SamlEntityId = "ATOMFEED_SAML_ENTITYID";
    public const string SamlIdpEntityId = "ATOMFEED_SAML_IDPENTITYID";
    public const string SamlSingleSignOnUrl = "ATOMFEED_SAML_SINGLESIGNONURL";
    public const string SamlIdpCertificate = "ATOMFEED_SAML_IDPCERTIFICATE";
    public const string SamlUnknownUser = "ATOMFEED_SAML_UNKNOWNUSER";
    public const string SamlRegisterRole = "ATOMFEED_SAML_REGISTERROLE";
    public const string SamlLoginIdSource = "ATOMFEED_SAML_LOGINIDSOURCE";
    public const string SamlLoginIdClaim = "ATOMFEED_SAML_LOGINIDCLAIM";
    public const string SamlButtonLabel = "ATOMFEED_SAML_BUTTONLABEL";
    public const string SamlSingleLogoutUrl = "ATOMFEED_SAML_SINGLELOGOUTURL";
    public const string PleasanterSsoEnabled = "ATOMFEED_PLEASANTERSSO_ENABLED";
    public const string PleasanterSsoInternalBaseUrl = "ATOMFEED_PLEASANTERSSO_INTERNALBASEURL";
    public const string PleasanterSsoLoginUrl = "ATOMFEED_PLEASANTERSSO_LOGINURL";
    public const string PleasanterSsoLogoutUrl = "ATOMFEED_PLEASANTERSSO_LOGOUTURL";
    public const string PleasanterSsoCookieNames = "ATOMFEED_PLEASANTERSSO_COOKIENAMES";
    public const string PleasanterSsoAllowedDeptIds = "ATOMFEED_PLEASANTERSSO_ALLOWEDDEPTIDS";
    public const string PleasanterSsoAllowedGroupIds = "ATOMFEED_PLEASANTERSSO_ALLOWEDGROUPIDS";
    public const string PleasanterSsoUnknownUser = "ATOMFEED_PLEASANTERSSO_UNKNOWNUSER";
    public const string PleasanterSsoRegisterRole = "ATOMFEED_PLEASANTERSSO_REGISTERROLE";
    public const string PleasanterSsoRevalidateMinutes = "ATOMFEED_PLEASANTERSSO_REVALIDATEMINUTES";
    public const string PleasanterSsoTimeoutSeconds = "ATOMFEED_PLEASANTERSSO_TIMEOUTSECONDS";
    public const string PleasanterSsoButtonLabel = "ATOMFEED_PLEASANTERSSO_BUTTONLABEL";

    private static readonly string[] TwoFactorModes = ["required", "optional", "disabled"];

    /// <summary>すべての定義（画面に出す順）。</summary>
    public static readonly IReadOnlyList<AppSettingDefinition> All =
    [
        new(PublicBaseUrl, AppSettingGroups.Service, AppSettingValueType.String, string.Empty, "公開 URL", MaximumLength: 2000, StringNormalizer: v => Url(v, requireHttps: false)),
        new(TimeZoneDefault, AppSettingGroups.Service, AppSettingValueType.String, "Asia/Tokyo", "既定のタイムゾーン", MaximumLength: 64, StringNormalizer: v => TimeZone(v, required: true)),

        new(PleasanterBaseUrl, AppSettingGroups.Pleasanter, AppSettingValueType.String, string.Empty, "Pleasanter の URL", MaximumLength: 2000, StringNormalizer: v => Url(v, requireHttps: false)),
        new(PleasanterApiKey, AppSettingGroups.Pleasanter, AppSettingValueType.String, string.Empty, "Pleasanter の共通 API キー", IsSecret: true, MaximumLength: 512),
        new(PleasanterApiVersion, AppSettingGroups.Pleasanter, AppSettingValueType.String, "1.1", "Pleasanter の API バージョン", MaximumLength: 8, StringNormalizer: ApiVersion),
        new(PleasanterTimeoutSeconds, AppSettingGroups.Pleasanter, AppSettingValueType.Integer, "30", "Pleasanter 呼び出しのタイムアウト（秒）", Minimum: 1, Maximum: 600),
        new(PleasanterTimeZone, AppSettingGroups.Pleasanter, AppSettingValueType.String, string.Empty, "API キー利用者のタイムゾーン", MaximumLength: 64, StringNormalizer: v => TimeZone(v, required: false)),
        new(PleasanterLinkBaseUrl, AppSettingGroups.Pleasanter, AppSettingValueType.String, string.Empty, "記事のリンクの基点 URL", MaximumLength: 2000, StringNormalizer: v => Url(v, requireHttps: false)),

        new(FeedCacheSeconds, AppSettingGroups.Feed, AppSettingValueType.Integer, "300", "キャッシュ秒数", Minimum: 1, Maximum: 86400),
        new(FeedMaxEntries, AppSettingGroups.Feed, AppSettingValueType.Integer, "1000", "1 フィードの記事の上限", Minimum: 1, Maximum: 100000),
        new(FeedFailureNotify, AppSettingGroups.Feed, AppSettingValueType.Boolean, "false", "フィードの取得の失敗が続いたらメールで知らせる"),
        new(FeedFailureNotifyThreshold, AppSettingGroups.Feed, AppSettingValueType.Integer, "3", "失敗を知らせる連続の回数", Minimum: 1, Maximum: 100),
        new(FeedFailureNotifyRepeatHours, AppSettingGroups.Feed, AppSettingValueType.Integer, "24", "失敗が続くときの再通知の間隔（時間）", Minimum: 1, Maximum: 720),
        new(FeedPageSize, AppSettingGroups.Feed, AppSettingValueType.Integer, "1000", "1 ページの取得件数", Minimum: 1, Maximum: 10000),

        new(AdminTwoFactor, AppSettingGroups.Security, AppSettingValueType.String, "optional", "管理者の 2 要素", AllowedValues: TwoFactorModes),
        new(PasswordMinimumLength, AppSettingGroups.Security, AppSettingValueType.Integer, "12", "パスワードの最低の長さ", Minimum: 8, Maximum: 128),
        new(PasswordAllowSameAsLoginId, AppSettingGroups.Security, AppSettingValueType.Boolean, "false", "ログイン ID と同じパスワードを許す"),
        new(AuditLogRetentionDays, AppSettingGroups.Security, AppSettingValueType.Integer, "365", "監査ログを残す日数", Minimum: 0, Maximum: 36500),

        new(MailEnabled, AppSettingGroups.Mail, AppSettingValueType.Boolean, "false", "メール送信を有効にする"),
        new(MailTransport, AppSettingGroups.Mail, AppSettingValueType.String, "Smtp", "送信方式", AllowedValues: ["Smtp", "AmazonSes", "AzureCommunicationServices"]),
        new(MailSmtpHost, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "SMTP サーバー", MaximumLength: 255),
        new(MailSmtpPort, AppSettingGroups.Mail, AppSettingValueType.Integer, "587", "SMTP ポート", Minimum: 1, Maximum: 65535),
        new(MailSmtpSecurity, AppSettingGroups.Mail, AppSettingValueType.String, "StartTls", "SMTP の暗号化", AllowedValues: ["StartTls", "ImplicitTls", "None"]),
        new(MailSmtpUser, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "SMTP 利用者", MaximumLength: 320),
        new(MailSmtpPassword, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "SMTP パスワード", IsSecret: true, MaximumLength: 2000),
        new(MailFromAddress, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "差出人メールアドレス", MaximumLength: 320, StringNormalizer: MailAddressOrEmpty),
        new(MailFromName, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "差出人名", MaximumLength: 200),
        new(MailReplyToAddress, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "返信先メールアドレス", MaximumLength: 320, StringNormalizer: MailAddressOrEmpty),
        new(MailSesRegion, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "Amazon SES のリージョン", MaximumLength: 64),
        new(MailAcsEndpoint, AppSettingGroups.Mail, AppSettingValueType.String, string.Empty, "Azure Communication Services のエンドポイント", MaximumLength: 2000, StringNormalizer: v => Url(v, requireHttps: true)),
        new(MailTimeoutSeconds, AppSettingGroups.Mail, AppSettingValueType.Integer, "30", "1 通あたりのタイムアウト（秒）", Minimum: 1, Maximum: 300),
        new(MailSendPerMinute, AppSettingGroups.Mail, AppSettingValueType.Integer, "60", "1 分あたりの送信数の上限", Minimum: 1, Maximum: 6000),

        // SAML 2.0 でのログイン（既定は無効。Questionnaire の SAML と同じ項目）
        new(SamlEnabled, AppSettingGroups.Saml, AppSettingValueType.Boolean, "false", "SAML ログインを有効にする"),
        new(SamlEntityId, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "SP のエンティティ ID", MaximumLength: 2000),
        new(SamlIdpEntityId, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "IdP のエンティティ ID", MaximumLength: 2000),
        new(SamlSingleSignOnUrl, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "IdP のシングルサインオン URL", MaximumLength: 2000, StringNormalizer: v => Url(v, requireHttps: false)),
        new(SamlIdpCertificate, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "IdP の署名証明書", MaximumLength: 20000, Multiline: true),
        new(SamlUnknownUser, AppSettingGroups.Saml, AppSettingValueType.String, "Reject", "本アプリに居ない利用者の扱い", AllowedValues: ["Reject", "Register"]),
        new(SamlRegisterRole, AppSettingGroups.Saml, AppSettingValueType.String, AdminRoles.Editor, "自動で作る利用者の役割", AllowedValues: AdminRoles.All),
        new(SamlLoginIdSource, AppSettingGroups.Saml, AppSettingValueType.String, "NameId", "ログイン ID の取り方", AllowedValues: ["NameId", "Claim"]),
        new(SamlLoginIdClaim, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "ログイン ID にする属性の名前", MaximumLength: 500),
        new(SamlButtonLabel, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "ログイン画面の釦の文字", MaximumLength: 100),
        new(SamlSingleLogoutUrl, AppSettingGroups.Saml, AppSettingValueType.String, string.Empty, "IdP の単一ログアウト URL", MaximumLength: 2000, StringNormalizer: v => Url(v, requireHttps: false)),
        new(PleasanterSsoEnabled, AppSettingGroups.PleasanterSso, AppSettingValueType.Boolean, "false", "Pleasanter のログインを有効にする"),
        new(PleasanterSsoInternalBaseUrl, AppSettingGroups.PleasanterSso, AppSettingValueType.String, string.Empty, "本アプリから Pleasanter へ問い合わせる URL（サーバ間）", MaximumLength: 2000),
        new(PleasanterSsoLoginUrl, AppSettingGroups.PleasanterSso, AppSettingValueType.String, string.Empty, "Pleasanter のログイン画面の URL", MaximumLength: 2000),
        new(PleasanterSsoLogoutUrl, AppSettingGroups.PleasanterSso, AppSettingValueType.String, string.Empty, "Pleasanter のログアウト画面の URL", MaximumLength: 2000),
        new(PleasanterSsoCookieNames, AppSettingGroups.PleasanterSso, AppSettingValueType.String, ".AspNetCore.Cookies,Pleasanter_SessionGuid", "Pleasanter へ転送する cookie の名前（前方一致）", MaximumLength: 500),
        new(PleasanterSsoAllowedDeptIds, AppSettingGroups.PleasanterSso, AppSettingValueType.String, string.Empty, "許可する組織の ID", MaximumLength: 1024),
        new(PleasanterSsoAllowedGroupIds, AppSettingGroups.PleasanterSso, AppSettingValueType.String, string.Empty, "許可するグループの ID", MaximumLength: 1024),
        new(PleasanterSsoUnknownUser, AppSettingGroups.PleasanterSso, AppSettingValueType.String, "Reject", "本アプリに居ない利用者の扱い", AllowedValues: ["Reject", "Register"]),
        new(PleasanterSsoRegisterRole, AppSettingGroups.PleasanterSso, AppSettingValueType.String, AdminRoles.Editor, "自動で作る利用者の役割", AllowedValues: AdminRoles.All),
        new(PleasanterSsoRevalidateMinutes, AppSettingGroups.PleasanterSso, AppSettingValueType.Integer, "5", "Pleasanter へ確かめ直す間隔（分）", Minimum: 1, Maximum: 60),
        new(PleasanterSsoTimeoutSeconds, AppSettingGroups.PleasanterSso, AppSettingValueType.Integer, "5", "Pleasanter への問い合わせの待ち時間（秒）", Minimum: 1, Maximum: 30),
        new(PleasanterSsoButtonLabel, AppSettingGroups.PleasanterSso, AppSettingValueType.String, string.Empty, "ログイン画面の釦の文字", MaximumLength: 100),
    ];

    /// <summary>キーで引く。</summary>
    public static readonly FrozenDictionary<string, AppSettingDefinition> ByKey =
        All.ToFrozenDictionary(d => d.Key, StringComparer.Ordinal);

    /// <summary>
    /// 値の組み合わせを検証する。<b>メールを有効にするなら、差出人・公開 URL・送信経路に応じた設定が要る</b>
    /// （足りないまま有効にすると、招待が黙って送られない）。
    /// </summary>
    public static void ValidateCombination(IReadOnlyDictionary<string, string> values)
    {
        ValidateSaml(values);
        ValidatePleasanterSso(values);
        if (!bool.Parse(values[MailEnabled]))
        {
            return;
        }

        if (values[MailFromAddress].Length == 0)
        {
            throw new AppSettingValidationException(ServerMessageKeys.MailFromRequired);
        }

        if (values[PublicBaseUrl].Length == 0)
        {
            throw new AppSettingValidationException(ServerMessageKeys.MailPublicUrlRequired);
        }

        switch (Enum.Parse<MailTransportKind>(values[MailTransport]))
        {
            case MailTransportKind.Smtp when values[MailSmtpHost].Length == 0:
                throw new AppSettingValidationException(ServerMessageKeys.MailSmtpHostRequired);
            case MailTransportKind.AmazonSes when values[MailSesRegion].Length == 0:
                throw new AppSettingValidationException(ServerMessageKeys.MailSesRegionRequired);
            case MailTransportKind.AzureCommunicationServices when values[MailAcsEndpoint].Length == 0:
                throw new AppSettingValidationException(ServerMessageKeys.MailAcsEndpointRequired);
        }
    }

    /// <summary>SAML を有効にするなら、必要な設定がそろっていること（足りないまま有効にすると、ログインできなくなる）。</summary>
    private static void ValidateSaml(IReadOnlyDictionary<string, string> values)
    {
        try
        {
            _ = Services.SamlOptions.FromValues(key => values.TryGetValue(key, out var v) ? v : null);
        }
        catch (SettingException ex)
        {
            // 鍵と埋め込みの値を引き継ぐ（管理 API が、要求の言語で返す）
            throw new AppSettingValidationException(ex.MessageKey, ex.Args);
        }
    }

    /// <summary>Pleasanter のログインを有効にするなら、必要な設定がそろっていること。</summary>
    private static void ValidatePleasanterSso(IReadOnlyDictionary<string, string> values)
    {
        try
        {
            _ = Services.PleasanterSsoOptions.FromValues(key => values.TryGetValue(key, out var v) ? v : null);
        }
        catch (SettingException ex)
        {
            // 鍵と埋め込みの値を引き継ぐ（管理 API が、要求の言語で返す）
            throw new AppSettingValidationException(ex.MessageKey, ex.Args);
        }
    }

    private static string Url(string value, bool requireHttps)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (requireHttps ? uri.Scheme == Uri.UriSchemeHttps : uri.Scheme is "http" or "https")
            ? value.TrimEnd('/')
            : throw new AppSettingValidationException(
                requireHttps ? ServerMessageKeys.SettingUrlHttpsRequired : ServerMessageKeys.SettingUrlHttpRequired);
    }

    private static string TimeZone(string value, bool required)
    {
        if (value.Length == 0)
        {
            return required
                ? throw new AppSettingValidationException(ServerMessageKeys.SettingTimeZoneRequired)
                : string.Empty;
        }

        return System.TimeZoneInfo.TryFindSystemTimeZoneById(value, out _)
            ? value
            : throw new AppSettingValidationException(ServerMessageKeys.SettingTimeZoneUnresolvable, value);
    }

    private static string ApiVersion(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v.ToString(CultureInfo.InvariantCulture)
            : throw new AppSettingValidationException(ServerMessageKeys.SettingApiVersionInvalid);

    private static string MailAddressOrEmpty(string value) =>
        value.Length == 0
            ? string.Empty
            : MailAddress.TryCreate(value, out var address) && string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)
                ? address.Address
                : throw new AppSettingValidationException(ServerMessageKeys.SettingEmailInvalid);
}
