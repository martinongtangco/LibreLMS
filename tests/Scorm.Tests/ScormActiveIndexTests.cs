using LibreLms.Modules.Scorm.Infrastructure;
using StackExchange.Redis;

namespace Scorm.Tests;

/// <summary>
/// Spec 048 (E8) — lifecycle test for the SCORM secondary index
/// <c>scorm:active:{studentId}:{courseId} → sessionId</c> that replaced the
/// blocking full-keyspace KEYS scan in <c>FindActiveSessionKeyAsync</c>.
///
/// Real Valkey, same connection convention as the spec 046 tests
/// (ConnectionStrings__Valkey; host runs use localhost:6380). Random marker
/// GUIDs per run so re-runs never interfere with each other; DisposeAsync
/// sweeps every key the tests may have left behind for this run's marker pair.
/// </summary>
public class ScormActiveIndexTests : IAsyncLifetime
{
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private IConnectionMultiplexer _mux = null!;
    private IDatabase _db = null!;
    private readonly List<Guid> _sessionIds = new();

    private string IndexKey => $"scorm:active:{_studentId}:{_courseId}";

    public Task InitializeAsync()
    {
        var valkeyConn = Environment.GetEnvironmentVariable("ConnectionStrings__Valkey") ?? "localhost:6380";
        return ConnectAsync(valkeyConn);
    }

    private async Task ConnectAsync(string valkeyConn)
    {
        _mux = await ConnectionMultiplexer.ConnectAsync(valkeyConn);
        _db = _mux.GetDatabase();
        // Clean slate for this run's marker pair (guards against a crashed earlier run).
        await _db.KeyDeleteAsync(IndexKey);
    }

    public async Task DisposeAsync()
    {
        foreach (var sessionId in _sessionIds)
            await _db.KeyDeleteAsync($"scorm:session:{sessionId}");
        await _db.KeyDeleteAsync(IndexKey);
        _mux.Close();
        _mux.Dispose();
    }

    [Fact]
    public async Task Create_find_activity_delete_index_lifecycle()
    {
        var store = new ScormSessionStore(_mux);
        var sessionId = Guid.NewGuid();
        _sessionIds.Add(sessionId);
        var attemptId = Guid.NewGuid();

        // 1. Create → the index is written and FindActiveSessionKeyAsync is O(1) on it.
        await store.CreateSessionAsync(SessionData.CreateDefault(sessionId, _studentId, _courseId, attemptId));
        Assert.True(await _db.KeyExistsAsync(IndexKey), "index key must exist after create");
        Assert.Equal(sessionId.ToString(), await store.FindActiveSessionKeyAsync(_studentId, _courseId));
        // A different student must not see this session.
        Assert.Null(await store.FindActiveSessionKeyAsync(Guid.NewGuid(), _courseId));

        // 2. Activity (SetValueAsync) → index key still exists AND its TTL is
        //    refreshed back to the 30-minute DefaultTtl. To make the refresh
        //    observable, pin the index TTL to 5 seconds, wait 2 seconds (so it
        //    would expire unrefreshed), then commit: a refreshed index must
        //    report a TTL far above the 5-second floor.
        Assert.True(await _db.KeyExpireAsync(IndexKey, TimeSpan.FromSeconds(5)), "index key must have a settable TTL");
        await Task.Delay(2000);
        Assert.True(await store.SetValueAsync(sessionId, "cmi.core.lesson_status", "passed"), "commit must succeed");
        Assert.True(await _db.KeyExistsAsync(IndexKey), "index key must still exist after activity");
        var indexTtl = _db.KeyTimeToLive(IndexKey);
        Assert.NotNull(indexTtl);
        Assert.True(indexTtl!.Value > TimeSpan.FromMinutes(10),
            $"index TTL must be refreshed toward the 30-minute DefaultTtl, was {indexTtl.Value}");

        // The committed value itself is intact (behavior unchanged by the index work).
        Assert.Equal("passed", await store.GetValueAsync(sessionId, "cmi.core.lesson_status"));

        // 3. Delete → session hash AND index key are gone; find returns null.
        await store.DeleteSessionAsync(sessionId);
        Assert.False(await _db.KeyExistsAsync($"scorm:session:{sessionId}"), "session hash must be deleted");
        Assert.False(await _db.KeyExistsAsync(IndexKey), "index key must be deleted with the session");
        Assert.Null(await store.FindActiveSessionKeyAsync(_studentId, _courseId));
    }

    [Fact]
    public async Task Stale_index_self_cleans_and_returns_null()
    {
        var store = new ScormSessionStore(_mux);
        var sessionId = Guid.NewGuid();
        _sessionIds.Add(sessionId);

        await store.CreateSessionAsync(SessionData.CreateDefault(sessionId, _studentId, _courseId, Guid.NewGuid()));
        Assert.Equal(sessionId.ToString(), await store.FindActiveSessionKeyAsync(_studentId, _courseId));

        // Simulate the session hash expiring (or being deleted) while the index
        // entry's TTL is still live — the stale-index case the old scan tolerated.
        await _db.KeyDeleteAsync($"scorm:session:{sessionId}");

        Assert.Null(await store.FindActiveSessionKeyAsync(_studentId, _courseId));
        Assert.False(await _db.KeyExistsAsync(IndexKey), "stale index key must be self-cleaned");
    }
}
