namespace GlurSolutions.Web.Security;

/// <summary>
/// Temporary lockout after repeated failed logins: per client IP, plus a looser limit over everyone
/// (to slow down attacks spread across many addresses).
/// In memory only: it resets when the app restarts and is not shared between instances.
/// That is fine for a single App Service instance.
/// </summary>
public sealed class LoginThrottle
{
    public const int MaxFailuresPerClient = 5;
    public const int MaxFailuresOverall = 50;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    private const string Overall = "*overall*";

    private sealed class Entry
    {
        public int Failures;
        public DateTime WindowStartUtc;
        public DateTime LockedUntilUtc;
    }

    private readonly Dictionary<string, Entry> _entries = new();
    private readonly object _gate = new();

    public bool IsLocked(string client, out TimeSpan retryAfter)
    {
        var now = DateTime.UtcNow;
        retryAfter = TimeSpan.Zero;
        lock (_gate)
        {
            Prune(now);
            foreach (var key in new[] { client, Overall })
            {
                if (_entries.TryGetValue(key, out var e) && e.LockedUntilUtc > now)
                {
                    var left = e.LockedUntilUtc - now;
                    if (left > retryAfter) retryAfter = left;
                }
            }
            return retryAfter > TimeSpan.Zero;
        }
    }

    public void RecordFailure(string client)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            Bump(client, MaxFailuresPerClient, now);
            Bump(Overall, MaxFailuresOverall, now);
        }
    }

    /// <summary>Clears this client's failures after a successful login.</summary>
    public void Reset(string client)
    {
        lock (_gate) _entries.Remove(client);
    }

    private void Bump(string key, int max, DateTime now)
    {
        if (!_entries.TryGetValue(key, out var e))
            _entries[key] = e = new Entry { WindowStartUtc = now };

        if (now - e.WindowStartUtc > Window)
        {
            e.Failures = 0;
            e.WindowStartUtc = now;
        }

        e.Failures++;
        if (e.Failures >= max)
        {
            e.LockedUntilUtc = now + Lockout;
            e.Failures = 0;
            e.WindowStartUtc = now;
        }
    }

    private void Prune(DateTime now)
    {
        var stale = _entries
            .Where(kv => kv.Value.LockedUntilUtc <= now && now - kv.Value.WindowStartUtc > Window)
            .Select(kv => kv.Key).ToList();
        foreach (var key in stale) _entries.Remove(key);
    }
}
