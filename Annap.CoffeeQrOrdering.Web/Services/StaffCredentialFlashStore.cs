using System.Collections.Concurrent;
using Annap.CoffeeQrOrdering.Web.Pages.Admin.StaffAccounts;

namespace Annap.CoffeeQrOrdering.Web.Services;

public interface IStaffCredentialFlashStore
{
    string Store(StaffCredentialRevealVm reveal);
    StaffCredentialRevealVm? Take(string token);
}

/// <summary>Single-use in-memory flash for admin credential reveal (never persisted to DB).</summary>
public sealed class StaffCredentialFlashStore : IStaffCredentialFlashStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (StaffCredentialRevealVm Reveal, DateTimeOffset ExpiresUtc)> _entries = new();

    public string Store(StaffCredentialRevealVm reveal)
    {
        PurgeExpired();
        var token = Guid.NewGuid().ToString("N");
        _entries[token] = (reveal, DateTimeOffset.UtcNow.Add(Ttl));
        return token;
    }

    public StaffCredentialRevealVm? Take(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        PurgeExpired();
        if (!_entries.TryRemove(token.Trim(), out var entry))
            return null;

        return entry.ExpiresUtc >= DateTimeOffset.UtcNow ? entry.Reveal : null;
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _entries.Where(x => x.Value.ExpiresUtc < now).Select(x => x.Key).ToList())
            _entries.TryRemove(key, out _);
    }
}
