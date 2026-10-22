using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VehicleVision.PleasanterTools.AtomFeed.Admin;
using VehicleVision.PleasanterTools.AtomFeed.Localization;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;
using VehicleVision.PleasanterTools.Core.Web;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>Core へ渡す本アプリ固有の値と、多言語例外・ITfoxtec への変換。</summary>
public sealed class AuthenticationSettingsTests
{
    [Fact]
    public void Core_の設定キーは既存の設定定義とすべて一致する()
    {
        var sso = AtomFeedAuthenticationSettings.PleasanterSsoKeys;
        var saml = AtomFeedAuthenticationSettings.SamlKeys;
        string[] actual =
        [
            sso.EnabledKey, sso.InternalBaseUrlKey, sso.LoginUrlKey, sso.LogoutUrlKey,
            sso.CookieNamesKey, sso.AllowedDeptIdsKey, sso.AllowedGroupIdsKey, sso.UnknownUserKey,
            sso.RegisterRoleKey, sso.RevalidateMinutesKey, sso.TimeoutSecondsKey, sso.ButtonLabelKey,
            saml.EnabledKey, saml.EntityIdKey, saml.IdpEntityIdKey, saml.SingleSignOnUrlKey,
            saml.IdpCertificateKey, saml.UnknownUserKey, saml.RegisterRoleKey, saml.LoginIdSourceKey,
            saml.LoginIdClaimKey, saml.ButtonLabelKey, saml.SingleLogoutUrlKey,
        ];
        string[] expected =
        [
            AppSettingDefinitions.PleasanterSsoEnabled, AppSettingDefinitions.PleasanterSsoInternalBaseUrl,
            AppSettingDefinitions.PleasanterSsoLoginUrl, AppSettingDefinitions.PleasanterSsoLogoutUrl,
            AppSettingDefinitions.PleasanterSsoCookieNames, AppSettingDefinitions.PleasanterSsoAllowedDeptIds,
            AppSettingDefinitions.PleasanterSsoAllowedGroupIds, AppSettingDefinitions.PleasanterSsoUnknownUser,
            AppSettingDefinitions.PleasanterSsoRegisterRole, AppSettingDefinitions.PleasanterSsoRevalidateMinutes,
            AppSettingDefinitions.PleasanterSsoTimeoutSeconds, AppSettingDefinitions.PleasanterSsoButtonLabel,
            AppSettingDefinitions.SamlEnabled, AppSettingDefinitions.SamlEntityId, AppSettingDefinitions.SamlIdpEntityId,
            AppSettingDefinitions.SamlSingleSignOnUrl, AppSettingDefinitions.SamlIdpCertificate,
            AppSettingDefinitions.SamlUnknownUser, AppSettingDefinitions.SamlRegisterRole,
            AppSettingDefinitions.SamlLoginIdSource, AppSettingDefinitions.SamlLoginIdClaim,
            AppSettingDefinitions.SamlButtonLabel, AppSettingDefinitions.SamlSingleLogoutUrl,
        ];

        Assert.Equal(expected, actual);
        Assert.All(actual, key => Assert.StartsWith("ATOMFEED_", key));
    }

