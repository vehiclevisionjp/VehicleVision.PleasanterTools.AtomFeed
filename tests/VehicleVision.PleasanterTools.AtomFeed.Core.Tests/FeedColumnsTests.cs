using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.AtomFeed.Syndication;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;

namespace VehicleVision.PleasanterTools.AtomFeed.Core.Tests;

public sealed class FeedColumnsTests
{
    [Theory]
    [InlineData("UpdatedTime")]
    [InlineData("CreatedTime")]
    [InlineData("DateA")]
    [InlineData("DateZ")]
    [InlineData("Date001")]
    [InlineData("Date100")]
    public void 日付の列として受け付ける(string column) => Assert.True(FeedColumns.IsDate(column));

    [Theory]
    [InlineData("Date")]
    [InlineData("DateAA")]
    [InlineData("Datea")]
    [InlineData("Date01")]
    [InlineData("Date0001")]
    [InlineData("Date00A")]
    [InlineData("ClassA")]
    [InlineData("Title")]
    [InlineData("DateA; DROP")]
    [InlineData("xDateA")]
    [InlineData("DateA\n")]
    [InlineData("")]
    public void 日付の列として受け付けない(string column) => Assert.False(FeedColumns.IsDate(column));

    [Theory]
    [InlineData("Title")]
    [InlineData("Body")]
    [InlineData("ClassB")]
    [InlineData("Class042")]
    [InlineData("DescriptionC")]
    [InlineData("Num001")]
    [InlineData("CheckA")]
    [InlineData("DateA")]
    public void 文字の列として受け付ける(string column) => Assert.True(FeedColumns.IsText(column));

    [Theory]
    [InlineData("Class")]
    [InlineData("ClassAB")]
    [InlineData("Class4")]
    [InlineData("Unknown")]
    [InlineData("Title; DROP")]
    public void 文字の列として受け付けない(string column) => Assert.False(FeedColumns.IsText(column));

    [Fact]
    public void 既定のマッピングは有効()
    {
        Assert.Null(new FeedMapping().Validate());
    }

    [Fact]
    public void 開始と終了に任意の日付列を指定できる()
    {
        Assert.Null(new FeedMapping { UpdatedColumn = "DateA", PublishedColumn = "Date001" }.Validate());
        Assert.Null(new FeedMapping { UpdatedColumn = "DateC", PublishedColumn = null }.Validate());
    }

    [Fact]
    public void 開始と終了に日付でない列は指定できない()
    {
        Assert.NotNull(new FeedMapping { UpdatedColumn = "ClassA" }.Validate());
        Assert.NotNull(new FeedMapping { PublishedColumn = "Title" }.Validate());
    }

    [Fact]
    public void 拡張列のDate001とDateAを混ぜて予定にできる()
    {
        var feed = new FeedDefinition(
            Guid.NewGuid(),
            "t",
            1,
            new FeedMapping { UpdatedColumn = "DateA", PublishedColumn = "Date001", AuthorColumn = "Class002" },
            new Dictionary<string, string>(),
            DateTimeOffset.UnixEpoch,
            false);

        var e = EntryMapper.Map(
            JsonNode.Parse("""
                { "ResultId": 3, "Title": "t",
                  "DateHash": { "DateA": "2026-03-01T09:00:00", "Date001": "2026-03-01T12:00:00" },
                  "ClassHash": { "Class002": "会議室" } }
                """)!,
            feed,
            TimeZoneInfo.Utc);

        Assert.NotNull(e);
        Assert.Equal(TimeSpan.FromHours(3), e.Published - e.Updated);
        Assert.Equal("会議室", e.Author);
    }
}

public sealed class SummaryTests
{
    private static FeedDefinition Feed(FeedMapping? m = null) => new(
        Guid.NewGuid(), "t", 1, m ?? new FeedMapping(), new Dictionary<string, string>(), DateTimeOffset.UnixEpoch, false);

    private static string? Summary(string json, FeedMapping? m = null) =>
        EntryMapper.Map(JsonNode.Parse(json)!, Feed(m), TimeZoneInfo.Utc)?.Title;

    [Fact]
    public void 既定は組み上げ済みのItemTitleを使う()
    {
        Assert.Equal(
            "A社 見積",
            Summary("""{ "IssueId": 1, "Title": "見積", "ItemTitle": "A社 見積", "UpdatedTime": "2026-03-01T09:00:00" }"""));
    }

    [Fact]
    public void ItemTitleが無ければTitleへ落とす()
    {
        Assert.Equal(
            "見積",
            Summary("""{ "IssueId": 1, "Title": "見積", "UpdatedTime": "2026-03-01T09:00:00" }"""));
    }

    [Fact]
    public void 件名の列にItemTitleとTitleを指定できる()
    {
        Assert.Null(new FeedMapping { TitleColumn = "ItemTitle" }.Validate());
        Assert.Null(new FeedMapping { TitleColumn = "Title" }.Validate());
        Assert.Equal(
            "見積",
            Summary(
                """{ "IssueId": 1, "Title": "見積", "ItemTitle": "A社 見積", "UpdatedTime": "2026-03-01T09:00:00" }""",
                new FeedMapping { TitleColumn = "Title" }));
    }
}
