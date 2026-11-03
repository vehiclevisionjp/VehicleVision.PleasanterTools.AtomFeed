using VehicleVision.PleasanterTools.AtomFeed.Web.Services;

namespace VehicleVision.PleasanterTools.AtomFeed.Web.Tests;

/// <summary>
/// 共有のレート制限の古い数えの掃除（<see cref="RateLimitPurgeService"/>）が、数えの更新と競合しないこと（Issue #93）。
/// </summary>
/// <remarks>
/// 掃除が消すのは「窓が 1 日より前」の行だけで、数えが触るのは今の窓の行だけなので、行が重ならない。
/// 万一重なっても、数えは 1 文の UPDATE（行が無ければ INSERT）なので、数えこぼしは「消された行が 1 から数え直し」になるだけ。
/// </remarks>
public sealed class RateLimitPurgeTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private static string NewKey() => $"test:{Guid.NewGuid():N}";

    /// <summary>掃除が残す境目（これより小さい窓の行を消す）。</summary>
    private long Cutoff => (_app.Time.GetUtcNow() - TimeSpan.FromDays(1)).ToUnixTimeSeconds();

    private long CurrentWindow => _app.Time.GetUtcNow().ToUnixTimeSeconds() / 300 * 300;

    [Fact]
    public async Task 掃除は_1日より前の窓の行だけを消す()
    {
        var store = _app.Service<IRateLimitStore>();
        var purge = _app.Service<RateLimitPurgeService>();
        var (stale, edge, current) = (NewKey(), NewKey(), NewKey());
        await store.HitAsync(stale, Cutoff - 1);
        await store.HitAsync(edge, Cutoff);
        await store.HitAsync(current, CurrentWindow);

        var purged = await purge.SweepAsync(CancellationToken.None);

        Assert.True(purged >= 1);
        // 消された行は 1 から数え直し。境目ちょうど・今の窓の行は残っていて続きの数になる
        Assert.Equal(1, await store.HitAsync(stale, Cutoff - 1));
        Assert.Equal(2, await store.HitAsync(edge, Cutoff));
        Assert.Equal(2, await store.HitAsync(current, CurrentWindow));
    }

    [Fact]
    public async Task 掃除と数えが同時に走っても_今の窓の数えをこぼさない()
    {
        var store = _app.Service<IRateLimitStore>();
        var purge = _app.Service<RateLimitPurgeService>();
        var key = NewKey();
        const int hits = 20;

        // 古い行を足し続けて、掃除が毎回何かを消している状態にする
        var work = Enumerable.Range(0, hits).Select(i => Task.Run(() => store.HitAsync(key, CurrentWindow))).ToList();
        var sweeps = Enumerable.Range(0, 5).Select(async i =>
        {
            await store.HitAsync(NewKey(), Cutoff - 1 - i);
            return await purge.SweepAsync(CancellationToken.None);
        }).ToList();

        var counts = await Task.WhenAll(work);
        await Task.WhenAll(sweeps);

        // 全員が別の数を受け取り、1 から 20 までそろう
        Assert.Equal(Enumerable.Range(1, hits), counts.Order());
        Assert.Equal(hits + 1, await store.HitAsync(key, CurrentWindow));
    }

    [Fact]
    public async Task 掃除を同時に重ねても_失敗しない_何度でも同じ結果になる()
    {
        var store = _app.Service<IRateLimitStore>();
        var purge = _app.Service<RateLimitPurgeService>();
        await store.HitAsync(NewKey(), Cutoff - 10);

        // 複数台がそれぞれ掃除する状況（同じ DELETE が重なる）
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => purge.SweepAsync(CancellationToken.None))));

        Assert.All(results, r => Assert.True(r >= 0));
        Assert.Equal(0, await purge.SweepAsync(CancellationToken.None));
    }
}