    [Fact]
    public void 直接読み取りと実行時の既定値は変わらない()
    {
        var runtime = new RuntimeSettings(new ConfigurationBuilder().Build()).Current;
        foreach (var sso in new[] { AtomFeedAuthenticationSettings.ReadPleasanterSso(_ => null), runtime.PleasanterSso })
        {
            Assert.False(sso.Enabled);
            Assert.Equal(PleasanterSsoUnknownUserPolicy.Reject, sso.UnknownUser);
            Assert.Equal("Editor", sso.RegisterRole);
            Assert.Equal("atomfeed_", sso.OwnCookiePrefix);
            Assert.Equal<string>([".AspNetCore.Cookies", "Pleasanter_SessionGuid"], sso.CookieNamePrefixes);
            Assert.Equal(TimeSpan.FromMinutes(5), sso.RevalidateInterval);
            Assert.Equal(TimeSpan.FromSeconds(5), sso.Timeout);
            Assert.False(sso.HasMembershipRestriction);
            Assert.False(sso.ShouldForwardCookie("ATOMFEED_admin"));
            Assert.True(sso.ShouldForwardCookie(".AspNetCore.CookiesC1"));
        }

        foreach (var saml in new[] { AtomFeedAuthenticationSettings.ReadSaml(_ => null), runtime.Saml })
        {
            Assert.False(saml.Enabled);
            Assert.False(saml.SingleLogoutEnabled);
            Assert.Equal(SamlUnknownUserPolicy.Reject, saml.UnknownUser);
            Assert.Equal(SamlLoginIdSource.NameId, saml.LoginIdSource);
            Assert.Equal("Editor", saml.RegisterRole);
        }
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("FeedAdministrator")]
    [InlineData("UserAdministrator")]
    [InlineData("Editor")]
    [InlineData("Auditor")]
    public void 本アプリの全役割を大小文字を問わず正規名で返す(string role)
    {
        var sso = AtomFeedAuthenticationSettings.ReadPleasanterSso(
            key => key == AppSettingDefinitions.PleasanterSsoRegisterRole ? $" {role.ToLowerInvariant()} " : null);
        using var idp = new FakeIdp();
        var values = SamlValues(idp);
        values[AppSettingDefinitions.SamlRegisterRole] = $" {role.ToLowerInvariant()} ";
        var saml = AtomFeedAuthenticationSettings.ReadSaml(key => values.GetValueOrDefault(key));

        Assert.Equal(role, sso.RegisterRole);
        Assert.Equal(role, saml.RegisterRole);
        Assert.Equal(["Administrator", "FeedAdministrator", "UserAdministrator", "Editor", "Auditor"], AdminRoles.All);
        DisposeCertificates(saml);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("Editor, Administrator")]
    [InlineData("SurveyAdministrator")]
    public void 他アプリの役割や数値や複合表記は従来どおり拒否する(string role)
    {
        var ssoError = Assert.Throws<SettingException>(() => AtomFeedAuthenticationSettings.ReadPleasanterSso(
            key => key == AppSettingDefinitions.PleasanterSsoRegisterRole ? role : null));
        using var idp = new FakeIdp();
        var values = SamlValues(idp);
        values[AppSettingDefinitions.SamlRegisterRole] = role;
        var samlError = Assert.Throws<SettingException>(() =>
            AtomFeedAuthenticationSettings.ReadSaml(key => values.GetValueOrDefault(key)));

        Assert.Equal(ServerMessageKeys.SettingUnknownValue, ssoError.MessageKey);
        Assert.Equal(ServerMessageKeys.SettingUnknownValue, samlError.MessageKey);
        Assert.Equal(
            [AppSettingDefinitions.PleasanterSsoRegisterRole, role, string.Join(" / ", AdminRoles.All)],
            ssoError.Args);
        Assert.Equal(
            [AppSettingDefinitions.SamlRegisterRole, role, string.Join(" / ", AdminRoles.All)],
            samlError.Args);
    }

    [Fact]
    public void SAML_は無効なら他の設定を読まず_SSO_は無効でも誤記を拒否する()
    {
        var saml = AtomFeedAuthenticationSettings.ReadSaml(key =>
            key == AppSettingDefinitions.SamlEnabled ? "yes" : throw new InvalidOperationException("先読みしました。"));
        Assert.False(saml.Enabled);
        Assert.Throws<SettingException>(() => AtomFeedAuthenticationSettings.ReadPleasanterSso(
            key => key == AppSettingDefinitions.PleasanterSsoUnknownUser ? "1" : null));

        using var idp = new FakeIdp();
        var values = SamlValues(idp);
        values[AppSettingDefinitions.SamlUnknownUser] = "1";
        values[AppSettingDefinitions.SamlLoginIdSource] = "1";
        values[AppSettingDefinitions.SamlLoginIdClaim] = "mail";
        var enabled = AtomFeedAuthenticationSettings.ReadSaml(key => values.GetValueOrDefault(key));
        Assert.Equal(SamlUnknownUserPolicy.Register, enabled.UnknownUser);
        Assert.Equal(SamlLoginIdSource.Claim, enabled.LoginIdSource);
        DisposeCertificates(enabled);
    }

