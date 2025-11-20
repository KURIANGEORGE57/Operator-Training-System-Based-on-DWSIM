using DWSIM.Interfaces;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace DWSIM.OTS.SimulationHost.Services;

/// <summary>
/// Manages flowsheet snapshots using XML serialization
/// </summary>
public class SnapshotManager : ISnapshotManager
{
    private readonly string _snapshotDirectory;
    private readonly ILogger<SnapshotManager> _logger;
    private readonly ConcurrentDictionary<string, SnapshotMetadata> _metadata = new();

    public SnapshotManager(IConfiguration configuration, ILogger<SnapshotManager> logger)
    {
        _snapshotDirectory = configuration.GetValue<string>("SnapshotPath") ?? "snapshots";
        _logger = logger;

        // Ensure snapshot directory exists
        Directory.CreateDirectory(_snapshotDirectory);

        _logger.LogInformation("SnapshotManager initialized with path: {Path}", _snapshotDirectory);
    }

    public async Task<string> CreateSnapshotAsync(string snapshotId, IFlowsheet flowsheet, string name)
    {
        try
        {
            var snapshotPath = GetSnapshotPath(snapshotId);

            _logger.LogInformation("Creating snapshot {SnapshotId} at {Path}", snapshotId, snapshotPath);

            // Use DWSIM's built-in save functionality
            await Task.Run(() =>
            {
                // Save flowsheet to XML
                flowsheet.SaveToXML(snapshotPath);
            });

            // Store metadata
            var fileInfo = new FileInfo(snapshotPath);
            _metadata[snapshotId] = new SnapshotMetadata
            {
                SnapshotId = snapshotId,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                FilePath = snapshotPath,
                SizeBytes = fileInfo.Length
            };

            _logger.LogInformation("Snapshot {SnapshotId} created successfully ({Size} bytes)",
                snapshotId, fileInfo.Length);

            return snapshotPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot {SnapshotId}", snapshotId);
            throw;
        }
    }

    public async Task<IFlowsheet?> RestoreSnapshotAsync(string snapshotId)
    {
        try
        {
            var snapshotPath = GetSnapshotPath(snapshotId);

            if (!File.Exists(snapshotPath))
            {
                _logger.LogWarning("Snapshot file not found: {Path}", snapshotPath);
                return null;
            }

            _logger.LogInformation("Restoring snapshot {SnapshotId} from {Path}", snapshotId, snapshotPath);

            // Load flowsheet from XML using DWSIM Automation
            IFlowsheet? flowsheet = null;
            await Task.Run(() =>
            {
                var automation = new DWSIM.Automation.Automation2();
                flowsheet = automation.LoadFlowsheet(snapshotPath);
            });

            if (flowsheet != null)
            {
                _logger.LogInformation("Snapshot {SnapshotId} restored successfully", snapshotId);
            }
            else
            {
                _logger.LogWarning("Failed to restore snapshot {SnapshotId}", snapshotId);
            }

            return flowsheet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring snapshot {SnapshotId}", snapshotId);
            throw;
        }
    }

    public Task<bool> DeleteSnapshotAsync(string snapshotId)
    {
        try
        {
            var snapshotPath = GetSnapshotPath(snapshotId);

            if (File.Exists(snapshotPath))
            {
                File.Delete(snapshotPath);
                _metadata.TryRemove(snapshotId, out _);

                _logger.LogInformation("Deleted snapshot {SnapshotId}", snapshotId);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snapshot {SnapshotId}", snapshotId);
            return Task.FromResult(false);
        }
    }

    public Task<bool> SnapshotExistsAsync(string snapshotId)
    {
        var snapshotPath = GetSnapshotPath(snapshotId);
        return Task.FromResult(File.Exists(snapshotPath));
    }

    public Task<List<(string snapshotId, string name, DateTime createdAt, long sizeBytes)>> ListSnapshotsAsync()
    {
        var snapshots = _metadata.Values
            .Select(m => (m.SnapshotId, m.Name, m.CreatedAt, m.SizeBytes))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(snapshots);
    }

    private string GetSnapshotPath(string snapshotId)
    {
        // Use .dwxmz extension (DWSIM compressed XML format)
        return Path.Combine(_snapshotDirectory, $"{snapshotId}.dwxmz");
    }

    private class SnapshotMetadata
    {
        public required string SnapshotId { get; set; }
        public required string Name { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required string FilePath { get; set; }
        public required long SizeBytes { get; set; }
    }
}
