using Microsoft.Extensions.Configuration;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>値をファイルから読む（<c>ATOMFEED_X_FILE</c>。Docker secrets など）。環境変数は直接渡し、プロセスの環境変数を触らない。</summary>
public sealed class SecretFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"atomfeed-secrets-{Guid.NewGuid():N}");

    public SecretFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Secret(string content)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}");
        File.WriteAllText(path, content);
        return path;
    }

    private static Dictionary<string, string?> Read(Dictionary<string, string?> env) =>
        ParameterFiles.SecretFiles(env.AsReadOnly()).ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void ファイルの中身を_Fileを外した名前の設定として読む()
    {
        var values = Read(new() { ["ATOMFEED_SECRET_KEY_FILE"] = Secret("abc123") });

        Assert.Equal("abc123", values["ATOMFEED_SECRET_KEY"]);
    }

    [Theory]
    [InlineData("v\n", "v")]
    [InlineData("v\r\n", "v")]
    [InlineData("v\n\n", "v\n")]
    [InlineData("a\nb\n", "a\nb")]
    public void 末尾の改行は_1つだけ落とす_複数行はそのまま(string content, string expected)
    {
        Assert.Equal(expected, Read(new() { ["ATOMFEED_PLEASANTER_APIKEY_FILE"] = Secret(content) })["ATOMFEED_PLEASANTER_APIKEY"]);
    }

    [Fact]
    public void 証明書のような複数行も読める()
    {
        var pem = "-----BEGIN CERTIFICATE-----\nAAA\n-----END CERTIFICATE-----\n";

        var value = Read(new() { ["ATOMFEED_SAML_IDPCERTIFICATE_FILE"] = Secret(pem) })["ATOMFEED_SAML_IDPCERTIFICATE"];

        Assert.Equal(pem.TrimEnd('\n'), value);
    }

    [Fact]
    public void 同じ設定に直接の値とファイルの両方があれば_落とす()
    {
        var env = new Dictionary<string, string?> { ["ATOMFEED_SECRET_KEY"] = "x", ["ATOMFEED_SECRET_KEY_FILE"] = Secret("y") };

        var ex = Assert.Throws<InvalidOperationException>(() => Read(env));

        Assert.Contains("ATOMFEED_SECRET_KEY", ex.Message);
    }

    [Fact]
    public void ファイルが無い_空なら_落とす_黙って未設定にしない()
    {
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["ATOMFEED_SECRET_KEY_FILE"] = Path.Combine(_dir, "none") }));
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["ATOMFEED_SECRET_KEY_FILE"] = Secret("\n") }));
    }

    [Fact]
    public void ATOMFEED以外_空のパス_直接の値が空なら_対象にしない()
    {
        var values = Read(new()
        {
            ["OTHER_X_FILE"] = Secret("no"),
            ["ATOMFEED_A_FILE"] = string.Empty,
            ["ATOMFEED_B"] = string.Empty,
            ["ATOMFEED_B_FILE"] = Secret("b"),
        });

        Assert.Equal(["ATOMFEED_B"], values.Keys.ToArray());
        Assert.Equal("b", values["ATOMFEED_B"]);
    }

    [Fact]
    public void 実際の積み方では_環境変数と同じ位置で_local_jsonより弱い()
    {
        const string name = "ATOMFEED_PLEASANTER_BASEURL";
        File.WriteAllText(Path.Combine(_dir, "Pleasanter.json"), """{ "BaseUrl": "https://file.test" }""");
        Environment.SetEnvironmentVariable(name + "_FILE", Secret("https://secret.test\n"));
        try
        {
            // 環境変数（_FILE）は、{名前}.json より強い
            Assert.Equal("https://secret.test", new ConfigurationBuilder().AddParameterFiles(_dir).Build()[name]);

            // {名前}.local.json は、さらに強い
            File.WriteAllText(Path.Combine(_dir, "Pleasanter.local.json"), """{ "BaseUrl": "https://local.test" }""");
            Assert.Equal("https://local.test", new ConfigurationBuilder().AddParameterFiles(_dir).Build()[name]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name + "_FILE", null);
        }
    }
}
