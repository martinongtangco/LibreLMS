namespace LibreLms.Modules.Enrollment.Application;

/// <summary>Which flow is being throttled (spec 027 FR-010/FR-013/FR-018, R6).</summary>
public enum ThrottleFlow { Signup, PasswordReset, ResendVerification }

/// <summary>
/// In-memory per-email sliding-window throttle (spec 027 R6): sign-ups 10/24 h,
/// password-reset requests 5/1 h, verification resends 3/1 h.
/// Thread-safe; expired entries are purged opportunistically on each call.
/// Lost on restart by design — a dev safeguard, not a compliance control.
/// One sentence: caps how often one email address can hammer one flow.
/// </summary>
public sealed class EmailThrottle
{
    private static readonly Dictionary<ThrottleFlow, (int Cap, TimeSpan Window)> Rules = new()
    {
        [ThrottleFlow.Signup] = (10, TimeSpan.FromHours(24)),
        [ThrottleFlow.PasswordReset] = (5, TimeSpan.FromHours(1)),
        [ThrottleFlow.ResendVerification] = (3, TimeSpan.FromHours(1)),
    };

    private readonly object _gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _attempts = new(); // key: "{flow}|{email}"

    /// <summary>
    /// Record an attempt and report whether it is allowed. The attempt counts toward the
    /// window whether or not the rest of the flow succeeds (throttling is checked before
    /// validation, so repeated invalid submissions for an email are throttled too).
    /// </summary>
    public bool Allow(string normalizedEmail, ThrottleFlow flow)
    {
        var (cap, window) = Rules[flow];
        var now = DateTimeOffset.UtcNow;
        var key = $"{flow}|{normalizedEmail}";

        lock (_gate)
        {
            PurgeExpired(now);

            if (!_attempts.TryGetValue(key, out var attempts))
            {
                attempts = new List<DateTimeOffset>();
                _attempts[key] = attempts;
            }

            var cutoff = now - window;
            attempts.RemoveAll(t => t < cutoff);

            if (attempts.Count >= cap)
                return false;

            attempts.Add(now);
            return true;
        }
    }

    /// <summary>Drop entries whose attempts are all outside their window (opportunistic housekeeping).</summary>
    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (var (key, attempts) in _attempts)
        {
            var pipe = key.IndexOf('|');
            if (pipe <= 0 || !Enum.TryParse<ThrottleFlow>(key[..pipe], out var flow))
                continue;

            var (_, window) = Rules[flow];
            if (attempts.Count == 0 || attempts[^1] < now - window)
                _attempts.Remove(key);
        }
    }
}
