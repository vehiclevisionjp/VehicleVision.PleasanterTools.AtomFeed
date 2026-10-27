using System.Globalization;
using System.Text;
using System.Xml;

namespace VehicleVision.PleasanterTools.AtomFeed.Syndication;

/// <summary>Atom・RSS の出力で共通に使う XML の補助。</summary>
internal static class XmlText
{
    /// <summary>
    /// XML 1.0 で許されない文字（制御文字など）を落とす。**Pleasanter の本文に紛れた制御文字で、
    /// フィード全体の出力が失敗しない**ようにする（<see cref="XmlWriter"/> は不正な文字で例外を投げる）。
    /// </summary>
    public static string Clean(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                sb.Append(c).Append(value[i + 1]);
                i++;
            }
            else if (XmlConvert.IsXmlChar(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>URL の制御文字を落とす（要素を壊さない）。</summary>
    public static string Url(string value) => Clean(string.Concat(value.Where(c => !char.IsControl(c))).Trim());

    /// <summary>RFC 3339 の日時（UTC・<c>Z</c> 付き）。</summary>
    public static string Rfc3339(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>RFC 822（RSS 2.0）の日時。UTC（<c>GMT</c>）で出す。</summary>
    public static string Rfc822(DateTimeOffset value) =>
        value.UtcDateTime.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);

    /// <summary>XML の書き出しの設定。宣言は UTF-8。</summary>
    public static XmlWriterSettings Settings() => new()
    {
        Indent = true,
        IndentChars = "  ",
        Encoding = new UTF8Encoding(false),
        CheckCharacters = true,
    };
}

/// <summary><see cref="StringBuilder"/> へ書くが、XML 宣言には UTF-8 と書く（<see cref="StringWriter"/> は既定で utf-16 と書く）。</summary>
internal sealed class Utf8StringWriter(StringBuilder sb) : StringWriter(sb, CultureInfo.InvariantCulture)
{
    public override Encoding Encoding { get; } = new UTF8Encoding(false);
}
