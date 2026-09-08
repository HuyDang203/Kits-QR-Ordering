namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Approximate live connection counts for internal diagnostics only.</summary>
public sealed class HubConnectionRegistry
{
    private long _guestOrderFollowers;
    private long _staffBoard;

    public void GuestJoined() => Interlocked.Increment(ref _guestOrderFollowers);

    public void GuestLeft() => Interlocked.Decrement(ref _guestOrderFollowers);

    public void StaffJoined() => Interlocked.Increment(ref _staffBoard);

    public void StaffLeft() => Interlocked.Decrement(ref _staffBoard);

    public (int GuestOrderFollowers, int StaffBoard) Snapshot() =>
        ((int)Math.Clamp(Interlocked.Read(ref _guestOrderFollowers), 0, 50_000),
            (int)Math.Clamp(Interlocked.Read(ref _staffBoard), 0, 50_000));
}
