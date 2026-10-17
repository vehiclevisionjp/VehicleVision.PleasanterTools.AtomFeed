using VehicleVision.PleasanterTools.AtomFeed.Localization;
using System.Globalization;

namespace VehicleVision.PleasanterTools.AtomFeed.Feeds;

/// <summary>Pleasanter のレコードを Atom / RSS の記事へ写す列の対応。</summary>
/// <remarks>
/// <para>
/// 列名は Pleasanter の列の内部名（<c>Title</c> / <c>UpdatedTime</c> / <c>ClassA</c> / <c>DateA</c> など）。
/// 型付きの列（<c>ClassX</c> / <c>DateX</c> …）はレコードの <c>ClassHash</c> / <c>DateHash</c> …から読む。
/// </para>
/// <para>
/// **日付と数値の列を文字にするときは、.NET のカスタム書式文字列を必ず与える**（<see cref="Formats"/> か、
/// テンプレート中の <c>{DateA:yyyy/MM/dd}</c>）。値は既定の応答の生の値を本アプリが整形する
/// （Pleasanter の表示用の文字は、日付だけ・ロケール依存で使えない。実機検証結果 5 章）。
/// </para>
/// </remarks>
public sealed class FeedMapping
{
    /// <summary>タイトルにする列。既定は Pleasanter がサイト設定のタイトルで組み上げた <c>ItemTitle</c>。<see cref="TitleTemplate"/> があればそちらが優先。</summary>
    public string TitleColumn { get; init; } = FeedColumns.ItemTitle;

    /// <summary>
    /// タイトルを列の値の組み合わせで作るテンプレート。<c>null</c> なら <see cref="TitleColumn"/>。
    /// 例: <c>{ClassA}：{Title}（{DateA:yyyy/MM/dd}）</c>（<see cref="TitleTemplate"/>）。
    /// </summary>
    public string? TitleTemplate { get; init; }

    /// <summary>
    /// 更新日時にする列（購読側が「更新された」と判断する基準で、記事の並びの基準）。
    /// 日付の列（<c>UpdatedTime</c> / <c>CreatedTime</c> / <c>StartTime</c> / <c>DateA</c>…）は書式が要らない。
    /// **日付が文字として入っている列（<c>Class*</c> / <c>Description*</c> / <c>Title</c> / <c>Body</c>）は <see cref="UpdatedFormat"/> が必須。**
    /// </summary>
    public string UpdatedColumn { get; init; } = "UpdatedTime";

    /// <summary>
    /// 更新日時が文字の列のときの、日付の書式（.NET のカスタム書式文字列。<c>DateTime.ParseExact</c>。例 <c>yyyy/MM/dd HH:mm</c>）。
    /// 日付の列には指定しない。
    /// </summary>
    public string? UpdatedFormat { get; init; }

    /// <summary>公開日時にする列。<c>null</c> なら公開日時を出さない。文字の列なら <see cref="PublishedFormat"/> が必須。</summary>
    public string? PublishedColumn { get; init; } = "CreatedTime";

    /// <summary>公開日時が文字の列のときの、日付の書式（<see cref="UpdatedFormat"/> と同じ）。</summary>
    public string? PublishedFormat { get; init; }

    /// <summary>本文にする列。プレーンテキストとして出す。</summary>
    public string? ContentColumn { get; init; } = "Body";

    /// <summary>著者の名前にする列（<c>Updator</c> / <c>Creator</c> / <c>Owner</c> など）。<c>null</c> なら出さない。</summary>
    public string? AuthorColumn { get; init; }

    /// <summary>
    /// 日付・数値の列を文字にするときの書式（列名 → .NET のカスタム書式文字列）。
    /// タイトル・本文・著者に日付・数値の列を指定したら必須。テンプレート中の <c>{列名:書式}</c> が優先。
    /// </summary>
    public IReadOnlyDictionary<string, string>? Formats { get; init; }

