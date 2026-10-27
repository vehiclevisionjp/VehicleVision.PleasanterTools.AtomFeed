using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.AtomFeed.Syndication;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;

namespace VehicleVision.PleasanterTools.AtomFeed.Core.Tests;

public sealed class TextTemplateTests
{
    [Fact]
    public void 固定の文字と列と書式に分ける()
    {
        var parts = TextTemplate.Parse("{ClassA}：{Title}（{DateA:yyyy/MM/dd HH:mm}）");

        Assert.Collection(
            parts,
            p => Assert.Equal(new TextTemplate.Part(null, "ClassA"), p),
            p => Assert.Equal(new TextTemplate.Part("："), p),
            p => Assert.Equal(new TextTemplate.Part(null, "Title"), p),
            p => Assert.Equal(new TextTemplate.Part("（"), p),
            p => Assert.Equal(new TextTemplate.Part(null, "DateA", "yyyy/MM/dd HH:mm"), p),
            p => Assert.Equal(new TextTemplate.Part("）"), p));
    }

    [Fact]
    public void 波括弧そのものは二重に書く()
    {
        var text = TextTemplate.Render(TextTemplate.Parse("{{{Title}}}"), (c, _) => "x");

        Assert.Equal("{x}", text);
    }

    [Fact]
    public void 書式には区切りのコロンを含められる()
    {
        var part = TextTemplate.Parse("{DateA:HH:mm:ss}").Single();

        Assert.Equal("HH:mm:ss", part.Format);
    }

    [Theory]
    [InlineData("{Title")]
    [InlineData("Title}")]
    [InlineData("{}")]
    [InlineData("{:yyyy}")]
    [InlineData("{DateA:}")]
    [InlineData("{1A}")]
    [InlineData("{Class A}")]
    [InlineData("{Title; DROP}")]
    public void 不正なテンプレートはFormatException(string template) =>
        Assert.ThrowsAny<FormatException>(() => TextTemplate.Parse(template));

    [Fact]
    public void 長すぎるテンプレートは弾く() =>
        Assert.ThrowsAny<FormatException>(() => TextTemplate.Parse(new string('a', TextTemplate.MaxLength + 1)));

    [Fact]
    public void 前後の空白は落とし値が空でも崩れない()
    {
        var text = TextTemplate.Render(TextTemplate.Parse("{ClassA} {Title}"), (c, _) => c == "ClassA" ? "" : "見積");

        Assert.Equal("見積", text);
    }
}

public sealed class FormatValidationTests
{
    [Fact]
    public void 件名テンプレートに日付と数値の書式を与えられる()
    {
        var mapping = new FeedMapping { TitleTemplate = "{Title} {DateA:yyyy/MM/dd} {NumA:#,##0.00}" };

        Assert.Null(mapping.Validate());
    }

    [Fact]
    public void 日付と数値の列に書式が無ければ弾く()
    {
        Assert.NotNull(new FeedMapping { TitleTemplate = "{Title} {DateA}" }.Validate());
        Assert.NotNull(new FeedMapping { TitleTemplate = "{Title} {NumA}" }.Validate());
        Assert.NotNull(new FeedMapping { AuthorColumn = "DateA" }.Validate());
        Assert.NotNull(new FeedMapping { ContentColumn = "NumA" }.Validate());
        Assert.NotNull(new FeedMapping { TitleColumn = "DateA" }.Validate());
    }

    [Fact]
    public void Formatsで列ごとの書式を与えると単一の列にも使える()
    {
        var formats = new Dictionary<string, string> { ["DateA"] = "yyyy/MM/dd", ["NumA"] = "N0" };

        Assert.Null(new FeedMapping { AuthorColumn = "DateA", ContentColumn = "NumA", Formats = formats }.Validate());
        Assert.Null(new FeedMapping { TitleTemplate = "{DateA}", Formats = formats }.Validate());
    }

    [Fact]
    public void 書式として使えないものは弾く()
    {
        Assert.NotNull(new FeedMapping { TitleTemplate = "{DateA:%}" }.Validate());
        Assert.NotNull(new FeedMapping { TitleTemplate = "{NumA:q}" }.Validate());
        Assert.NotNull(new FeedMapping { AuthorColumn = "DateA", Formats = new Dictionary<string, string> { ["DateA"] = "x\ny" } }.Validate());
    }

    [Fact]
    public void Formatsに指定できるのは日付と数値の列だけ()
    {
        Assert.NotNull(new FeedMapping { Formats = new Dictionary<string, string> { ["ClassA"] = "yyyy" } }.Validate());
        Assert.NotNull(new FeedMapping { Formats = new Dictionary<string, string> { ["Title"] = "yyyy" } }.Validate());
    }

