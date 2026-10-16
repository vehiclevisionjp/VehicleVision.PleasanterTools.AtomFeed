using System.Security.Cryptography;
using System.Text;
using VehicleVision.PleasanterTools.AtomFeed.Feeds;
using VehicleVision.PleasanterTools.AtomFeed.Pleasanter;
using VehicleVision.PleasanterTools.AtomFeed.Syndication;
using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Endpoints;

/// <summary>購読フィード（<c>/feeds/{token}.atom</c> / <c>/feeds/{token}.rss</c>）。同じフィードを 2 つの形式で出す。</summary>
public static class FeedEndpoints
{
    /// <summary>購読フィードの口を登録する。</summary>
    public static void MapFeeds(this WebApplication app)
    {
        app.MapGet("/feeds/{token}.atom", (
            string token, HttpContext http, IFeedStore feeds, FeedService service, RuntimeSettings settings,
            ILogger<Program> logger, CancellationToken ct) =>
            ServeAsync(FeedFormat.Atom, token, http, feeds, service, settings, logger, ct)).RequireRateLimiting("feed");

        app.MapGet("/feeds/{token}.rss", (
            string token, HttpContext http, IFeedStore feeds, FeedService service, RuntimeSettings settings,
            ILogger<Program> logger, CancellationToken ct) =>
            ServeAsync(FeedFormat.Rss, token, http, feeds, service, settings, logger, ct)).RequireRateLimiting("feed");
    }

    /// <summary>購読 URL（トークンつき）。<paramref name="settings"/> の公開 URL があればそれを基点にする。</summary>
    public static string FeedUrl(HttpContext http, RuntimeSettings settings, string token, FeedFormat format)
    {
        // **リバースプロキシの内側では Host が内部の値になる。公開 URL は設定で固定する**
        var baseUrl = settings.Current.PublicBaseUrl?.TrimEnd('/')
            ?? $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}";
        return $"{baseUrl}/feeds/{token}.{(format == FeedFormat.Rss ? "rss" : "atom")}";
    }

    private static async Task<IResult> ServeAsync(
        FeedFormat format,
        string token,
        HttpContext http,
        IFeedStore feeds,
        FeedService service,
        RuntimeSettings settings,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // **存在しない・失効済み・形が違うものは、どれも同じ 404 にする**（有効なトークンを推測させない）
        var feed = await feeds.FindByTokenAsync(token, cancellationToken);
        if (feed is null)
        {
            return Results.NotFound();
        }

        IReadOnlyList<FeedEntry> entries;
        try
        {
            entries = await service.GetEntriesAsync(feed, cancellationToken);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            // 秘密の鍵が未設定、または保管時と違う鍵（失った・変えた）。**API キーを登録し直す必要がある**
            logger.LogError("フィード {FeedId} の API キーを復号できません: {Message}", feed.Id, ex.Message);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
        catch (PleasanterException ex)
        {
            // **トークンをログへ出さない。** フィード ID だけで追える
            logger.LogError("フィード {FeedId} の取得に失敗しました（{Kind}）: {Message}", feed.Id, ex.Kind, ex.Message);
            if (ex.Kind is PleasanterErrorKind.Transient or PleasanterErrorKind.RateLimited)
            {
                http.Response.Headers.RetryAfter = service.RetryAfterSeconds(feed, ex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        var body = service.Render(feed, entries, format, FeedUrl(http, settings, token, format));
        var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)))[..32]}\"";
        http.Response.Headers.CacheControl = $"private, max-age={settings.Current.Feed.CacheSeconds}";
        http.Response.Headers.ETag = etag;
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (http.Request.Headers.IfNoneMatch == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Text(body, $"{(format == FeedFormat.Rss ? RssWriter.ContentType : AtomWriter.ContentType)}; charset=utf-8");
    }
}
