using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;
using VehicleVision.PleasanterTools.AtomFeed.Syndication;

namespace VehicleVision.PleasanterTools.AtomFeed.Core.Tests;

public sealed class EntryMapperTests
{
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone(
        "JST", TimeSpan.FromHours(9), "JST", "JST");

    private static FeedDefinition Feed(FeedMapping? mapping = null) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "テスト",
        42,
        mapping ?? new FeedMapping(),
        new Dictionary<string, string>(),
        DateTimeOffset.UnixEpoch,
        false);

    private static JsonNode Record(string json) => JsonNode.Parse(json)!;

    [Fact]
    public void 標準の列から記事を作る()
    {
        var e = EntryMapper.Map(
            Record("""
                { "IssueId": 7, "Ver": 3, "Title": "お知らせ", "Body": "本文です",
                  "CreatedTime": "2026-03-01T08:00:00", "UpdatedTime": "2026-03-02T09:30:00" }
                """),
            Feed(),
            Tokyo,
            "https://pleasanter.example.com/");

        Assert.NotNull(e);
        Assert.Equal("urn:atomfeed:11111111111111111111111111111111:42:7", e.Id);
        Assert.Equal(7, e.RecordId);
        Assert.Equal("お知らせ", e.Title);
        Assert.Equal("本文です", e.Content);
        Assert.Equal(new DateTimeOffset(2026, 3, 2, 0, 30, 0, TimeSpan.Zero), e.Updated.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 23, 0, 0, TimeSpan.Zero), e.Published!.Value.ToUniversalTime());
        Assert.Equal("https://pleasanter.example.com/items/7", e.Url);
        Assert.Null(e.Author);
    }

    [Fact]
    public void 型付きの列はHashから読む()
    {
        var mapping = new FeedMapping
        {
            UpdatedColumn = "DateA",
            PublishedColumn = "DateB",
            AuthorColumn = "ClassA",
            ContentColumn = null,
        };
        var e = EntryMapper.Map(
            Record("""
                { "ResultId": 9, "Title": "t",
                  "DateHash": { "DateA": "2026-03-01T09:00:00", "DateB": "2026-03-01T07:00:00" },
                  "ClassHash": { "ClassA": "山田" } }
                """),
            Feed(mapping),
            Tokyo);

        Assert.NotNull(e);
        Assert.Equal("山田", e.Author);
        Assert.Equal(TimeSpan.FromHours(2), e.Updated - e.Published);
        Assert.Null(e.Content);
    }

    [Fact]
    public void 更新日時が未設定なら記事にしない()
    {
        Assert.Null(EntryMapper.Map(
            Record("""{ "IssueId": 1, "Title": "t", "UpdatedTime": "1899-12-30T00:00:00" }"""), Feed(), Tokyo));
        Assert.Null(EntryMapper.Map(Record("""{ "IssueId": 1, "Title": "t" }"""), Feed(), Tokyo));
    }

    [Fact]
    public void 公開日時が未設定なら公開日時は空で記事は出す()
    {
        var e = EntryMapper.Map(
            Record("""
                { "IssueId": 1, "Title": "t", "UpdatedTime": "2026-03-01T09:00:00",
                  "CreatedTime": "1899-12-30T00:00:00" }
                """),
            Feed(),
            Tokyo);

        Assert.NotNull(e);
        Assert.Null(e.Published);
    }

    [Fact]
    public void 公開日時の列を指定しなければ公開日時は空()
    {
        var e = EntryMapper.Map(
            Record("""{ "IssueId": 1, "Title": "t", "UpdatedTime": "2026-03-01T09:00:00" }"""),
            Feed(new FeedMapping { PublishedColumn = null }),
            Tokyo);

        Assert.Null(e!.Published);
    }

    [Fact]
    public void タイトルが空なら仮のタイトルを付ける()
    {
        var e = EntryMapper.Map(
            Record("""{ "IssueId": 1, "Title": "", "UpdatedTime": "2026-03-01T09:00:00" }"""), Feed(), Tokyo);

        Assert.Equal("(無題)", e!.Title);
    }

    [Fact]
    public void レコードIDが無ければ記事にしない()
    {
        Assert.Null(EntryMapper.Map(
            Record("""{ "Title": "t", "UpdatedTime": "2026-03-01T09:00:00" }"""), Feed(), Tokyo));
    }

    [Fact]
    public void 記事のIDはレコードごとに変わらない()
    {
        var feed = Feed();

        Assert.Equal(EntryMapper.DerivedId(feed, 5), EntryMapper.DerivedId(feed, 5));
        Assert.NotEqual(EntryMapper.DerivedId(feed, 5), EntryMapper.DerivedId(feed, 6));
        Assert.StartsWith(EntryMapper.ChannelId(feed), EntryMapper.DerivedId(feed, 5), StringComparison.Ordinal);
    }
}
