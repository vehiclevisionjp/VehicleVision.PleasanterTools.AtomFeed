using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VehicleVision.PleasanterTools.Core.Data;
using VehicleVision.PleasanterTools.AtomFeed.Localization;
using VehicleVision.PleasanterTools.AtomFeed.Security;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>共通仕様 U7・T3〜T5: 本人確認と登録専用セッションを迂回できない。</summary>
public sealed class AdminTotpReenrollmentTests : IDisposable
{
    private readonly TestApp _app = new(setUpAdmin: false);

    public void Dispose() => _app.Dispose();

    private async Task<(HttpClient Client, Guid UserId, string SessionCookie, string[] Codes)> EnrollAsync()
    {
        var client = _app.Client();
        await client.PostAsJsonAsync("/admin/api/setup", new { loginId = TestApp.AdminLoginId, password = TestApp.AdminPassword });
        var login = await client.PostAsJsonAsync("/admin/api/login", new { loginId = TestApp.AdminLoginId, password = TestApp.AdminPassword });
        var sessionCookie = CookieOf(login, AdminSessionGuard.SessionCookie);
        var begin = await client.PostAsync("/admin/api/me/totp/begin", null);
        var secret = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        var complete = await CompleteAsync(client, secret);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var codes = (await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes")
            .EnumerateArray().Select(code => code.GetString()!).ToArray();
        var me = await client.GetFromJsonAsync<JsonElement>("/admin/api/me/");
        _app.Time.Advance(TimeSpan.FromMinutes(5));
        return (client, me.GetProperty("id").GetGuid(), sessionCookie, codes);
    }

    private static string CookieOf(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie")
            .Last(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal)).Split(';')[0];

    private static Task<HttpResponseMessage> BeginAsync(HttpClient client, string? password = TestApp.AdminPassword) =>
        client.PostAsJsonAsync("/admin/api/me/totp/begin", new { password });

    private Task<HttpResponseMessage> CompleteAsync(HttpClient client, string secret) =>
        client.PostAsJsonAsync("/admin/api/me/totp/complete", new { code = TotpService.Compute(secret, _app.Time.Now.UtcDateTime) });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong password")]
    public async Task 再登録の開始には正しいパスワードの再確認が要る(string? password)
    {
        var (client, userId, _, _) = await EnrollAsync();
        var response = await BeginAsync(client, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ServerMessages.Get(ServerMessageKeys.CurrentPasswordRejected, "en"), body.GetProperty("error").GetString());
        Assert.Null(await _app.Service<IAdminUserStore>().GetTotpPendingAsync(userId));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task 本文なしの再登録も拒み_途中の鍵があっても通常セッションだけでは完了できない()
    {
        var (client, userId, _, _) = await EnrollAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/admin/api/me/totp/begin", null)).StatusCode);
        var secret = TotpService.GenerateSecret();
        await _app.Service<IAdminUserStore>().SetTotpPendingAsync(userId, secret);

        var response = await CompleteAsync(client, secret);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ServerMessages.Get(ServerMessageKeys.EnrollmentRestartRequired, "en"),
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task 再確認後に再登録でき_旧復旧コードと登録途中のセッションは失効する()
    {
        var (client, _, _, oldCodes) = await EnrollAsync();
        var begin = await BeginAsync(client);
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var cookie = CookieOf(begin, AdminSessionGuard.ReenrollCookie);
        Assert.Contains("httponly", begin.Headers.GetValues("Set-Cookie").Last(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", begin.Headers.GetValues("Set-Cookie").Last(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=300", begin.Headers.GetValues("Set-Cookie").Last(), StringComparison.OrdinalIgnoreCase);
        var id = cookie[(cookie.IndexOf('=') + 1)..];
        Assert.Equal(AdminSessionGuard.ReenrollKind, (await _app.Service<IAdminSessionStore>().FindAsync(id))!.Kind);
        var secret = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;

        var complete = await CompleteAsync(client, secret);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var codes = (await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes")
            .EnumerateArray().Select(code => code.GetString()!).ToArray();
        Assert.Equal(10, codes.Length);
        Assert.Null(await _app.Service<IAdminSessionStore>().FindAsync(id));
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(client, secret)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/api/me/")).StatusCode);

        var other = _app.Client();
        await other.PostAsJsonAsync("/admin/api/login", new { loginId = TestApp.AdminLoginId, password = TestApp.AdminPassword });
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await other.PostAsJsonAsync("/admin/api/login/recovery", new { code = oldCodes[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await other.PostAsJsonAsync("/admin/api/login/recovery", new { code = codes[0] })).StatusCode);
    }

    [Fact]
    public async Task 再登録の誤パスワードを締め出しに数え_上限の次から423になる()
    {
        var (client, _, _, _) = await EnrollAsync();
        var begin = await BeginAsync(client);
        var secret = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await BeginAsync(client, "wrong password")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Locked, (await BeginAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Locked, (await CompleteAsync(client, secret)).StatusCode);
        _app.Time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.OK, (await BeginAsync(client)).StatusCode);
    }

    [Fact]
    public async Task 空のパスワードはQと同じく締め出しに数えない()
    {
        var (client, _, _, _) = await EnrollAsync();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await BeginAsync(client, "")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await BeginAsync(client)).StatusCode);
    }

    [Fact]
    public async Task 再登録の開始もログインと共通のIP制限で11回目に429になる()
    {
        var (client, _, _, _) = await EnrollAsync();
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await BeginAsync(client, "")).StatusCode);
        }

        var limited = await BeginAsync(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter!.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/admin/api/login", new { loginId = TestApp.AdminLoginId, password = TestApp.AdminPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/admin/api/me/totp/complete", new { code = "123456" })).StatusCode);
    }

    [Fact]
    public async Task 登録途中は5分で失効し_通常セッションは残る()
    {
        var (client, _, _, _) = await EnrollAsync();
        var begin = await BeginAsync(client);
        var secret = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        _app.Time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(client, secret)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/api/me/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await BeginAsync(client)).StatusCode);
    }

    [Theory]
    [InlineData(AdminSessionKinds.Pending, false)]
    [InlineData(AdminSessionKinds.Session, false)]
    [InlineData(AdminSessionGuard.ReenrollKind, true)]
    public async Task 別の段階や別人の登録途中では完了できない(string kind, bool otherUser)
    {
        var (_, userId, sessionCookie, _) = await EnrollAsync();
        var secret = TotpService.GenerateSecret();
        var store = _app.Service<IAdminUserStore>();
        var target = userId;
        // 別人のセッションは、実在する別の管理者を招待して作る。
        if (otherUser)
        {
            var owner = _app.RawClient();
            owner.DefaultRequestHeaders.Add("Cookie", sessionCookie);
            var invitation = await owner.PostAsJsonAsync("/admin/api/users", new { loginId = "other", role = "Editor", language = "ja" });
            Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
            target = (await invitation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();
        }

        var id = await _app.Service<IAdminSessionStore>().CreateAsync(target, kind, TimeSpan.FromMinutes(5));
        await store.SetTotpPendingAsync(userId, secret);
        var client = _app.RawClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{sessionCookie}; {AdminSessionGuard.ReenrollCookie}={id}");

        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(client, secret)).StatusCode);
    }

    [Fact]
    public async Task 登録途中のcookieはログイン途中にも通常ログインにも流用できない()
    {
        var (owner, _, _, _) = await EnrollAsync();
        var begin = await BeginAsync(owner);
        var secret = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        var cookie = CookieOf(begin, AdminSessionGuard.ReenrollCookie);
        var id = cookie[(cookie.IndexOf('=') + 1)..];
        var onlyReenroll = _app.RawClient();
        onlyReenroll.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteAsync(onlyReenroll, secret)).StatusCode);
        var client = _app.RawClient();
        client.DefaultRequestHeaders.Add("Cookie",
            $"{cookie}; {AdminSessionGuard.PendingCookie}={id}; {AdminSessionGuard.SessionCookie}={id}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/admin/api/me/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await BeginAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteAsync(client, secret)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/admin/api/login/totp", new { code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/admin/api/login/recovery", new { code = "ABCDE-FGHIJ" })).StatusCode);
    }

    [Fact]
    public async Task 開始し直しとログアウトで古い登録途中をサーバ側から消す()
    {
        var (client, _, _, _) = await EnrollAsync();
        var first = CookieOf(await BeginAsync(client), AdminSessionGuard.ReenrollCookie).Split('=')[1];
        var second = CookieOf(await BeginAsync(client), AdminSessionGuard.ReenrollCookie).Split('=')[1];
        var sessions = _app.Service<IAdminSessionStore>();
        Assert.Null(await sessions.FindAsync(first));
        Assert.NotNull(await sessions.FindAsync(second));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/admin/api/logout", null)).StatusCode);
        Assert.Null(await sessions.FindAsync(second));
    }

    [Fact]
    public async Task パスワード変更で登録途中は失効し_通常セッションだけでは確定できない()
    {
        var (client, _, _, _) = await EnrollAsync();
        var begin = await BeginAsync(client);
        var reenroll = CookieOf(begin, AdminSessionGuard.ReenrollCookie).Split('=')[1];
        var secret = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;

        var changed = await client.PostAsJsonAsync("/admin/api/me/password",
            new { currentPassword = TestApp.AdminPassword, newPassword = "a new long password for reenrollment" });

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Null(await _app.Service<IAdminSessionStore>().FindAsync(reenroll));
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(client, secret)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/api/me/")).StatusCode);
    }
}