    /// <summary>
    /// **Pleasanter に表示名へ解決させる必要がある列**（選択肢・リンク・ユーザー・状況）。
    /// 空なら 2 回目の取得は要らない。
    /// </summary>
    public IReadOnlyList<string> ResolvedColumns()
    {
        var columns = new List<string>();
        foreach (var column in TextColumns())
        {
            if (FeedColumns.NeedsResolution(column) && !columns.Contains(column))
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    /// <summary>タイトル・本文・著者が読む列（テンプレートの列を含む）。解析できなければテンプレートの分は含めない。</summary>
    private IEnumerable<string> TextColumns()
    {
        if (TitleTemplate is not null)
        {
            IReadOnlyList<TextTemplate.Part>? parts = null;
            try
            {
                parts = TextTemplate.Parse(TitleTemplate);
            }
            catch (FormatException)
            {
                // 不正なテンプレートは Validate が弾く
            }

            foreach (var part in parts ?? [])
            {
                if (part.Column is not null)
                {
                    yield return part.Column;
                }
            }
        }
        else
        {
            yield return TitleColumn;
        }

        if (ContentColumn is not null)
        {
            yield return ContentColumn;
        }

        if (AuthorColumn is not null)
        {
            yield return AuthorColumn;
        }
    }

    /// <summary>入力を検証する。問題があれば理由を、無ければ <c>null</c> を返す。</summary>
    public string? Validate(string? language = null)
    {
        if (TitleTemplate is null && !FeedColumns.IsText(TitleColumn))
        {
            return ServerMessages.Get(ServerMessageKeys.MappingTitleColumnInvalid, language, TitleColumn);
        }

        if (ValidateDateColumn(nameof(UpdatedColumn), UpdatedColumn, UpdatedFormat, language) is { } startError)
        {
            return startError;
        }

        if (PublishedColumn is null)
        {
            if (PublishedFormat is not null)
            {
                return ServerMessages.Get(ServerMessageKeys.MappingPublishedFormatWithoutColumn, language);
            }
        }
        else if (ValidateDateColumn(nameof(PublishedColumn), PublishedColumn, PublishedFormat, language) is { } endError)
        {
            return endError;
        }

        foreach (var (name, column) in new[]
        {
            (nameof(ContentColumn), ContentColumn),
            (nameof(AuthorColumn), AuthorColumn),
        })
        {
            if (column is not null && !FeedColumns.IsText(column))
            {
                return ServerMessages.Get(ServerMessageKeys.MappingColumnInvalid, language, name, column);
            }
        }

        return ValidateFormats(language);
    }

    /// <summary>更新・公開の列と書式の組み合わせを検証する。</summary>
    private static string? ValidateDateColumn(string name, string column, string? format, string? language)
    {
        if (FeedColumns.IsDate(column))
        {
            return format is null
                ? null
                : ServerMessages.Get(ServerMessageKeys.MappingDateColumnNoFormat, language, name, column);
        }

        if (!FeedColumns.IsParsableText(column))
        {
            return ServerMessages.Get(ServerMessageKeys.MappingDateColumnKind, language, name, column);
        }

        if (format is null)
        {
            return ServerMessages.Get(ServerMessageKeys.MappingTextColumnNeedsFormat, language, name, name.Replace("Column", "Format", StringComparison.Ordinal), column);
        }

        return TryParseFormat(format, language) is { } error ? $"{name.Replace("Column", "Format", StringComparison.Ordinal)}: {error}" : null;
    }

    /// <summary>日付を読む書式として使えるか。**整形してから同じ書式で読み戻せること**を確かめる。</summary>
    public static string? TryParseFormat(string format, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(format) || format.Length > 100 || format.Any(char.IsControl))
        {
            return ServerMessages.Get(ServerMessageKeys.MappingFormatLengthInvalid, language);
        }

        try
        {
            // 曜日だけの書式（ddd）は、読み戻すと「実行した日」の曜日と食い違うかどうかで結果が変わる（日付の無い書式は今日の日付で補われる）。
            // 曜日の違う 2 日を両方とも読み戻せることを求め、実行した曜日によって通ったり弾かれたりしないようにする
            var samples = new[]
            {
                new DateTime(2026, 3, 5, 14, 30, 45, DateTimeKind.Unspecified),
                new DateTime(2026, 3, 6, 14, 30, 45, DateTimeKind.Unspecified),
            };
            return samples.All(sample => DateTime.TryParseExact(sample.ToString(format, CultureInfo.InvariantCulture), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                ? null
                : ServerMessages.Get(ServerMessageKeys.MappingDateFormatNotRoundTrip, language, format);
        }
        catch (FormatException)
        {
            return ServerMessages.Get(ServerMessageKeys.MappingDateFormatUnusable, language, format);
        }
    }

    /// <summary>書式の指定を検証する（必須の有無・書式として使えるか）。</summary>
    private string? ValidateFormats(string? language)
    {
        if (Formats is { } formats)
        {
            if (formats.Count > 50)
            {
                return ServerMessages.Get(ServerMessageKeys.MappingFormatsTooMany, language);
            }

            foreach (var (column, format) in formats)
            {
                if (!FeedColumns.NeedsFormat(column))
                {
                    return ServerMessages.Get(ServerMessageKeys.MappingFormatsOnlyDateNumber, language, column);
                }

                if (TryFormat(column, format, language) is { } error)
                {
                    return $"Formats[{column}]: {error}";
                }
            }
        }

        // テンプレート
        if (TitleTemplate is not null)
        {
            IReadOnlyList<TextTemplate.Part> parts;
            try
            {
                parts = TextTemplate.Parse(TitleTemplate);
            }
            catch (TemplateFormatException ex)
            {
                return $"TitleTemplate: {ServerMessages.Get(ex.MessageKey, language, ex.Args)}";
            }

            if (parts.All(p => p.Column is null))
            {
                return ServerMessages.Get(ServerMessageKeys.MappingTitleTemplateNoColumns, language);
            }

            foreach (var part in parts.Where(p => p.Column is not null))
            {
                if (!FeedColumns.IsText(part.Column!))
                {
                    return ServerMessages.Get(ServerMessageKeys.MappingTitleTemplateColumnInvalid, language, part.Column!);
                }

                if (!FeedColumns.NeedsFormat(part.Column!))
                {
                    continue;
                }

                var format = part.Format ?? Formats?.GetValueOrDefault(part.Column!);
                if (format is null)
                {
                    return ServerMessages.Get(ServerMessageKeys.MappingTitleTemplateNeedsFormat, language, part.Column!);
                }

                if (TryFormat(part.Column!, format, language) is { } error)
                {
                    return ServerMessages.Get(ServerMessageKeys.MappingTitleTemplateFormatError, language, part.Column!, format, error);
                }
            }
        }
        else if (FeedColumns.NeedsFormat(TitleColumn) && Formats?.ContainsKey(TitleColumn) != true)
        {
            return ServerMessages.Get(ServerMessageKeys.MappingTitleColumnNeedsFormat, language, TitleColumn);
        }

        foreach (var (name, column) in new[]
        {
            (nameof(ContentColumn), ContentColumn),
            (nameof(AuthorColumn), AuthorColumn),
        })
        {
            if (column is not null && FeedColumns.NeedsFormat(column) && Formats?.ContainsKey(column) != true)
            {
                return ServerMessages.Get(ServerMessageKeys.MappingColumnNeedsFormat, language, name, column);
            }
        }

        return null;
    }

    /// <summary>書式として使えるか、実際に整形して確かめる。</summary>
    /// <remarks>
    /// 日付と数値で使える書式が違う。**カルチャはインバリアント**（書式文字列の解釈を環境に依存させない）。
    /// </remarks>
    public static string? TryFormat(string column, string format, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(format) || format.Length > 100 || format.Any(char.IsControl))
        {
            return ServerMessages.Get(ServerMessageKeys.MappingFormatLengthInvalid, language);
        }

        try
        {
            _ = FeedColumns.IsNumber(column)
                ? 1234567.891m.ToString(format, CultureInfo.InvariantCulture)
                : new DateTime(2026, 3, 1, 9, 5, 7, DateTimeKind.Unspecified).ToString(format, CultureInfo.InvariantCulture);
            return null;
        }
        catch (FormatException)
        {
            return ServerMessages.Get(FeedColumns.IsNumber(column) ? ServerMessageKeys.MappingNumberFormatUnusable : ServerMessageKeys.MappingDateFormatUnusable, language, format);
        }
    }
}
