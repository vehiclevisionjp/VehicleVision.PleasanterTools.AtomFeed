using System.Text.Json;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;

namespace VehicleVision.PleasanterTools.AtomFeed.Core.Tests;

/// <summary>フィードに出す記事の範囲。</summary>
public sealed class FeedRangeTests
{
    // 今日は 2026-10-15 の昼
    private static readonly DateTime Today = new(2026, 10, 15, 12, 30, 0);

    [Fact]
    public void 既定は_過去90日_最大100件()
    {
        Assert.Equal(new FeedRange(90, 100), FeedRange.Default);
    }

    [Fact]
    public void 期間の始まりは_今日の0時から数える()
    {
        Assert.Equal(new DateTime(2026, 7, 17), new FeedRange(90, null).Since(Today));
        Assert.Equal(new DateTime(2026, 10, 15), new FeedRange(0, null).Since(Today));
        Assert.Null(FeedRange.Unbounded.Since(Today));
    }

    [Theory]
    [InlineData("2026-07-16 23:59", false)]
    [InlineData("2026-07-17 00:00", true)]
    [InlineData("2026-10-15 09:00", true)]
    [InlineData("2099-01-01 00:00", true)] // 未来の更新日時（時計のずれ）でも落とさない
    public void 更新が期間の中の記事だけを出す(string updated, bool expected)
    {
        Assert.Equal(expected, new FeedRange(90, null).Includes(Today, DateTime.Parse(updated)));
    }

    [Fact]
    public void 日数が無制限なら_どの記事も出す()
    {
        Assert.True(FeedRange.Unbounded.Includes(Today, new DateTime(1900, 1, 1)));
    }

    [Theory]
    [InlineData(0, 1, null)]
    [InlineData(36500, 100000, null)]
    [InlineData(null, null, null)]
    [InlineData(-1, 1, "PastDays")]
    [InlineData(0, 0, "MaxItems")]
    [InlineData(36501, 1, "PastDays")]
    [InlineData(0, 100001, "MaxItems")]
    public void 値の範囲を検証する(int? past, int? max, string? expectedContains)
    {
        var error = new FeedRange(past, max).Validate("en");

        if (expectedContains is null)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.Contains(expectedContains, error);
            Assert.Contains("36500", error);
            Assert.Contains("100000", error);
        }
    }

    // ---- Pleasanter への粗い絞り込み ----

    private static string[] Ranges(IReadOnlyDictionary<string, string> filters, string column) =>
        JsonSerializer.Deserialize<string[]>(filters[column])!;

    [Fact]
    public void 更新日時の列には_期間の始まりから_の開区間を足す()
    {
        var mapping = new FeedMapping { UpdatedColumn = "UpdatedTime" };

        var filters = new FeedRange(90, 100).ApplyTo(new Dictionary<string, string>(), mapping, Today);

        Assert.Equal(["2026/07/17 00:00:00,"], Ranges(filters, "UpdatedTime"));
    }

    [Fact]
    public void 日数が無制限なら絞り込みを足さない()
    {
        var mapping = new FeedMapping();

        Assert.Empty(new FeedRange(null, 100).ApplyTo(new Dictionary<string, string>(), mapping, Today));
    }

    [Fact]
    public void 日付が文字の列は_Pleasanterでは絞れない_絞り込みを足さない()
    {
        var mapping = new FeedMapping { UpdatedColumn = "ClassA", UpdatedFormat = "yyyyMMdd" };

        Assert.Empty(new FeedRange(90, 100).ApplyTo(new Dictionary<string, string>(), mapping, Today));
    }

    [Fact]
    public void 利用者が同じ列へ書いた絞り込みは_上書きしない_元の絞り込みも残る()
    {
        var mapping = new FeedMapping { UpdatedColumn = "UpdatedTime" };
        var own = new Dictionary<string, string> { ["UpdatedTime"] = "[\"2026/01/01,\"]", ["ClassA"] = "[\"1\"]" };

        var filters = new FeedRange(90, 100).ApplyTo(own, mapping, Today);

        Assert.Equal("[\"2026/01/01,\"]", filters["UpdatedTime"]);
        Assert.Equal("[\"1\"]", filters["ClassA"]);
        Assert.Equal(2, own.Count);
    }

    [Fact]
    public void 渡した絞り込みの辞書は変えない()
    {
        var own = new Dictionary<string, string> { ["ClassA"] = "[\"1\"]" };

        var filters = new FeedRange(90, 100).ApplyTo(own, new FeedMapping(), Today);

        Assert.Single(own);
        Assert.Equal(2, filters.Count);
    }
}