    [Fact]
    public void テンプレートに列が無い_指定できない列_書式つきの文字の列は弾くか受け付ける()
    {
        Assert.NotNull(new FeedMapping { TitleTemplate = "固定の文字だけ" }.Validate());
        Assert.NotNull(new FeedMapping { TitleTemplate = "{Unknown}" }.Validate());
        Assert.NotNull(new FeedMapping { TitleTemplate = "{Title" }.Validate());
        Assert.Null(new FeedMapping { TitleTemplate = "{ClassA}-{Description001}-{Status}-{Manager}" }.Validate());
    }

    [Fact]
    public void 表示名への解決が要る列を数える()
    {
        var mapping = new FeedMapping
        {
            TitleTemplate = "{ClassA}：{Title}：{Owner}：{DateA:yyyy}",
            AuthorColumn = "Class001",
            ContentColumn = "ClassA",
        };

        Assert.Equal(["ClassA", "Owner", "Class001"], mapping.ResolvedColumns());
        Assert.Empty(new FeedMapping().ResolvedColumns());
        Assert.Empty(new FeedMapping { TitleTemplate = "{Title}-{Body}" }.ResolvedColumns());
    }

    [Theory]
    [InlineData("Status", true)]
    [InlineData("Manager", true)]
    [InlineData("ClassA", true)]
    [InlineData("Class100", true)]
    [InlineData("Title", false)]
    [InlineData("DateA", false)]
    [InlineData("NumA", false)]
    [InlineData("DescriptionA", false)]
    public void 解決が要る列の判定(string column, bool expected) =>
        Assert.Equal(expected, FeedColumns.NeedsResolution(column));
}

public sealed class FormattedTextMappingTests
{
    private static FeedDefinition Feed(FeedMapping mapping) => new(
        Guid.NewGuid(), "t", 1, mapping, new Dictionary<string, string>(), DateTimeOffset.UnixEpoch, false);

    private static FeedEntry? Map(string json, FeedMapping mapping, IReadOnlyDictionary<string, string>? resolved = null) =>
        EntryMapper.Map(JsonNode.Parse(json)!, Feed(mapping), TimeZoneInfo.Utc, null, resolved);

    private const string Record = """
        { "IssueId": 7, "Title": "見積", "ItemTitle": "A社 見積", "UpdatedTime": "2026-03-01T09:00:00",
          "ClassHash": { "ClassA": "2", "ClassB": "1" },
          "DateHash": { "DateA": "2026-03-05T14:30:00", "DateB": "1899-12-30T00:00:00" },
          "NumHash": { "NumA": 1234567.891 } }
        """;

    [Fact]
    public void テンプレートで日付と数値を書式で整形して組み合わせる()
    {
        var e = Map(Record, new FeedMapping { TitleTemplate = "{Title} {DateA:yyyy/MM/dd HH:mm} {NumA:#,##0.00}" });

        Assert.Equal("見積 2026/03/05 14:30 1,234,567.89", e!.Title);
    }

    [Fact]
    public void 書式はインバリアントカルチャで解釈する()
    {
        var current = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var e = Map(Record, new FeedMapping { TitleTemplate = "{NumA:N2}|{DateA:MMM dd}" });

            Assert.Equal("1,234,567.89|Mar 05", e!.Title);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = current;
        }
    }

    [Fact]
    public void 未設定の日付は空文字になる()
    {
        var e = Map(Record, new FeedMapping { TitleTemplate = "{Title}[{DateB:yyyy/MM/dd}]" });

        Assert.Equal("見積[]", e!.Title);
    }

    [Fact]
    public void 選択肢は解決済みの表示名を使い無ければ生の値()
    {
        var resolved = new Dictionary<string, string> { ["ClassA"] = "出張" };

        var e = Map(Record, new FeedMapping { TitleTemplate = "{ClassA}/{ClassB}" }, resolved);

        Assert.Equal("出張/1", e!.Title);
    }

    [Fact]
    public void 場所と説明にも表示名と書式が効く()
    {
        var e = Map(
            Record,
            new FeedMapping
            {
                AuthorColumn = "ClassA",
                ContentColumn = "DateA",
                Formats = new Dictionary<string, string> { ["DateA"] = "yyyy-MM-dd" },
            },
            new Dictionary<string, string> { ["ClassA"] = "会議室" });

        Assert.Equal("会議室", e!.Author);
        Assert.Equal("2026-03-05", e.Content);
    }

    [Fact]
    public void 件名の既定はItemTitleのまま()
    {
        Assert.Equal("A社 見積", Map(Record, new FeedMapping())!.Title);
    }

    [Fact]
    public void テンプレートの結果が空なら仮の件名()
    {
        var e = Map(Record, new FeedMapping { TitleTemplate = "{ClassC}" });

        Assert.Equal("(無題)", e!.Title);
    }
}
