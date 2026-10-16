using Microsoft.Extensions.Configuration;
using VehicleVision.PleasanterTools.AtomFeed.Mail;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>
/// 環境変数はプロセス全体のものなので、**このクラスのテストは 1 つずつ直列に走らせ、必ず元へ戻す**
/// （同じクラス内は xUnit が直列に実行する）。
/// </summary>
public sealed class ParameterFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"atomfeed-params-{Guid.NewGuid():N}");

    public ParameterFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AppSettings.PleasanterBaseUrl, null);
        Environment.SetEnvironmentVariable(AppSettings.PleasanterApiKey, null);
        Directory.Delete(_dir, recursive: true);
    }

    private void Write(string name, string json) => File.WriteAllText(Path.Combine(_dir, name), json);

    private IConfigurationRoot Build() => new ConfigurationBuilder().AddParameterFiles(_dir).Build();

    [Fact]
    public void ファイルのキーを環境変数と同じ名前へ写す()
    {
        Write("Pleasanter.json", """{ "BaseUrl": "https://a.test", "TimeoutSeconds": 12 }""");
        Write("Feed.json", """{ "CacheSeconds": 60 }""");

        var config = Build();

        Assert.Equal("https://a.test", config[AppSettings.PleasanterBaseUrl]);
        Assert.Equal("12", config[AppSettings.PleasanterTimeoutSeconds]);
        Assert.Equal("60", config[AppSettings.FeedCacheSeconds]);
    }

    [Fact]
    public void 失敗の通知の設定は_Feed_jsonの入れ子から写す()
    {
        Write("Feed.json", """{ "FailureNotify": { "Enabled": true, "Threshold": 5, "RepeatHours": 12 } }""");

        var config = Build();

        Assert.Equal("True", config[AppSettingDefinitions.FeedFailureNotify]);
        Assert.Equal("5", config[AppSettingDefinitions.FeedFailureNotifyThreshold]);
        Assert.Equal("12", config[AppSettingDefinitions.FeedFailureNotifyRepeatHours]);
    }

    [Fact]
    public void メールの設定は_Pleasanterに倣って経路ごとに入れ子にして書ける()
    {
        Write("Mail.json", """
            {
              "Enabled": true, "Provider": "Smtp", "FromAddress": "noreply@example.com", "Worker": false,
              "Smtp": { "Host": "smtp.example.com", "Port": 2525, "Security": "StartTls", "User": "u", "Password": "p" },
              "AwsSes": { "Region": "ap-northeast-1" },
              "Acs": { "Endpoint": "https://x.communication.azure.com" }
            }
            """);

        var config = Build();

        Assert.Equal("True", config["ATOMFEED_MAIL_ENABLED"]);
        Assert.Equal("Smtp", config["ATOMFEED_MAIL_TRANSPORT"]);
        Assert.Equal("noreply@example.com", config["ATOMFEED_MAIL_FROM_ADDRESS"]);
        Assert.Equal("smtp.example.com", config["ATOMFEED_MAIL_SMTP_HOST"]);
        Assert.Equal("2525", config["ATOMFEED_MAIL_SMTP_PORT"]);
        Assert.Equal("StartTls", config["ATOMFEED_MAIL_SMTP_SECURITY"]);
        Assert.Equal("u", config["ATOMFEED_MAIL_SMTP_USER"]);
        Assert.Equal("p", config["ATOMFEED_MAIL_SMTP_PASSWORD"]);
        Assert.Equal("ap-northeast-1", config["ATOMFEED_MAIL_SES_REGION"]);
        Assert.Equal("https://x.communication.azure.com", config["ATOMFEED_MAIL_ACS_ENDPOINT"]);
        Assert.Equal("False", config[MailSenderOptions.WorkerKey]);
    }

    [Fact]
    public void SAMLとPleasanterのログインの設定は_Authentication_jsonの入れ子から写す()
    {
        Write("Authentication.json", """
            {
              "Saml": { "Enabled": true, "EntityId": "urn:atomfeed", "IdpCertificate": "-----BEGIN CERTIFICATE-----\nAAA\n-----END CERTIFICATE-----" },
              "PleasanterSso": { "Enabled": true, "LoginUrl": "/users/login", "RevalidateMinutes": 10 }
            }
            """);

        var config = Build();

        Assert.Equal("True", config[AppSettingDefinitions.SamlEnabled]);
        Assert.Equal("urn:atomfeed", config[AppSettingDefinitions.SamlEntityId]);
        Assert.Contains("BEGIN CERTIFICATE", config[AppSettingDefinitions.SamlIdpCertificate]);
        Assert.Equal("True", config[AppSettingDefinitions.PleasanterSsoEnabled]);
        Assert.Equal("/users/login", config[AppSettingDefinitions.PleasanterSsoLoginUrl]);
        Assert.Equal("10", config[AppSettingDefinitions.PleasanterSsoRevalidateMinutes]);
    }

    [Fact]
    public void 同じ名前のキーが別のファイルにあっても混ざらない()
    {
        // Rds.json の Provider（RDBMS）と Mail.json の Provider（送信経路）。Pleasanter と同じく、ファイルごとに持てる
        Write("Rds.json", """{ "Provider": "PostgreSql" }""");
        Write("Mail.json", """{ "Provider": "AmazonSes" }""");

        var config = Build();

        Assert.Equal("PostgreSql", config[AppSettings.DbProvider]);
        Assert.Equal("AmazonSes", config["ATOMFEED_MAIL_TRANSPORT"]);
    }

    [Fact]
    public void 環境変数と同じ正式名のキーをファイルへそのまま書いても効く()
    {
        Write("Service.json", """{ "ATOMFEED_SAML_ENABLED": "true" }""");

        Assert.Equal("true", Build()["ATOMFEED_SAML_ENABLED"]);
    }

    [Fact]
    public void 優先順位はlocal_jsonが環境変数より強く環境変数がjsonより強い()
    {
        Write("Pleasanter.json", """{ "BaseUrl": "https://file.test" }""");
        Environment.SetEnvironmentVariable(AppSettings.PleasanterBaseUrl, "https://env.test");
        Assert.Equal("https://env.test", Build()[AppSettings.PleasanterBaseUrl]);

        Write("Pleasanter.local.json", """{ "BaseUrl": "https://local.test" }""");
        Assert.Equal("https://local.test", Build()[AppSettings.PleasanterBaseUrl]);
    }

    [Fact]
    public void nullと空の値は写さず環境変数のAPIキーを塗り潰さない()
    {
        Write("Pleasanter.json", """{ "ApiKey": null }""");
        Write("Pleasanter.local.json", """{ "ApiKey": "" }""");
        Environment.SetEnvironmentVariable(AppSettings.PleasanterApiKey, "from-env");

        Assert.Equal("from-env", Build()[AppSettings.PleasanterApiKey]);
    }

    [Fact]
    public void 読んだ置き場と見つかったファイルを残す()
    {
        Write("Service.json", "{}");
        Write("Security.local.json", "{}");

        var config = Build();

        Assert.Equal(_dir, config[ParameterFiles.DirectoryKey]);
        Assert.Equal("Service.json / Security.local.json", config[ParameterFiles.FoundFilesKey]);
    }

    [Fact]
    public void 置き場が無くても落ちず見つかったファイルは空()
    {
        var config = new ConfigurationBuilder().AddParameterFiles(Path.Combine(_dir, "none")).Build();

        Assert.Equal(string.Empty, config[ParameterFiles.FoundFilesKey]);
    }

    [Fact]
    public void 同梱の既定ファイルが読める()
    {
        var shipped = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
            "VehicleVision.PleasanterTools.AtomFeed.Web", "App_Data", "Parameters"));
        var config = new ConfigurationBuilder().AddParameterFiles(shipped).Build();

        // **管理画面から編集できる設定は、同梱のファイルで固定しない**（値が入っていると外部設定として固定され、画面から変えられなくなる）。
        // 既定値はコードにある
        foreach (var definition in AppSettingDefinitions.All)
        {
            Assert.True(config[definition.Key] is null, $"同梱のファイルが {definition.Key} を固定している: {config[definition.Key]}");
        }

        Assert.Null(config[AppSettings.SecretKey]);
    }

    [Fact]
    public void Pleasanterの接続先が未設定でも起動できる_画面から設定できる()
    {
        var config = new ConfigurationBuilder().Build();

        var options = TestSettings.Build(config).Pleasanter;

        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal("Asia/Tokyo", options.ApiKeyUserTimeZoneId);
        Assert.Equal(1.1m, options.ApiVersion);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("ATOMFEED_PLEASANTER_APIVERSION", "abc")]
    [InlineData("ATOMFEED_PLEASANTER_TIMEOUTSECONDS", "abc")]
    [InlineData("ATOMFEED_PLEASANTER_BASEURL", "ftp://a.test")]
    public void 読めない外部設定は起動時に落とす(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();

        Assert.Throws<AppSettingValidationException>(() => TestSettings.Build(config));
    }

    [Fact]
    public void 外部設定の値を読む()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AppSettings.PleasanterBaseUrl] = "https://a.test/",
            [AppSettings.PleasanterApiKey] = "k",
            [AppSettings.TimeZoneDefault] = "Asia/Tokyo",
            [AppSettings.PleasanterApiVersion] = "1.0",
            [AppSettings.PleasanterTimeoutSeconds] = "45",
        }).Build();

        var options = TestSettings.Build(config).Pleasanter;

        Assert.Equal("https://a.test", options.BaseUrl);
        Assert.Equal("k", options.ApiKey);
        Assert.Equal(1.0m, options.ApiVersion);
        Assert.Equal(45, options.TimeoutSeconds);
    }
}
