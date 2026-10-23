using VehicleVision.PleasanterTools.AtomFeed.Admin;
using VehicleVision.PleasanterTools.AtomFeed.Localization;
using VehicleVision.PleasanterTools.Core.Web;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Services;

/// <summary>Core の認証設定に、本アプリの設定名・役割・多言語メッセージを渡す。</summary>
public static class AtomFeedAuthenticationSettings
{
    public const string Prefix = "ATOMFEED_";
    public const string OwnCookiePrefix = "atomfeed_";

    public static PleasanterSsoSettingKeys PleasanterSsoKeys { get; } = new(Prefix);
    public static SamlSettingKeys SamlKeys { get; } = new(Prefix);

    public static PleasanterSsoOptions ReadPleasanterSso(Func<string, string?> valueOf) =>
        Translate(() => PleasanterSsoOptions.FromValues(
            valueOf, PleasanterSsoKeys, OwnCookiePrefix, AdminRoles.All, AdminRoles.Editor));

    public static SamlOptions ReadSaml(Func<string, string?> valueOf) =>
        Translate(() => SamlOptions.FromValues(valueOf, SamlKeys, AdminRoles.All, AdminRoles.Editor));

    private static T Translate<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (AuthenticationSettingException exception)
        {
            var messageKey = exception.Error switch
            {
                AuthenticationSettingError.RequiredWhenEnabled => ServerMessageKeys.SettingRequiredWhenEnabled,
                AuthenticationSettingError.BooleanValueInvalid => ServerMessageKeys.SettingBooleanValueInvalid,
                AuthenticationSettingError.IntegerRangeInvalid => ServerMessageKeys.SettingIntegerRangeInvalid,
                AuthenticationSettingError.UnknownValue => ServerMessageKeys.SettingUnknownValue,
                AuthenticationSettingError.CookieNameInvalidChars => ServerMessageKeys.SsoCookieNameInvalidChars,
                AuthenticationSettingError.CookieNameOverlapsOwn => ServerMessageKeys.SsoCookieNameOverlapsOwn,
                AuthenticationSettingError.CookieNamesEmpty => ServerMessageKeys.SsoCookieNamesEmpty,
                AuthenticationSettingError.InternalUrlInvalid => ServerMessageKeys.SsoInternalUrlInvalid,
                AuthenticationSettingError.BrowserUrlInvalid => ServerMessageKeys.SsoBrowserUrlInvalid,
                AuthenticationSettingError.IdListTooLong => ServerMessageKeys.SsoIdListTooLong,
                AuthenticationSettingError.IdListInvalid => ServerMessageKeys.SsoIdListInvalid,
                AuthenticationSettingError.AbsoluteUrlInvalid => ServerMessageKeys.SettingAbsoluteUrlInvalid,
                AuthenticationSettingError.CertificateRequired => ServerMessageKeys.SamlCertificateRequired,
                AuthenticationSettingError.LoginIdClaimRequired => ServerMessageKeys.SamlLoginIdClaimRequired,
                AuthenticationSettingError.CertificatePemIncomplete => ServerMessageKeys.SamlCertificatePemIncomplete,
                AuthenticationSettingError.CertificatePemUnreadable => ServerMessageKeys.SamlCertificatePemUnreadable,
                AuthenticationSettingError.CertificateNotFound => ServerMessageKeys.SamlCertificateNotFound,
                AuthenticationSettingError.CertificateUnreadable => ServerMessageKeys.SamlCertificateUnreadable,
                _ => throw new ArgumentOutOfRangeException(nameof(exception), exception.Error, "未知の認証設定エラーです。"),
            };
            throw new SettingException(messageKey, [.. exception.Arguments], exception.InnerException);
        }
    }
}
