using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>フィードに出す記事の範囲。時計は 2026-10-13 09:00（UTC）。</summary>
public sealed class FeedRangeEndpointTests : IDisposable
{
    private readonly TestApp _app = new();

    public FeedRangeEndpointTests()
    {
        // 古い記事（2024 年）・90 日以内の古い方（2026-09）・新しい方（2026-10）の 3 件。Pleasanter は並びを無視して返す（本アプリが並べ直す）
        _app.Pleasanter.Record = Record(1, "古い記事", "2024-01-10T09:00:00") + ","
            + Record(2, "九月の記事", "2026-09-01T09:00:00") + ","
            + Record(3, "十月の記事", "2026-10-12T09:00:00");
    }

    public void Dispose() => _app.Dispose();

    private static string Record(int id, string title, string updated) =>
        $$"""{ "IssueId": {{id}}, "Ver": 1, "Title": "{{title}}", "UpdatedTime": "{{updated}}", "CreatedTime": "2024-01-01T00:00:00" }""";

    private async Task<(string Path, JsonElement Feed)> CreateAsync(object body)
    {
        var response = await _app.AdminClient().PostAsJsonAsync("/admin/api/feeds", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (new Uri(json.GetProperty("url").GetString()!).AbsolutePath, json.GetProperty("feed"));
    }

    private async Task<string> FeedAsync(string path)
    {
        var response = await _app.Client().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task 作成のとき範囲を省くと_既定は過去90日_最大100件()
    {
        var (path, feed) = await CreateAsync(new { name = "共有", siteId = 1 });

        Assert.Equal(90, feed.GetProperty("range").GetProperty("pastDays").GetInt32());
        Assert.Equal(100, feed.GetProperty("range").GetProperty("maxItems").GetInt32());

        var atom = await FeedAsync(path);
        Assert.DoesNotContain("古い記事", atom);   // 90 日より前
        Assert.Contains("九月の記事", atom);
        Assert.Contains("十月の記事", atom);
    }

    [Fact]
    public async Task 記事は更新の新しい順に並ぶ()
    {
        var (path, _) = await CreateAsync(new { name = "共有", siteId = 1 });

        var atom = await FeedAsync(path);

        Assert.True(atom.IndexOf("十月の記事", StringComparison.Ordinal) < atom.IndexOf("九月の記事", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 最大件数を超えた分は_古い方から落ちる()
    {
        var (path, _) = await CreateAsync(new { name = "共有", siteId = 1, pastDays = 90, maxItems = 1 });

        var atom = await FeedAsync(path);

        Assert.Contains("十月の記事", atom);
        Assert.DoesNotContain("九月の記事", atom);
    }

    [Fact]
    public async Task Pleasanterへは_更新日時の列の粗い絞り込みと降順の並びを足して取りに行く()
    {
        var (path, _) = await CreateAsync(new { name = "共有", siteId = 1, pastDays = 90, maxItems = 30 });

        await FeedAsync(path);

        var view = JsonDocument.Parse(_app.Pleasanter.Bodies[0]).RootElement.GetProperty("View");
        // 今日 2026-10-13 の 90 日前は 2026-07-15
        Assert.Equal(["2026/07/15 00:00:00,"], JsonSerializer.Deserialize<string[]>(view.GetProperty("ColumnFilterHash").GetProperty("UpdatedTime").GetString()!)!);
        Assert.Equal("desc", view.GetProperty("ColumnSorterHash").GetProperty("UpdatedTime").GetString());
    }

    [Fact]
    public async Task 両側を無制限にすると_期間では絞らず_すべて出す()
    {
        var (path, feed) = await CreateAsync(new { name = "共有", siteId = 1, pastDays = (int?)null, maxItems = (int?)null });

        Assert.Equal(JsonValueKind.Null, feed.GetProperty("range").GetProperty("pastDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("range").GetProperty("maxItems").ValueKind);
        var atom = await FeedAsync(path);

        Assert.Contains("古い記事", atom);
        Assert.Contains("九月の記事", atom);
        Assert.Contains("十月の記事", atom);
        Assert.DoesNotContain("ColumnFilterHash", _app.Pleasanter.Bodies[0]);
    }

    [Fact]
    public async Task 編集で範囲を変えられる_キャッシュは捨てられ_次の取得から効く()
    {
        var (path, feed) = await CreateAsync(new { name = "共有", siteId = 1 });
        Assert.DoesNotContain("古い記事", await FeedAsync(path));
        var id = feed.GetProperty("id").GetString();

        var put = await _app.AdminClient().PutAsJsonAsync($"/admin/api/feeds/{id}", new { name = "共有", siteId = 1, pastDays = (int?)null, maxItems = 2 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var view = (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("range");
        Assert.Equal(JsonValueKind.Null, view.GetProperty("pastDays").ValueKind);
        Assert.Equal(2, view.GetProperty("maxItems").GetInt32());
        var atom = await FeedAsync(path);
        Assert.DoesNotContain("古い記事", atom);   // 日数は無制限だが、新しい 2 件だけ
        Assert.Contains("九月の記事", atom);
        Assert.Contains("十月の記事", atom);
    }

    [Theory]
    [InlineData(-1, null, "PastDays")]
    [InlineData(null, 0, "MaxItems")]
    [InlineData(null, 100001, "MaxItems")]
    public async Task 範囲外の値は断る_言語ごとの文言(int? past, int? max, string expected)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/api/feeds")
        {
            Content = JsonContent.Create(new { name = "共有", siteId = 1, pastDays = past, maxItems = max }),
        };
        request.Headers.Add("Accept-Language", "en");

        var response = await _app.AdminClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains(expected, error);
        Assert.Contains("36500", error);
    }
}
