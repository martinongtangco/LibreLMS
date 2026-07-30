using System.Text.Json;
using StackExchange.Redis;

namespace LibreLms.Modules.Scorm.Infrastructure;

/// <summary>
/// Valkey-backed session state for live SCORM sessions.
/// Uses StackExchange.Redis against Valkey (Redis-protocol-compatible).
/// Key pattern: scorm:session:{sessionId}
/// TTL: 30 minutes
/// </summary>
public class ScormSessionStore : IScormSessionStore
{
    private readonly IDatabase _redis;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    public ScormSessionStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _redis = connectionMultiplexer.GetDatabase();
    }

    /// <summary>Create a new session with default CMI values.</summary>
    public async Task CreateSessionAsync(SessionData data)
    {
        var hashEntries = data.ToHashEntries();
        var key = SessionKey(data.SessionId.ToString());
        await _redis.HashSetAsync(key, hashEntries);
        await _redis.KeyExpireAsync(key, DefaultTtl);
    }

    /// <summary>Set a CMI value in an existing session. Returns false if session not found.</summary>
    public async Task<bool> SetValueAsync(Guid sessionId, string element, string value)
    {
        var key = SessionKey(sessionId);
        var exists = await _redis.KeyExistsAsync(key);
        if (!exists)
            return false;

        await _redis.HashSetAsync(key, element, value);
        // Reset TTL on activity
        await _redis.KeyExpireAsync(key, DefaultTtl);
        return true;
    }

    /// <summary>Get a CMI value from a session. Returns null if session or element not found.</summary>
    public async Task<string?> GetValueAsync(Guid sessionId, string element)
    {
        var key = SessionKey(sessionId);
        var exists = await _redis.KeyExistsAsync(key);
        if (!exists)
            return null;

        var value = await _redis.HashGetAsync(key, element);
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>Read the full CMI bag as a SessionData record. Returns null if session not found.</summary>
    public async Task<SessionData?> ReadSessionAsync(Guid sessionId)
    {
        var key = SessionKey(sessionId);
        var exists = await _redis.KeyExistsAsync(key);
        if (!exists)
            return null;

        var entries = await _redis.HashGetAllAsync(key);
        return SessionData.FromHashEntries(entries);
    }

    /// <summary>Delete a session (cleanup on LMSFinish).</summary>
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        var key = SessionKey(sessionId);
        await _redis.KeyDeleteAsync(key);
    }

    /// <summary>Check if a session exists (for concurrent session detection).</summary>
    public async Task<bool> SessionExistsAsync(Guid sessionId)
    {
        var key = SessionKey(sessionId);
        return await _redis.KeyExistsAsync(key);
    }

    /// <summary>Find active session key for a student/course pair. Returns null if none found.</summary>
    public async Task<string?> FindActiveSessionKeyAsync(Guid studentId, Guid courseId)
    {
        // Scan for sessions belonging to this student/course.
        // In production with high concurrency, use a secondary index key instead.
        var endPoints = _redis.Multiplexer.GetEndPoints();
        foreach (var endPoint in endPoints)
        {
            var server = _redis.Multiplexer.GetServer(endPoint);
            if (!server.IsConnected)
                continue;

            foreach (var key in server.Keys(pattern: "scorm:session:*"))
            {
                var rawKey = key.ToString();
                // Extract sessionId from key pattern "scorm:session:{guid}"
                var sessionIdStr = rawKey["scorm:session:".Length..];
                if (Guid.TryParse(sessionIdStr, out var sid))
                {
                    var sessionData = await ReadSessionAsync(sid);
                    if (sessionData is not null
                        && Guid.TryParse(sessionData.StudentId, out var sId)
                        && sId == studentId
                        && Guid.TryParse(sessionData.CourseId, out var cId)
                        && cId == courseId)
                    {
                        return sessionIdStr;
                    }
                }
            }
        }
        return null;
    }

    private static RedisKey SessionKey(Guid sessionId) => $"scorm:session:{sessionId}";
    private static RedisKey SessionKey(string sessionId) => $"scorm:session:{sessionId}";
}

