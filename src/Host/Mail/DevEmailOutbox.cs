using LibreLms.SharedKernel;

namespace LibreLms.Host.Mail;

/// <summary>
/// Developer-observable record of every "sent" email (spec 027 FR-020).
/// Bounded ring of ~200 newest-first entries; thread-safe; lost on app restart by design
/// (a dev artifact, not durable state — Constitution VI).
/// </summary>
public sealed class DevEmailOutbox
{
    public const int MaxEntries = 200;

    private readonly object _gate = new();
    private readonly List<OutboxEntry> _entries = new(); // index 0 = newest

    /// <summary>Record an email as sent (newest first).</summary>
    public void Add(OutboundEmail email)
    {
        lock (_gate)
        {
            _entries.Insert(0, new OutboxEntry(email, DateTimeOffset.UtcNow));
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }
    }

    /// <summary>All recorded entries, newest first.</summary>
    public IReadOnlyList<OutboxEntry> List()
    {
        lock (_gate)
        {
            return _entries.ToList();
        }
    }

    /// <summary>Drop all recorded entries.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}

/// <summary>One recorded email: the payload plus when the mock "sent" it.</summary>
public sealed record OutboxEntry(OutboundEmail Email, DateTimeOffset SentAtUtc);
