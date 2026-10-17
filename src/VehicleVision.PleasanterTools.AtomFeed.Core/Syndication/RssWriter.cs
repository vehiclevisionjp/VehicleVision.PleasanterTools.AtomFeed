using System.Globalization;
using System.Text;
using System.Xml;

namespace VehicleVision.PleasanterTools.AtomFeed.Syndication;

/// <summary>記事を RSS 2.0 の本文にする。</summary>
/// <remarks>
/// <para>
/// **本文（<c>description</c>）はプレーンテキスト。** XML の値は <see cref="XmlWriter"/> が必ずエスケープする。
/// </para>
/// <para>
/// 著者は、RSS 2.0 の <c>author</c> がメールアドレスを求めるため、Dublin Core の <c>dc:creator</c> で出す。
/// 記事の <c>guid</c> は URL ではない（<c>isPermaLink="false"</c>）。
/// </para>
/// </remarks>
public static class RssWriter
{
    private const string AtomNs = "http://www.w3.org/2005/Atom";
    private const string DcNs = "http://purl.org/dc/elements/1.1/";

    /// <summary>MIME タイプ。</summary>
    public const string ContentType = "application/rss+xml";

    /// <summary>フィード全体を書く。</summary>
    /// <param name="channel">フィード全体の情報。</param>
    /// <param name="entries">記事。</param>
    /// <param name="ttlMinutes">購読側が見直す間隔の目安（分）。</param>
    public static string Write(FeedChannel channel, IEnumerable<FeedEntry> entries, int ttlMinutes = 60)
    {
        var list = entries.ToList();
        var sb = new StringBuilder();
        using (var xml = XmlWriter.Create(new Utf8StringWriter(sb), XmlText.Settings()))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("rss");
            xml.WriteAttributeString("version", "2.0");
            xml.WriteAttributeString("xmlns", "atom", null, AtomNs);
            xml.WriteAttributeString("xmlns", "dc", null, DcNs);

            xml.WriteStartElement("channel");
            xml.WriteElementString("title", XmlText.Clean(channel.Title));
            xml.WriteElementString("link", XmlText.Url(channel.SiteUrl ?? channel.SelfUrl ?? "about:blank"));
            xml.WriteElementString("description", XmlText.Clean(channel.Title));
            xml.WriteElementString("language", XmlText.Clean(channel.Language));
            xml.WriteElementString("lastBuildDate", XmlText.Rfc822(list.Count == 0 ? channel.Generated : list.Max(e => e.Updated)));
            xml.WriteElementString("ttl", ttlMinutes.ToString(CultureInfo.InvariantCulture));
            xml.WriteElementString("generator", "VehicleVision.PleasanterTools.AtomFeed");
            if (channel.SelfUrl is not null)
            {
                xml.WriteStartElement("link", AtomNs);
                xml.WriteAttributeString("rel", "self");
                xml.WriteAttributeString("type", ContentType);
                xml.WriteAttributeString("href", XmlText.Url(channel.SelfUrl));
                xml.WriteEndElement();
            }

            foreach (var entry in list)
            {
                WriteItem(xml, entry);
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return sb.ToString();
    }

    private static void WriteItem(XmlWriter xml, FeedEntry e)
    {
        xml.WriteStartElement("item");
        xml.WriteElementString("title", XmlText.Clean(e.Title));
        if (!string.IsNullOrEmpty(e.Url))
        {
            xml.WriteElementString("link", XmlText.Url(e.Url));
        }

        xml.WriteStartElement("guid");
        xml.WriteAttributeString("isPermaLink", "false");
        xml.WriteString(XmlText.Clean(e.Id));
        xml.WriteEndElement();

        // pubDate は公開日時。無ければ更新日時（RSS 2.0 に「更新日時」の要素は無い）
        xml.WriteElementString("pubDate", XmlText.Rfc822(e.Published ?? e.Updated));
        if (!string.IsNullOrWhiteSpace(e.Author))
        {
            xml.WriteElementString("dc", "creator", DcNs, XmlText.Clean(e.Author));
        }

        if (!string.IsNullOrEmpty(e.Content))
        {
            xml.WriteElementString("description", XmlText.Clean(e.Content));
        }

        xml.WriteEndElement();
    }
}
