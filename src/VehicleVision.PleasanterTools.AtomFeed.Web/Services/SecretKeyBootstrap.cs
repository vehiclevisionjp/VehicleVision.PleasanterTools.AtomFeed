using System.Text.Json;
using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.AtomFeed.Security;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Services;

/// <summary>
/// 秘密の鍵（<c>ATOMFEED_SECRET_KEY</c>）が無いとき、**初回の起動で作って <c>Security.local.json</c> へ保存する。**
/// </summary>
/// <remarks>
/// <para>
/// 手で鍵を用意する手間（<c>--generate-secret-key</c>）を省くため。**鍵が既にあれば何もしない**
/// （環境変数・ファイルのどちらでも。上書きしない）。
/// </para>
/// <para>
/// ⚠️ **保存できなければ起動を止める**（IdentityProvider の <c>SecretKeyProvisioner</c> と同じ）。保存できないまま使うと、再起動で鍵を失い、
/// DB に暗号化して置いた API キーが全部使えなくなる。読み取り専用のファイルシステムのコンテナなどでは、デプロイ先の秘密の保管先へ
/// <c>ATOMFEED_SECRET_KEY</c>（または <c>ATOMFEED_SECRET_KEY_FILE</c>）を置く。
/// </para>
/// <para>
/// ⚠️ **自動で作った鍵は、DB に管理者もフィードも無いときだけ使ってよい。** 鍵を失ったまま作り直すと、DB に暗号化して置いたものを復号できず、
/// 黙って別の鍵で動くと気付けない。**作った直後に DB に管理者かフィードがあれば、作った鍵をファイルから消して起動を止める**（<see cref="Discard"/>）。
/// </para>
/// <para>
/// ⚠️ **生成した鍵は、そのディスクが残る限りの鍵。** コンテナを作り直すと消える。本番では、作られた鍵を秘密の保管先へ移す
/// （運用手順書 8 章）。止めるには <c>ATOMFEED_SECRET_KEY_AUTOGENERATE=false</c>（<c>Security.json</c> の <c>SecretKeyAutoGenerate</c>）。
/// </para>
/// </remarks>
public static class SecretKeyBootstrap
{
    /// <summary>自動生成の切り替え（既定 true）。</summary>
    public const string AutoGenerateKey = "ATOMFEED_SECRET_KEY_AUTOGENERATE";

    /// <summary>生成して保存したファイルのパス（生成したときだけ入る。起動のログに出す）。</summary>
    public const string GeneratedFileKey = "ATOMFEED_SECRET_KEY_GENERATED_PATH";

    private const string FileName = "Security.local.json";

    /// <summary>
    /// 鍵が無ければ作って保存し、設定へ積む。作った（保存した）ファイルのパス。作らなかったら <c>null</c>。
    /// </summary>
    /// <param name="builder">設定。**環境変数・ファイルを積んだ後に呼ぶ。**</param>
    /// <param name="directory">Parameters の置き場。</param>
    /// <exception cref="InvalidOperationException">鍵を保存できなかったとき。</exception>
    public static string? EnsureKey(IConfigurationBuilder builder, string directory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var current = builder.Build();
        if (!string.IsNullOrWhiteSpace(current[AppSettings.SecretKey])
            || string.Equals(current[AutoGenerateKey] ?? current["SecretKeyAutoGenerate"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = SecretProtector.GenerateKey();
        var path = Path.Combine(directory, FileName);
        try
        {
            Save(path, key);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 保存できないなら止める（作っても、再起動で鍵を失うため）。コンソールへ出る文なので英語
            throw new InvalidOperationException(
                $"ATOMFEED_SECRET_KEY is not set, and a new key could not be saved to {path} ({ex.GetType().Name}). "
                + "Set ATOMFEED_SECRET_KEY (Base64 of 32 bytes; generate with --generate-secret-key) or ATOMFEED_SECRET_KEY_FILE, or make the directory writable.",
                ex);
        }

        builder.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(AppSettings.SecretKey, key),
            new KeyValuePair<string, string?>(GeneratedFileKey, path),
        ]);
        return path;
    }

    /// <summary>既にある <c>Security.local.json</c> の他のキーを残して、<c>SecretKey</c> だけを書く。</summary>
    private static void Save(string path, string key)
    {
        JsonObject root;
        if (File.Exists(path))
        {
            root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }) as JsonObject
                ?? throw new JsonException("Security.local.json is not a JSON object.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            root = [];
        }

        root["SecretKey"] = key;
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>自動で作った鍵を、ファイルから消す（DB に暗号化して置いたものが読めなくなる事故を避けるため）。</summary>
    public static void Discard(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
        if (root is null)
        {
            return;
        }

        root.Remove("SecretKey");
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
