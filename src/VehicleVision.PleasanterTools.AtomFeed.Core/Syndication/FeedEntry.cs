namespace VehicleVision.PleasanterTools.AtomFeed.Syndication;

/// <summary>フィードの記事（Atom の <c>entry</c> / RSS の <c>item</c>）1 件。</summary>
public sealed record FeedEntry
{
    /// <summary>記事の ID。**レコードごとに変わらない値**にする（購読側が更新と新規を見分ける）。</summary>
    public required string Id { get; init; }

    /// <summary>元の Pleasanter のレコード ID。</summary>
    public long? RecordId { get; init; }

    /// <summary>タイトル。</summary>
    public required string Title { get; init; }

    /// <summary>更新日時。購読側が「更新された」と判断する基準で、並びの基準でもある。</summary>
    public required DateTimeOffset Updated { get; init; }

    /// <summary>公開（作成）日時。</summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>本文（プレーンテキスト）。</summary>
    public string? Content { get; init; }

    /// <summary>著者の名前。</summary>
    public string? Author { get; init; }

    /// <summary>元のレコードへのリンク。</summary>
    public string? Url { get; init; }
}

/// <summary>フィード全体の情報。</summary>
/// <param name="Id">フィードの ID（Atom の <c>id</c>。IRI）。変わらない値。</param>
/// <param name="Title">フィードのタイトル。</param>
/// <param name="Language">フィードの言語（BCP 47。<c>ja</c> など）。</param>
/// <param name="SelfUrl">このフィード自身の URL。<c>null</c> なら出さない。</param>
/// <param name="SiteUrl">元のサイト（Pleasanter）の URL。<c>null</c> なら出さない。</param>
/// <param name="Generated">出力した時刻。記事が 1 件も無いときの <c>updated</c> に使う。</param>
public sealed record FeedChannel(
    string Id,
    string Title,
    string Language,
    string? SelfUrl,
    string? SiteUrl,
    DateTimeOffset Generated);
