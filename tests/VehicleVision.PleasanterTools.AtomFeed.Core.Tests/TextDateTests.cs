using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.AtomFeed.Syndication;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;

namespace VehicleVision.PleasanterTools.AtomFeed.Core.Tests;

/// <summary>日付が文字の列に入っているときの更新・公開日時。</summary>
public sealed class TextDateTests
{
    private static FeedEntry? Map(string json, FeedMapping mapping) =>
        EntryMapper.Map(
            JsonNode.Parse(json)!,
            new FeedDefinition(
                Guid.NewGuid(), "t", 1, mapping, new Dictionary<string, string>(), DateTimeOffset.UnixEpoch, false),
            TimeZoneInfo.Utc);

    private static FeedMapping Text(string updatedFormat, string? publishedColumn = null, string? publishedFormat = null) => new()
    {
        UpdatedColumn = "DescriptionA",
        UpdatedFormat = updatedFormat,
        PublishedColumn = publishedColumn,
        PublishedFormat = publishedFormat,
    };

    [Fact]
    public void 文字の列の日付を書式で読む()
    {
        var e = Map(
            """{ "IssueId": 1, "Title": "t", "DescriptionHash": { "DescriptionA": "2026/03/05 14:30" } }""",
            Text("yyyy/MM/dd HH:mm"));

        Assert.Equal(new DateTime(2026, 3, 5, 14, 30, 0), e!.Updated.DateTime);
    }

    [Fact]
    public void 和暦ではない日本語の書式も読める()
    {
        var e = Map(
            """{ "IssueId": 1, "Title": "t", "DescriptionHash": { "DescriptionA": "2026年3月5日" } }""",
            Text("yyyy年M月d日"));

        Assert.Equal(new DateTime(2026, 3, 5), e!.Updated.DateTime);
    }

    [Fact]
    public void 更新日時と公開日時を別の文字の列から読める()
    {
        var e = Map(
            """
            { "IssueId": 1, "Title": "t",
              "DescriptionHash": { "DescriptionA": "2026/03/05 09:00", "DescriptionB": "2026/03/05 17:30" } }
            """,
            new FeedMapping
            {
                UpdatedColumn = "DescriptionA",
                UpdatedFormat = "yyyy/MM/dd HH:mm",
                PublishedColumn = "DescriptionB",
                PublishedFormat = "yyyy/MM/dd HH:mm",
            });

        Assert.Equal(TimeSpan.FromMinutes(510), e!.Published - e.Updated);
    }

    [Fact]
    public void 前後の空白は無視する()
    {
        var e = Map(
            """{ "IssueId": 1, "Title": "t", "DescriptionHash": { "DescriptionA": "  2026/03/05  " } }""",
            Text("yyyy/MM/dd"));

        Assert.NotNull(e);
    }

    [Theory]
    [InlineData("")]
    [InlineData("未定")]
    [InlineData("2026-03-05")]
    [InlineData("2026/13/45")]
    public void 更新日時が空か読めなければ記事を出さない(string value)
    {
        var e = Map(
            $$"""{ "IssueId": 1, "Title": "t", "DescriptionHash": { "DescriptionA": "{{value}}" } }""",
            Text("yyyy/MM/dd"));

        Assert.Null(e);
    }

    [Fact]
    public void 公開日時が空なら公開日時は空で記事は出す()
    {
        var e = Map(
            """{ "IssueId": 1, "Title": "t", "DescriptionHash": { "DescriptionA": "2026/03/05 09:00", "DescriptionB": "" } }""",
            Text("yyyy/MM/dd HH:mm", "DescriptionB", "yyyy/MM/dd HH:mm"));

        Assert.NotNull(e);
        Assert.Null(e.Published);
    }

    [Fact]
    public void 公開日時が空でないのに読めなければ記事を出さない_黙って別の日付にしない()
    {
        var e = Map(
            """{ "IssueId": 1, "Title": "t", "DescriptionHash": { "DescriptionA": "2026/03/05 09:00", "DescriptionB": "来週" } }""",
            Text("yyyy/MM/dd HH:mm", "DescriptionB", "yyyy/MM/dd HH:mm"));

        Assert.Null(e);
    }
}
