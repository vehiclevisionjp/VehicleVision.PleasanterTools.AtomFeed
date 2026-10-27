using System.Globalization;
using System.Text.Json;
using VehicleVision.PleasanterTools.AtomFeed.Localization;

namespace VehicleVision.PleasanterTools.AtomFeed.Feeds;

/// <summary>
/// フィードに出す記事の範囲。<b>更新が今日から何日前までの記事を、最大何件出すか。</b>
/// どちらかを <c>null</c>（無制限）にすると、その側は絞らない。
/// </summary>
/// <remarks>
/// <para>
/// <b>日の単位。</b>過去 <c>N</c> 日は「今日の 0 時の N 日前から」。今日は、<b>API キー利用者のタイムゾーン</b>の今日
/// （記事の時刻と同じ壁時計）。0 日なら、今日の 0 時から。
/// </para>
/// <para>
/// <b>Pleasanter への絞り込みは、取り過ぎない範囲の粗いもの</b>（更新日時の列が日付の列のときだけ。実機で確かめた書式
/// <c>["from,to"]</c>。どちらかを空にすると開区間）。<b>正確な判定は取得した後で本アプリが行う</b>（<see cref="Includes"/>）。
/// 更新日時が文字の列に入っているときは、Pleasanter では絞れないので、取得した後の判定だけが効く。
/// </para>
/// <para>
/// <b>件数は、更新の新しい順に数える。</b>アプリ全体の上限（設定 <c>ATOMFEED_FEED_MAXENTRIES</c>）も効き、小さい方が使われる。
/// </para>
/// </remarks>
/// <param name="PastDays">更新が今日から何日前までか。<c>null</c> なら無制限。</param>
/// <param name="MaxItems">最大何件か。<c>null</c> ならアプリ全体の上限だけ。</param>
public sealed record FeedRange(int? PastDays, int? MaxItems)
{
    /// <summary>新しいフィードの既定の過去（90 日）。</summary>
    public const int DefaultPastDays = 90;

    /// <summary>新しいフィードの既定の件数（100 件）。</summary>
    public const int DefaultMaxItems = 100;

    /// <summary>指定できる最大の日数（100 年）。</summary>
    public const int MaxDays = 36500;

    /// <summary>指定できる最大の件数。</summary>
    public const int MaxItemsLimit = 100000;

    /// <summary>新しいフィードの既定: 過去 90 日・最大 100 件。</summary>
    public static FeedRange Default { get; } = new(DefaultPastDays, DefaultMaxItems);

    /// <summary>両側とも無制限（絞り込まない）。</summary>
    public static FeedRange Unbounded { get; } = new(null, null);

    private const string FilterFormat = "yyyy/MM/dd HH:mm:ss";

    /// <summary>値の範囲を検証する。問題があれば理由を返す。</summary>
    public string? Validate(string? language = null) =>
        PastDays is < 0 or > MaxDays || MaxItems is < 1 or > MaxItemsLimit
            ? ServerMessages.Get(ServerMessageKeys.FeedRangeInvalid, language, MaxDays, MaxItemsLimit)
            : null;

    /// <summary>期間の始まり（今日が <paramref name="today"/>）。無制限なら <c>null</c>。</summary>
    public DateTime? Since(DateTime today) => PastDays is { } past ? today.Date.AddDays(-past) : null;

    /// <summary>記事を出すか。<paramref name="updated"/> は、記事の更新日時の壁時計（API キー利用者のタイムゾーン）。</summary>
    public bool Includes(DateTime today, DateTime updated) => Since(today) is not { } from || updated >= from;

    /// <summary>
    /// Pleasanter への絞り込み（<c>View.ColumnFilterHash</c>）に、期間の粗い絞り込みを足して返す。
    /// <b>利用者が同じ列へ書いた絞り込みは、上書きしない</b>（正確な判定は取得した後で行う）。
    /// </summary>
    public IReadOnlyDictionary<string, string> ApplyTo(
        IReadOnlyDictionary<string, string> filters, FeedMapping mapping, DateTime today)
    {
        if (Since(today) is not { } from
            || !FeedColumns.IsDate(mapping.UpdatedColumn)
            || filters.ContainsKey(mapping.UpdatedColumn))
        {
            return filters;
        }

        var merged = new Dictionary<string, string>(filters, StringComparer.Ordinal)
        {
            [mapping.UpdatedColumn] = JsonSerializer.Serialize(new[]
            {
                $"{from.ToString(FilterFormat, CultureInfo.InvariantCulture)},",
            }),
        };
        return merged;
    }
}
