using System.Text;
using System.Xml;

namespace VehicleVision.PleasanterTools.AtomFeed.Syndication;

/// <summary>記事を Atom（RFC 4287）の本文にする。</summary>
/// <remarks>
/// <para>
/// **本文はプレーンテキスト（<c>type="text"</c>）で出す。** Pleasanter の本文は Markdown で、
/// HTML として出すと購読側のリーダーでスクリプトを差し込めてしまう。XML の値は <see cref="XmlWriter"/> が必ずエスケープする。
/// </para>
/// <para>
/// 日時は UTC（<c>Z</c> 付き）。<c>feed/updated</c> は、記事の最新の更新日時（無ければ出力した時刻）。
/// </para>
/// </remarks>
public static class AtomWriter
{
    private const string Ns = "http://www.w3.org/2005/Atom";

    /// <summary>MIME タイプ。</summary>
    public const string ContentType = "application/atom+xml";

    /// <summary>フィード全体を書く。</summary>
    public static string Write(FeedChannel channel, IEnumerable<FeedEntry> entries)
    {
        var list = entries.ToList();
        var sb = new StringBuilder();
        using (var xml = XmlWriter.Create(new Utf8StringWriter(sb), XmlText.Settings()))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("feed", Ns);
            xml.WriteAttributeString("xml", "lang", null, XmlText.Clean(channel.Language));

            xml.WriteElementString("id", XmlText.Clean(channel.Id));
            Text(xml, "title", channel.Title);
            xml.WriteElementString("updated", XmlText.Rfc3339(list.Count == 0 ? channel.Generated : list.Max(e => e.Updated)));
            if (channel.SelfUrl is not null)
            {
                Link(xml, "self", channel.SelfUrl, ContentType);
            }

            if (channel.SiteUrl is not null)
            {
                Link(xml, "alternate", channel.SiteUrl, null);
            }

            xml.WriteElementString("generator", "VehicleVision.PleasanterTools.AtomFeed");

            foreach (var entry in list)
            {
                WriteEntry(xml, entry);
            }

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return sb.ToString();
    }

    private static void WriteEntry(XmlWriter xml, FeedEntry e)
    {
        xml.WriteStartElement("entry");
        xml.WriteElementString("id", XmlText.Clean(e.Id));
        Text(xml, "title", e.Title);
        xml.WriteElementString("updated", XmlText.Rfc3339(e.Updated));
        if (e.Published is { } published)
        {
            xml.WriteElementString("published", XmlText.Rfc3339(published));
        }

        if (!string.IsNullOrEmpty(e.Url))
        {
            Link(xml, "alternate", e.Url, null);
        }

        if (!string.IsNullOrWhiteSpace(e.Author))
        {
            xml.WriteStartElement("author");
            xml.WriteElementString("name", XmlText.Clean(e.Author));
            xml.WriteEndElement();
        }

        if (!string.IsNullOrEmpty(e.Content))
        {
            Text(xml, "content", e.Content);
        }

        xml.WriteEndElement();
    }

    private static void Text(XmlWriter xml, string name, string value)
    {
        xml.WriteStartElement(name);
        xml.WriteAttributeString("type", "text");
        xml.WriteString(XmlText.Clean(value));
        xml.WriteEndElement();
    }

    private static void Link(XmlWriter xml, string rel, string href, string? type)
    {
        xml.WriteStartElement("link");
        xml.WriteAttributeString("rel", rel);
        if (type is not null)
        {
            xml.WriteAttributeString("type", type);
        }

        xml.WriteAttributeString("href", XmlText.Url(href));
        xml.WriteEndElement();
    }
}
