using System.Text.Json;
using StackExchange.Redis;

namespace LibreLms.Modules.Scorm.Infrastructure;

/// <summary>
/// Valkey-backed session state for live SCORM sessions.
/// Uses StackExchange.Redis against Valkey (Redis-protocol-compatible).
/// Key pattern: scorm:session:{sessionId}
/// Secondary index (spec 048 E8): scorm:active:{studentId}:{courseId} → sessionId
/// TTL: 30 minutes (both the session hash and its index entry)
/// </summary>
public class ScormSessionStore : IScormSessionStore
{
    private readonly IDatabase _redis;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    public ScormSessionStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _redis = connectionMultiplexer.GetDatabase();
    }

    /// <summary>
    /// Create a new session with default CMI values. Also writes the secondary index
    /// <c>scorm:active:{studentId}:{courseId} → sessionId</c> (spec 048 E8) with the same
    /// TTL as the session hash.
    /// </summary>
    public async Task CreateSessionAsync(SessionData data)
    {
        var key = SessionKey(data.SessionId.ToString());

        await _redis.HashSetAsync(key, data.ToHashEntries());
        await _redis.KeyExpireAsync(key, DefaultTtl);

        // The hash stores student/course as lowercase guid strings (SessionData.ToString()).
        if (Guid.TryParse(data.StudentId, out var studentId)
            && Guid.TryParse(data.CourseId, out var courseId))
        {
            await _redis.StringSetAsync(ActiveIndexKey(studentId, courseId), data.SessionId, DefaultTtl);
        }
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

        // Refresh the secondary-index TTL in step with the hash TTL (spec 048 E8):
        // the hash TTL is extended on every commit, so the index must be too —
        // otherwise an actively-committing session older than 30 minutes would lose
        // its scorm:active:{student}:{course} entry and a relaunch would start a
        // second live attempt (regression vs. the old full-keyspace scan).
        // One round trip reads both identity fields. Skip silently if either is
        // missing or not a guid (defensive; CreateSessionAsync always writes both).
        var identity = await _redis.HashGetAsync(key, new RedisValue[] { "studentId", "courseId" });
        if (identity.Length == 2
            && Guid.TryParse(identity[0].ToString(), out var studentId)
            && Guid.TryParse(identity[1].ToString(), out var courseId))
        {
            await _redis.KeyExpireAsync(ActiveIndexKey(studentId, courseId), DefaultTtl);
        }

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

    /// <summary>Delete a session and its secondary-index entry (cleanup on LMSFinish).</summary>
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        // Read first so we know the student/course pair for the index key.
        // A missing session has nothing to do (spec 048 E8).
        var session = await ReadSessionAsync(sessionId);
        if (session is null)
            return;

        if (Guid.TryParse(session.StudentId, out var studentId)
            && Guid.TryParse(session.CourseId, out var courseId))
        {
            await _redis.KeyDeleteAsync(ActiveIndexKey(studentId, courseId));
        }
        await _redis.KeyDeleteAsync(SessionKey(sessionId));
    }

    /// <summary>Check if a session exists (for concurrent session detection).</summary>
    public async Task<bool> SessionExistsAsync(Guid sessionId)
    {
        var key = SessionKey(sessionId);
        return await _redis.KeyExistsAsync(key);
    }

    /// <summary>
    /// Find the active session ID for a student/course pair via the secondary index
    /// <c>scorm:active:{studentId}:{courseId}</c> (spec 048 E8) — O(1) in the live
    /// session count, unlike the old blocking KEYS scan. If the index points at a
    /// hash that no longer exists (session expired/deleted while the index TTL was
    /// still live), the stale index is deleted and null is returned.
    /// </summary>
    public async Task<string?> FindActiveSessionKeyAsync(Guid studentId, Guid courseId)
    {
        var indexKey = ActiveIndexKey(studentId, courseId);
        var value = await _redis.StringGetAsync(indexKey);
        if (value.IsNullOrEmpty)
            return null;

        if (!Guid.TryParse(value.ToString(), out var sessionId))
        {
            // Corrupt index entry — self-clean and report no active session.
            await _redis.KeyDeleteAsync(indexKey);
            return null;
        }

        var session = await ReadSessionAsync(sessionId);
        if (session is null)
        {
            await _redis.KeyDeleteAsync(indexKey);
            return null;
        }

        return sessionId.ToString();
    }

    private static RedisKey SessionKey(Guid sessionId) => $"scorm:session:{sessionId}";
    private static RedisKey SessionKey(string sessionId) => $"scorm:session:{sessionId}";

    /// <summary>
    /// Secondary-index key (spec 048 E8). Guid.ToString() emits lowercase "D" format,
    /// matching the lowercase guid strings stored in the session hash, so the key is
    /// stable across create (SessionData fields) and lookup (parsed guids).
    /// </summary>
    private static RedisKey ActiveIndexKey(Guid studentId, Guid courseId)
        => $"scorm:active:{studentId.ToString()}:{courseId.ToString()}";
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
