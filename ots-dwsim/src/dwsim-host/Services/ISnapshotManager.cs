using DWSIM.Interfaces;

namespace DWSIM.OTS.SimulationHost.Services;

/// <summary>
/// Interface for managing flowsheet snapshots
/// </summary>
public interface ISnapshotManager
{
    /// <summary>
    /// Create a snapshot of a flowsheet's current state
    /// </summary>
    Task<string> CreateSnapshotAsync(string snapshotId, IFlowsheet flowsheet, string name);

    /// <summary>
    /// Restore a flowsheet from a snapshot
    /// </summary>
    Task<IFlowsheet?> RestoreSnapshotAsync(string snapshotId);

    /// <summary>
    /// Delete a snapshot
    /// </summary>
    Task<bool> DeleteSnapshotAsync(string snapshotId);

    /// <summary>
    /// Check if a snapshot exists
    /// </summary>
    Task<bool> SnapshotExistsAsync(string snapshotId);

    /// <summary>
    /// List all snapshots
    /// </summary>
    Task<List<(string snapshotId, string name, DateTime createdAt, long sizeBytes)>> ListSnapshotsAsync();
}