/// <summary>Interface for session store — allows mocking in tests.</summary>
public interface IScormSessionStore
{
    Task CreateSessionAsync(SessionData data);
    Task<bool> SetValueAsync(Guid sessionId, string element, string value);
    Task<string?> GetValueAsync(Guid sessionId, string element);
    Task<SessionData?> ReadSessionAsync(Guid sessionId);
    Task DeleteSessionAsync(Guid sessionId);
    Task<bool> SessionExistsAsync(Guid sessionId);
    Task<string?> FindActiveSessionKeyAsync(Guid studentId, Guid courseId);
}

/// <strong>Immutable record representing a SCORM session's CMI bag.</strong>
public record SessionData(
    string SessionId,
    string StudentId,
    string CourseId,
    string AttemptId,
    string CmiStudentId,
    string CmiStudentName,
    string CmiLessonStatus,
    string CmiCredit,
    string CmiEntry,
    string CmiExit,
    string CmiScoreRaw,
    string CmiSessionTime,
    string CmiSuspendData,
    string StartedAt,
    string ErrorCode)
{
    public static SessionData CreateDefault(Guid sessionId, Guid studentId, Guid courseId, Guid attemptId, string entryMode = "initial")
    {
        return new SessionData(
            SessionId: sessionId.ToString(),
            StudentId: studentId.ToString(),
            CourseId: courseId.ToString(),
            AttemptId: attemptId.ToString(),
            CmiStudentId: studentId.ToString(),
            CmiStudentName: "",
            CmiLessonStatus: "not attempted",
            CmiCredit: "credit",
            CmiEntry: entryMode,
            CmiExit: "",
            CmiScoreRaw: "0",
            CmiSessionTime: "00:00:00",
            CmiSuspendData: "",
            StartedAt: DateTimeOffset.UtcNow.ToString("O"),
            ErrorCode: "0");
    }

    public HashEntry[] ToHashEntries()
    {
        return
        [
            new HashEntry("sessionId", SessionId),
            new HashEntry("studentId", StudentId),
            new HashEntry("courseId", CourseId),
            new HashEntry("attemptId", AttemptId),
            new HashEntry("cmi.core.student_id", CmiStudentId),
            new HashEntry("cmi.core.student_name", CmiStudentName),
            new HashEntry("cmi.core.lesson_status", CmiLessonStatus),
            new HashEntry("cmi.core.credit", CmiCredit),
            new HashEntry("cmi.core.entry", CmiEntry),
            new HashEntry("cmi.core.exit", CmiExit),
            new HashEntry("cmi.core.score.raw", CmiScoreRaw),
            new HashEntry("cmi.core.session_time", CmiSessionTime),
            new HashEntry("cmi.suspend_data", CmiSuspendData),
            new HashEntry("startedAt", StartedAt),
            new HashEntry("error_code", ErrorCode),
        ];
    }

    public static SessionData FromHashEntries(HashEntry[] entries)
    {
        var dict = new Dictionary<string, string>();
        foreach (var entry in entries)
        {
            dict[entry.Name.ToString()] = entry.Value.ToString();
        }

        return new SessionData(
            SessionId: dict.GetValueOrDefault("sessionId", ""),
            StudentId: dict.GetValueOrDefault("studentId", ""),
            CourseId: dict.GetValueOrDefault("courseId", ""),
            AttemptId: dict.GetValueOrDefault("attemptId", ""),
            CmiStudentId: dict.GetValueOrDefault("cmi.core.student_id", ""),
            CmiStudentName: dict.GetValueOrDefault("cmi.core.student_name", ""),
            CmiLessonStatus: dict.GetValueOrDefault("cmi.core.lesson_status", ""),
            CmiCredit: dict.GetValueOrDefault("cmi.core.credit", ""),
            CmiEntry: dict.GetValueOrDefault("cmi.core.entry", ""),
            CmiExit: dict.GetValueOrDefault("cmi.core.exit", ""),
            CmiScoreRaw: dict.GetValueOrDefault("cmi.core.score.raw", "0"),
            CmiSessionTime: dict.GetValueOrDefault("cmi.core.session_time", "00:00:00"),
            CmiSuspendData: dict.GetValueOrDefault("cmi.suspend_data", ""),
            StartedAt: dict.GetValueOrDefault("startedAt", ""),
            ErrorCode: dict.GetValueOrDefault("error_code", "0"));
    }
}
