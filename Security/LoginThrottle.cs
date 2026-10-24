using System.Collections.Concurrent;

namespace GlurSolutions.Web.Security;

/// <summary>
/// Slows down password guessing: after 5 wrong attempts from one visitor address, that address is blocked from the
/// login for 15 minutes. It is per address on purpose: a global lock-out would let an attacker lock YOU out.
/// The counters live in memory, so they reset when the app restarts.
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private const int MaxTrackedAddresses = 10_000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private sealed class Entry
    {
        public int Failures;
        public DateTime WindowStartUtc;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public bool IsLockedOut(string key, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_entries.TryGetValue(key, out var entry)) return false;

        lock (entry)
        {
            var elapsed = DateTime.UtcNow - entry.WindowStartUtc;
            if (elapsed >= Window)
            {
                _entries.TryRemove(key, out _);
                return false;
            }
            if (entry.Failures < MaxFailures) return false;

            retryAfter = Window - elapsed;
            return true;
        }
    }

    public void RecordFailure(string key)
    {
        if (_entries.Count > MaxTrackedAddresses) Prune();

        var entry = _entries.GetOrAdd(key, _ => new Entry { WindowStartUtc = DateTime.UtcNow });
        lock (entry)
        {
            if (DateTime.UtcNow - entry.WindowStartUtc >= Window)
            {
                entry.Failures = 0;
                entry.WindowStartUtc = DateTime.UtcNow;
            }
            entry.Failures++;
        }
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);

    // Keeps memory bounded if someone floods the login from many addresses
    private void Prune()
    {
        var cutoff = DateTime.UtcNow - Window;
        foreach (var pair in _entries)
        {
            if (pair.Value.WindowStartUtc < cutoff) _entries.TryRemove(pair.Key, out _);
        }
        if (_entries.Count > MaxTrackedAddresses) _entries.Clear();
    }
}
