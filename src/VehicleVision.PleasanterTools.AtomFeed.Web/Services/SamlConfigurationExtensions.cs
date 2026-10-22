using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.IdentityModel.Tokens;
using VehicleVision.PleasanterTools.Core.Web;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Services;

/// <summary>Core の設定から、ITfoxtec での照合に使う設定を作る。</summary>
public static class SamlConfigurationExtensions
{
    public static Saml2Configuration ToSaml2Configuration(this SamlOptions options)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("SAML は無効です。");
        }

        var configuration = new Saml2Configuration
        {
            Issuer = options.EntityId,
            SingleSignOnDestination = options.SingleSignOnUrl,
            SingleLogoutDestination = options.SingleLogoutUrl,
            AllowedIssuer = options.IdpEntityId,
            AudienceRestricted = true,
            // 信頼の起点は設定の証明書そのもの。自己署名も使えるよう、連鎖は検査しない。
            CertificateValidationMode = X509CertificateValidationMode.None,
            RevocationMode = X509RevocationMode.NoCheck,
        };

        configuration.AllowedAudienceUris.Add(options.EntityId);
        configuration.SignatureValidationCertificates.AddRange(options.IdpCertificates);

        // 署名の種類は IdP 任せにしない。SHA-1 は受け取らない。
        configuration.SignatureValidationAlgorithms.Add(Saml2SecurityAlgorithms.RsaSha256Signature);
        configuration.SignatureValidationAlgorithms.Add(SecurityAlgorithms.RsaSha384Signature);
        configuration.SignatureValidationAlgorithms.Add(SecurityAlgorithms.RsaSha512Signature);

        return configuration;
    }
}