    public static IEnumerable<object[]> ErrorCases()
    {
        yield return Case(false, "ENABLED", "yes", ServerMessageKeys.SettingBooleanValueInvalid, "yes");
        yield return Case(false, "ENABLED", "true", ServerMessageKeys.SettingRequiredWhenEnabled,
            "ATOMFEED_PLEASANTERSSO_INTERNALBASEURL");
        yield return Case(false, "TIMEOUTSECONDS", "31", ServerMessageKeys.SettingIntegerRangeInvalid, 1, 30, "31");
        yield return Case(false, "UNKNOWNUSER", "1", ServerMessageKeys.SettingUnknownValue, "1", "Reject / Register");
        yield return Case(false, "COOKIENAMES", "bad name", ServerMessageKeys.SsoCookieNameInvalidChars, "bad name");
        yield return Case(false, "COOKIENAMES", "ATOMFEED_admin", ServerMessageKeys.SsoCookieNameOverlapsOwn,
            "atomfeed_", "ATOMFEED_admin");
        yield return Case(false, "COOKIENAMES", ",", ServerMessageKeys.SsoCookieNamesEmpty);
        yield return Case(false, "INTERNALBASEURL", "ftp://example.test", ServerMessageKeys.SsoInternalUrlInvalid,
            "ftp://example.test");
        yield return Case(false, "LOGINURL", "//example.test", ServerMessageKeys.SsoBrowserUrlInvalid, "//example.test");
        yield return Case(false, "ALLOWEDDEPTIDS", new string('1', 1025), ServerMessageKeys.SsoIdListTooLong);
        yield return Case(false, "ALLOWEDGROUPIDS", "-1", ServerMessageKeys.SsoIdListInvalid);
        yield return Case(true, "ENTITYID", "", ServerMessageKeys.SettingRequiredWhenEnabled, "ATOMFEED_SAML_ENTITYID");
        yield return Case(true, "SINGLESIGNONURL", "relative", ServerMessageKeys.SettingAbsoluteUrlInvalid, "relative");
        yield return Case(true, "SINGLELOGOUTURL", "relative", ServerMessageKeys.SettingAbsoluteUrlInvalid, "relative");
        yield return Case(true, "IDPCERTIFICATE", "", ServerMessageKeys.SamlCertificateRequired);
        yield return Case(true, "LOGINIDSOURCE", "Claim", ServerMessageKeys.SamlLoginIdClaimRequired,
            "ATOMFEED_SAML_LOGINIDCLAIM");
        yield return Case(true, "IDPCERTIFICATE", "-----BEGIN CERTIFICATE-----",
            ServerMessageKeys.SamlCertificatePemIncomplete);
        yield return Case(true, "IDPCERTIFICATE", "-----BEGIN CERTIFICATE-----\nbad\n-----END CERTIFICATE-----",
            ServerMessageKeys.SamlCertificatePemUnreadable);
        yield return Case(true, "IDPCERTIFICATE", "not base64", ServerMessageKeys.SamlCertificateUnreadable);
    }

