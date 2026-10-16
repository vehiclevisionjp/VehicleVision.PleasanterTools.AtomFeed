using System.Xml.Linq;
using VehicleVision.PleasanterTools.AtomFeed.Syndication;

namespace VehicleVision.PleasanterTools.AtomFeed.Core.Tests;

public sealed class FeedWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    private static FeedChannel Channel(string title = "テスト") =>
        new("urn:atomfeed:abc", title, "ja", "https://feed.example.com/feeds/t.atom", "https://pleasanter.example.com/", Now);

    private static FeedEntry Entry(string id = "urn:atomfeed:abc:1:1", string title = "お知らせ") => new()
    {
        Id = id,
        Title = title,
        Updated = new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.FromHours(9)),
        Published = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.FromHours(9)),
        Content = "本文",
        Author = "山田",
        Url = "https://pleasanter.example.com/items/1",
    };

    // ---- Atom ----

    [Fact]
    public void Atomは必須の要素を持ち_日時はUTCで出る()
    {
        var doc = XDocument.Parse(AtomWriter.Write(Channel(), [Entry()]));
        var feed = doc.Root!;

        Assert.Equal(Atom + "feed", feed.Name);
        Assert.Equal("ja", (string?)feed.Attribute(XNamespace.Xml + "lang"));
        Assert.Equal("urn:atomfeed:abc", (string?)feed.Element(Atom + "id"));
        Assert.Equal("テスト", (string?)feed.Element(Atom + "title"));
        Assert.Equal("2026-03-02T00:30:00Z", (string?)feed.Element(Atom + "updated"));

        var entry = feed.Element(Atom + "entry")!;
        Assert.Equal("urn:atomfeed:abc:1:1", (string?)entry.Element(Atom + "id"));
        Assert.Equal("お知らせ", (string?)entry.Element(Atom + "title"));
        Assert.Equal("2026-03-02T00:30:00Z", (string?)entry.Element(Atom + "updated"));
        Assert.Equal("2026-02-28T23:00:00Z", (string?)entry.Element(Atom + "published"));
        Assert.Equal("山田", (string?)entry.Element(Atom + "author")!.Element(Atom + "name"));
        Assert.Equal("text", (string?)entry.Element(Atom + "content")!.Attribute("type"));
        Assert.Equal("https://pleasanter.example.com/items/1", (string?)entry.Element(Atom + "link")!.Attribute("href"));
    }

    [Fact]
    public void Atomの_self_は購読URL_alternate_は元のサイト()
    {
        var links = XDocument.Parse(AtomWriter.Write(Channel(), [])).Root!.Elements(Atom + "link").ToList();

        Assert.Equal("https://feed.example.com/feeds/t.atom", (string?)links.Single(l => (string?)l.Attribute("rel") == "self")!.Attribute("href"));
        Assert.Equal("https://pleasanter.example.com/", (string?)links.Single(l => (string?)l.Attribute("rel") == "alternate")!.Attribute("href"));
    }

    [Fact]
    public void Atomの記事が無ければ_feedのupdatedは出力時刻()
    {
        var feed = XDocument.Parse(AtomWriter.Write(Channel(), [])).Root!;

        Assert.Equal("2026-03-01T00:00:00Z", (string?)feed.Element(Atom + "updated"));
        Assert.Empty(feed.Elements(Atom + "entry"));
    }

    [Fact]
    public void Atomは宣言にUTF8と書く()
    {
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", AtomWriter.Write(Channel(), []), StringComparison.Ordinal);
    }

    [Fact]
    public void Atomの値はエスケープされ_タグを差し込めない()
    {
        var text = AtomWriter.Write(Channel(), [Entry(title: "<script>alert(1)</script> & \"x\"") with { Content = "</content><evil/>" }]);

        Assert.DoesNotContain("<script>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<evil/>", text, StringComparison.Ordinal);
        var entry = XDocument.Parse(text).Root!.Element(Atom + "entry")!;
        Assert.Equal("<script>alert(1)</script> & \"x\"", (string?)entry.Element(Atom + "title"));
        Assert.Equal("</content><evil/>", (string?)entry.Element(Atom + "content"));
    }

    [Fact]
    public void 制御文字は落とし_出力は壊れない()
    {
        var text = AtomWriter.Write(Channel(), [Entry(title: "a\u0001b\u000bc") with { Content = "x\u0000y" }]);

        var entry = XDocument.Parse(text).Root!.Element(Atom + "entry")!;
        Assert.Equal("abc", (string?)entry.Element(Atom + "title"));
        Assert.Equal("xy", (string?)entry.Element(Atom + "content"));
    }

    [Fact]
    public void 絵文字などのサロゲートペアは残す()
    {
        var entry = XDocument.Parse(AtomWriter.Write(Channel(), [Entry(title: "祝🎉")])).Root!.Element(Atom + "entry")!;

        Assert.Equal("祝🎉", (string?)entry.Element(Atom + "title"));
    }

    [Fact]
    public void 本文と著者が無ければ要素を出さない()
    {
        var entry = XDocument.Parse(AtomWriter.Write(Channel(), [Entry() with { Content = null, Author = " ", Published = null, Url = null }])).Root!.Element(Atom + "entry")!;

        Assert.Null(entry.Element(Atom + "content"));
        Assert.Null(entry.Element(Atom + "author"));
        Assert.Null(entry.Element(Atom + "published"));
        Assert.Null(entry.Element(Atom + "link"));
    }

    // ---- RSS ----

    [Fact]
    public void RSSはchannelとitemを持ち_日時はRFC822のGMT()
    {
        var rss = XDocument.Parse(RssWriter.Write(Channel(), [Entry()], 5)).Root!;
        var channel = rss.Element("channel")!;

        Assert.Equal("2.0", (string?)rss.Attribute("version"));
        Assert.Equal("テスト", (string?)channel.Element("title"));
        Assert.Equal("https://pleasanter.example.com/", (string?)channel.Element("link"));
        Assert.Equal("ja", (string?)channel.Element("language"));
        Assert.Equal("Mon, 02 Mar 2026 00:30:00 GMT", (string?)channel.Element("lastBuildDate"));
        Assert.Equal("5", (string?)channel.Element("ttl"));

        var item = channel.Element("item")!;
        Assert.Equal("お知らせ", (string?)item.Element("title"));
        Assert.Equal("https://pleasanter.example.com/items/1", (string?)item.Element("link"));
        Assert.Equal("urn:atomfeed:abc:1:1", (string?)item.Element("guid"));
        Assert.Equal("false", (string?)item.Element("guid")!.Attribute("isPermaLink"));
        Assert.Equal("Sat, 28 Feb 2026 23:00:00 GMT", (string?)item.Element("pubDate"));
        Assert.Equal("山田", (string?)item.Element(Dc + "creator"));
        Assert.Equal("本文", (string?)item.Element("description"));
    }

    [Fact]
    public void RSSの公開日時が無ければpubDateは更新日時()
    {
        var item = XDocument.Parse(RssWriter.Write(Channel(), [Entry() with { Published = null }])).Root!.Element("channel")!.Element("item")!;

        Assert.Equal("Mon, 02 Mar 2026 00:30:00 GMT", (string?)item.Element("pubDate"));
    }

    [Fact]
    public void RSSにも_self_のリンクが付く()
    {
        var self = XDocument.Parse(RssWriter.Write(Channel(), [])).Root!.Element("channel")!.Element(Atom + "link")!;

        Assert.Equal("self", (string?)self.Attribute("rel"));
        Assert.Equal("https://feed.example.com/feeds/t.atom", (string?)self.Attribute("href"));
    }

    [Fact]
    public void RSSの値もエスケープされ_制御文字は落ちる()
    {
        var text = RssWriter.Write(Channel(), [Entry(title: "<b>x</b>\u0002") with { Content = "&amp;" }]);

        var item = XDocument.Parse(text).Root!.Element("channel")!.Element("item")!;
        Assert.Equal("<b>x</b>", (string?)item.Element("title"));
        Assert.Equal("&amp;", (string?)item.Element("description"));
    }
}
