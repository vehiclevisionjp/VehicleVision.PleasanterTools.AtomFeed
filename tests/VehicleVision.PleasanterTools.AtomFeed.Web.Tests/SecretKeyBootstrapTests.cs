using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using VehicleVision.PleasanterTools.AtomFeed.Security;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>秘密の鍵の自動生成。環境変数（テスト全体で自動生成を止めてある）は、各テストの設定へ直接渡して避ける。</summary>
public sealed class SecretKeyBootstrapTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"atomfeed-key-{Guid.NewGuid():N}");

    public SecretKeyBootstrapTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ConfigurationBuilder Empty(params (string Key, string Value)[] values)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)));
        return builder;
    }

    [Fact]
    public void 鍵が無ければ作って保存し_設定から読める_次の起動でも同じ鍵()
    {
        var builder = Empty();

        var path = SecretKeyBootstrap.EnsureKey(builder, _dir);

        Assert.Equal(Path.Combine(_dir, "Security.local.json"), path);
        var key = builder.Build()[AppSettings.SecretKey];
        Assert.False(string.IsNullOrEmpty(key));
        Assert.Equal(32, Convert.FromBase64String(key).Length);
        new SecretProtector(key);   // 使える鍵であること
        Assert.Equal(key, (string?)JsonNode.Parse(File.ReadAllText(path!))!["SecretKey"]);

        // 次の起動: ファイルから鍵が読め、作り直さない
        var next = new ConfigurationBuilder().AddParameterFiles(_dir);
        Assert.Null(SecretKeyBootstrap.EnsureKey(next, _dir));
        Assert.Equal(key, next.Build()[AppSettings.SecretKey]);
    }

    [Fact]
    public void 鍵が環境変数などで既にあれば何もしない()
    {
        var builder = Empty((AppSettings.SecretKey, SecretProtector.GenerateKey()));

        Assert.Null(SecretKeyBootstrap.EnsureKey(builder, _dir));
        Assert.False(File.Exists(Path.Combine(_dir, "Security.local.json")));
    }

    [Fact]
    public void 止めたければ作らない()
    {
        Assert.Null(SecretKeyBootstrap.EnsureKey(Empty((SecretKeyBootstrap.AutoGenerateKey, "false")), _dir));
        Assert.False(File.Exists(Path.Combine(_dir, "Security.local.json")));

        // Security.json のキーも、正式名へ写される
        File.WriteAllText(Path.Combine(_dir, "Security.json"), """{ "SecretKeyAutoGenerate": "false" }""");
        Assert.Equal("false", new ConfigurationBuilder().AddParameterFiles(_dir).Build()[SecretKeyBootstrap.AutoGenerateKey]);
    }

    [Fact]
    public void 既にあるlocal_jsonの他のキーは残す()
    {
        File.WriteAllText(Path.Combine(_dir, "Security.local.json"), """{ "AdminTwoFactor": "required" }""");

        Assert.NotNull(SecretKeyBootstrap.EnsureKey(Empty(), _dir));

        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(_dir, "Security.local.json")))!;
        Assert.Equal("required", (string?)saved["AdminTwoFactor"]);
        Assert.False(string.IsNullOrEmpty((string?)saved["SecretKey"]));
    }

    [Fact]
    public void 保存できなければ起動を止める_作っても再起動で鍵を失うため()
    {
        // 置き場の場所にファイルがあるので、ディレクトリを作れず保存に失敗する
        var blocker = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocker, "x");
        var builder = Empty();

        var ex = Assert.Throws<InvalidOperationException>(() => SecretKeyBootstrap.EnsureKey(builder, Path.Combine(blocker, "Parameters")));

        Assert.Contains("ATOMFEED_SECRET_KEY", ex.Message);
        Assert.Null(builder.Build()[AppSettings.SecretKey]);
    }

    [Fact]
    public void 作った鍵は_Discardでファイルから消せ_他のキーは残る()
    {
        File.WriteAllText(Path.Combine(_dir, "Security.local.json"), """{ "AdminTwoFactor": "required" }""");
        var path = SecretKeyBootstrap.EnsureKey(Empty(), _dir)!;

        SecretKeyBootstrap.Discard(path);

        var saved = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Null(saved["SecretKey"]);
        Assert.Equal("required", (string?)saved["AdminTwoFactor"]);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416", Justification = "Windows 以外でだけ実行する")]
    public void 保存したファイルは_所有者だけが読み書きできる()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = SecretKeyBootstrap.EnsureKey(Empty(), _dir)!;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
