using System.Net;
using System.Globalization;
using VehicleVision.PleasanterTools.Core.Mail;
using VehicleVision.PleasanterTools.AtomFeed.Pleasanter;
using VehicleVision.PleasanterTools.AtomFeed.Admin;
using VehicleVision.PleasanterTools.AtomFeed.Security;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Services;

/// <summary>設定の名前と、読み取り。**設定名はここ 1 か所に置く。**</summary>
/// <remarks>
/// 名前は環境変数と同じ（<c>ATOMFEED_*</c>）。<c>App_Data/Parameters/*.json</c> のキーは
/// <see cref="ParameterFiles"/> が写す。
/// </remarks>
public static class AppSettings
{
    public const string TimeZoneDefault = "ATOMFEED_TIMEZONE_DEFAULT";
    public const string PublicBaseUrl = "ATOMFEED_PUBLIC_BASEURL";

    public const string PleasanterBaseUrl = "ATOMFEED_PLEASANTER_BASEURL";
    public const string PleasanterApiKey = "ATOMFEED_PLEASANTER_APIKEY";
    public const string PleasanterApiVersion = "ATOMFEED_PLEASANTER_APIVERSION";
    public const string PleasanterTimeoutSeconds = "ATOMFEED_PLEASANTER_TIMEOUTSECONDS";
    public const string PleasanterTimeZone = "ATOMFEED_PLEASANTER_TIMEZONE";
    public const string PleasanterLinkBaseUrl = "ATOMFEED_PLEASANTER_LINKBASEURL";

    public const string FeedCacheSeconds = "ATOMFEED_FEED_CACHESECONDS";
    public const string FeedMaxEntries = "ATOMFEED_FEED_MAXENTRIES";
    public const string FeedPageSize = "ATOMFEED_FEED_PAGESIZE";

    public const string DbProvider = "ATOMFEED_DB_PROVIDER";
    public const string DbConnectionString = "ATOMFEED_DB_CONNECTIONSTRING";
    public const string DbAutoMigrate = "ATOMFEED_DB_AUTOMIGRATE";
    public const string AdminTwoFactor = "ATOMFEED_ADMIN_TWOFACTOR";
    public const string PasswordMinimumLength = "ATOMFEED_PASSWORD_MINIMUMLENGTH";
    public const string PasswordAllowSameAsLoginId = "ATOMFEED_PASSWORD_ALLOWSAMEASLOGINID";
    public const string PasswordPolicies = "PasswordPolicies";
    public const string SecretKey = "ATOMFEED_SECRET_KEY";
    public const string AuditLogRetentionDays = "ATOMFEED_AUDITLOG_RETENTION_DAYS";
    public const string ForwardedProxyNetworks = "ATOMFEED_FORWARDED_PROXY_NETWORKS";

    /// <summary>秘密の鍵から <see cref="ISecretProtector"/> を作る。未設定なら、使おうとしたときに止まる実装を返す。</summary>
    public static ISecretProtector ReadSecretProtector(IConfiguration configuration) =>
        Text(configuration, SecretKey) is { } key ? new SecretProtector(key) : new UnconfiguredSecretProtector();

    /// <summary>
    /// 転送ヘッダ（<c>X-Forwarded-*</c>）を信頼するプロキシのネットワーク（カンマ区切りの CIDR）を読む。
    /// **未設定なら空（誰も信頼しない）。** CIDR でない値は起動時に落とす。**起動に係るので、画面から編集する設定には含めない。**
    /// </summary>
    public static IReadOnlyList<IPNetwork> ReadForwardedProxyNetworks(IConfiguration configuration) =>
        (Text(configuration, ForwardedProxyNetworks) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => IPNetwork.TryParse(value, out var network)
                ? network
                : throw new InvalidOperationException(
                    $"{ForwardedProxyNetworks} に CIDR でない値があります: {value}（例: 10.0.0.0/8, 192.0.2.10/32）"))
            .ToArray();

    /// <summary>起動時にマイグレーションを当てるか。既定は当てる。<c>false</c> で無効（手で当てる）。</summary>
    public static bool ReadAutoMigrate(IConfiguration configuration) =>
        !string.Equals(Text(configuration, DbAutoMigrate), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>空でない値を返す。無ければ <c>null</c>。</summary>
    public static string? Text(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : null;

    private static string Required(IConfiguration configuration, string key) =>
        Text(configuration, key)
        ?? throw new InvalidOperationException(
            $"設定 {key} がありません。App_Data/Parameters の *.local.json か環境変数で与えてください。");

    private static int PositiveInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(Text(configuration, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v
            : fallback;
}
