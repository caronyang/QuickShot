using QuickShot.Models;

namespace QuickShot.Services;

/// <summary>Tracks the floating snapshots and enforces the configured limit.</summary>
public sealed class SnapshotManager
{
    private readonly SettingsService _settings;
    private readonly List<Snapshot> _snapshots = new();

    public SnapshotManager(SettingsService settings) => _settings = settings;

    public IReadOnlyList<Snapshot> Items => _snapshots;

    public int Count => _snapshots.Count;

    public int MaxCount => Math.Max(1, _settings.Current.MaxSnapshots);

    public bool IsFull => Count >= MaxCount;

    /// <summary>True when at least one floating window is showing its full size.</summary>
    public bool AnyExpanded => _snapshots.Any(snapshot => !snapshot.IsCollapsed);

    public event EventHandler? Changed;

    public void Add(Snapshot snapshot)
    {
        _snapshots.Add(snapshot);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(Snapshot snapshot)
    {
        if (!_snapshots.Remove(snapshot)) return false;
        snapshot.ReleaseFullImage();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        foreach (var snapshot in _snapshots) snapshot.ReleaseFullImage();
        _snapshots.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Oldest first - used by the "which one should I replace?" dialog.</summary>
    public IReadOnlyList<Snapshot> OldestFirst() =>
        _snapshots.OrderBy(s => s.Sequence).ToList();

    public Snapshot? Oldest => _snapshots.MinBy(s => s.Sequence);
}