    private static object[] Case(bool saml, string suffix, string value, string messageKey, params object[] rest)
    {
        var key = (saml ? "ATOMFEED_SAML_" : "ATOMFEED_PLEASANTERSSO_") + suffix;
        return [saml, key, value, messageKey, new object[] { key }.Concat(rest).ToArray()];
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public void Core_の誤りを既存の鍵と引数と元例外で翻訳する(
        bool saml, string key, string value, string messageKey, object[] expectedArgs)
    {
        using var idp = new FakeIdp();
        var values = saml ? SamlValues(idp) : new Dictionary<string, string?>();
        values[key] = value;
        string? ValueOf(string name) => values.GetValueOrDefault(name);
        Action read = saml
            ? () => AtomFeedAuthenticationSettings.ReadSaml(ValueOf)
            : () => AtomFeedAuthenticationSettings.ReadPleasanterSso(ValueOf);
        var exception = Assert.Throws<SettingException>(read);

        // 必須設定だけは、有効化の鍵が第 1 引数になる。
        if (messageKey == ServerMessageKeys.SettingRequiredWhenEnabled)
        {
            expectedArgs = [saml ? "ATOMFEED_SAML_ENABLED" : "ATOMFEED_PLEASANTERSSO_ENABLED", expectedArgs[1]];
        }
        Assert.Equal(messageKey, exception.MessageKey);
        Assert.Equal(expectedArgs, exception.Args);
        Assert.Equal(ServerMessages.Get(messageKey, "ja", expectedArgs), exception.Message);
        foreach (var language in new[] { "ja", "en", "de", "es", "fr", "ko", "zh" })
        {
            Assert.Equal(
                ServerMessages.Get(messageKey, language, expectedArgs),
                ServerMessages.Get(exception.MessageKey, language, exception.Args));
        }
        if (messageKey is ServerMessageKeys.SamlCertificateUnreadable or ServerMessageKeys.SamlCertificatePemUnreadable)
        {
            Assert.NotNull(exception.InnerException);
            Assert.IsNotType<AuthenticationSettingException>(exception.InnerException);
        }
        else
        {
            Assert.Null(exception.InnerException);
        }
    }

    [Fact]
    public void ITfoxtec_への変換は発行者と宛先と証明書を固定し_SHA1_を許可しない()
    {
        using var idp = new FakeIdp();
        var values = SamlValues(idp);
        values[AppSettingDefinitions.SamlSingleLogoutUrl] = FakeIdp.SingleLogoutUrl;
        var options = AtomFeedAuthenticationSettings.ReadSaml(key => values.GetValueOrDefault(key));
        var configuration = options.ToSaml2Configuration();

        Assert.Equal(FakeIdp.SpEntityId, configuration.Issuer);
        Assert.Equal(FakeIdp.EntityId, configuration.AllowedIssuer);
        Assert.Equal(new Uri(FakeIdp.SingleSignOnUrl), configuration.SingleSignOnDestination);
        Assert.Equal(new Uri(FakeIdp.SingleLogoutUrl), configuration.SingleLogoutDestination);
        Assert.True(configuration.AudienceRestricted);
        Assert.Equal([FakeIdp.SpEntityId], configuration.AllowedAudienceUris);
        Assert.Equal(idp.Certificate.Thumbprint, Assert.Single(configuration.SignatureValidationCertificates).Thumbprint);
        Assert.Equal(X509CertificateValidationMode.None, configuration.CertificateValidationMode);
        Assert.Equal(X509RevocationMode.NoCheck, configuration.RevocationMode);
        Assert.Equal(
            [Saml2SecurityAlgorithms.RsaSha256Signature, SecurityAlgorithms.RsaSha384Signature, SecurityAlgorithms.RsaSha512Signature],
            configuration.SignatureValidationAlgorithms);
        var disabled = AtomFeedAuthenticationSettings.ReadSaml(_ => null);
        Assert.Equal("SAML は無効です。", Assert.Throws<InvalidOperationException>(() => disabled.ToSaml2Configuration()).Message);
        DisposeCertificates(options);
    }

    private static Dictionary<string, string?> SamlValues(FakeIdp idp) => new()
    {
        [AppSettingDefinitions.SamlEnabled] = "true",
        [AppSettingDefinitions.SamlEntityId] = FakeIdp.SpEntityId,
        [AppSettingDefinitions.SamlIdpEntityId] = FakeIdp.EntityId,
        [AppSettingDefinitions.SamlSingleSignOnUrl] = FakeIdp.SingleSignOnUrl,
        [AppSettingDefinitions.SamlIdpCertificate] = idp.CertificatePem,
    };

    private static void DisposeCertificates(SamlOptions options)
    {
        foreach (var certificate in options.IdpCertificates)
        {
            certificate.Dispose();
        }
    }
}
