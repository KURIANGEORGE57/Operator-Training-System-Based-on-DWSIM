using DWSIM.OTS.ControlGateway.Models;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Concurrent;

namespace DWSIM.OTS.ControlGateway.Services;

/// <summary>
/// OPC UA Server implementation for DWSIM OTS Control Gateway
/// </summary>
public class OpcUaServer : IOpcUaServer
{
    private readonly ILogger<OpcUaServer> _logger;
    private readonly ConcurrentDictionary<string, NodeState> _nodes = new();
    private OpcUaMapping? _mapping;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public OpcUaServer(ILogger<OpcUaServer> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(OpcUaMapping mapping, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting OPC UA Server with namespace {NamespaceUri}", mapping.Namespace.Uri);

            _mapping = mapping;

            // TODO: Implement full OPC UA server initialization using OPC Foundation .NET stack
            // This is a skeleton implementation showing the structure

            // Steps to implement:
            // 1. Create ApplicationInstance
            // 2. Load application configuration
            // 3. Start server with configured endpoints
            // 4. Create address space from mapping
            // 5. Register node managers

            _logger.LogInformation("Creating OPC UA address space...");
            await CreateAddressSpaceAsync(mapping, cancellationToken);

            _isRunning = true;
            _logger.LogInformation("OPC UA Server started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting OPC UA Server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Stopping OPC UA Server");

            // TODO: Implement proper shutdown
            _isRunning = false;
            _nodes.Clear();

            _logger.LogInformation("OPC UA Server stopped");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping OPC UA Server");
            throw;
        }
    }

    public Task UpdateTagValueAsync(string tagPath, object value)
    {
        try
        {
            if (_nodes.TryGetValue(tagPath, out var node))
            {
                // Update the node value in the OPC UA address space
                if (node is BaseDataVariableState variableNode)
                {
                    variableNode.Value = value;
                    variableNode.Timestamp = DateTime.UtcNow;
                    variableNode.ClearChangeMasks(null, false);

                    _logger.LogDebug("Updated tag {TagPath} = {Value}", tagPath, value);
                }
                else
                {
                    _logger.LogWarning("Node {TagPath} is not a variable node", tagPath);
                }
            }
            else
            {
                _logger.LogWarning("Tag not found in address space: {TagPath}", tagPath);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tag {TagPath}", tagPath);
            throw;
        }
    }

    public Task<object?> ReadTagValueAsync(string tagPath)
    {
        try
        {
            if (_nodes.TryGetValue(tagPath, out var node))
            {
                // Read the actual node value
                if (node is BaseDataVariableState variableNode)
                {
                    _logger.LogDebug("Read tag {TagPath} = {Value}", tagPath, variableNode.Value);
                    return Task.FromResult(variableNode.Value);
                }
                else
                {
                    _logger.LogWarning("Node {TagPath} is not a variable node", tagPath);
                    return Task.FromResult<object?>(null);
                }
            }
            else
            {
                _logger.LogWarning("Tag not found in address space: {TagPath}", tagPath);
                return Task.FromResult<object?>(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading tag {TagPath}", tagPath);
            throw;
        }
    }

    private async Task CreateAddressSpaceAsync(OpcUaMapping mapping, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating folder structure...");

        // Create folders
        foreach (var folder in mapping.Folders)
        {
            CreateFolderNode(folder);
        }

        _logger.LogInformation("Creating tag nodes from explicit mappings...");

        // Create nodes from explicit mappings
        foreach (var explicitMapping in mapping.ExplicitMappings)
        {
            CreateTagNode(explicitMapping);
        }

        _logger.LogInformation("Created {NodeCount} nodes in OPC UA address space", _nodes.Count);

        await Task.CompletedTask;
    }

    private void CreateFolderNode(FolderDefinition folder)
    {
        try
        {
            _logger.LogDebug("Creating folder: {FolderName} with NodeId {NodeId}", folder.Name, folder.NodeId);

            // Create OPC UA folder node
            var nodeState = new FolderState(null)
            {
                NodeId = NodeId.Parse(folder.NodeId),
                BrowseName = new QualifiedName(folder.Name, _mapping?.Namespace.Index ?? 2),
                DisplayName = new LocalizedText(folder.Name),
                Description = new LocalizedText($"Folder: {folder.Name}"),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            _nodes.TryAdd(folder.Name, nodeState);

            // Recursively create children
            if (folder.Children != null)
            {
                foreach (var child in folder.Children)
                {
                    CreateFolderNode(child);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder {FolderName}", folder.Name);
        }
    }

    private void CreateTagNode(ExplicitMapping mapping)
    {
        try
        {
            _logger.LogDebug("Creating tag node: {TagPath} -> {NodeId}", mapping.DwsimTag, mapping.OpcuaNodeId);

            // Create OPC UA variable node with proper data type
            var nodeState = new BaseDataVariableState(null)
            {
                NodeId = NodeId.Parse(mapping.OpcuaNodeId),
                BrowseName = new QualifiedName(mapping.BrowseName, _mapping?.Namespace.Index ?? 2),
                DisplayName = new LocalizedText(mapping.DisplayName),
                Description = mapping.Description != null ? new LocalizedText(mapping.Description) : null,
                DataType = GetOpcDataType(mapping.DataType),
                ValueRank = ValueRanks.Scalar,
                AccessLevel = mapping.Writable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                UserAccessLevel = mapping.Writable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                MinimumSamplingInterval = 100,
                Historizing = false,
                Value = GetDefaultValue(mapping.DataType),
                Timestamp = DateTime.UtcNow,
                StatusCode = StatusCodes.Good
            };

            // Set engineering units if specified
            if (mapping.Units != null)
            {
                nodeState.EngineeringUnits = new PropertyState<EUInformation>(nodeState)
                {
                    Value = new EUInformation(mapping.Units, mapping.Units, "http://www.opcfoundation.org/UA/units/un/cefact")
                };
            }

            // Set EURange if specified
            if (mapping.EngineeringUnits != null)
            {
                nodeState.EURange = new PropertyState<Range>(nodeState)
                {
                    Value = new Range(mapping.EngineeringUnits.High, mapping.EngineeringUnits.Low)
                };
            }

            _nodes.TryAdd(mapping.DwsimTag, nodeState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tag node {TagPath}", mapping.DwsimTag);
        }
    }

    private object GetDefaultValue(string dataType)
    {
        return dataType.ToLower() switch
        {
            "double" => 0.0,
            "float" => 0.0f,
            "int32" => 0,
            "int64" => 0L,
            "boolean" => false,
            "string" => string.Empty,
            "datetime" => DateTime.UtcNow,
            _ => 0.0
        };
    }

    private NodeId GetOpcDataType(string dataType)
    {
        // Map string data type to OPC UA data type NodeId
        return dataType.ToLower() switch
        {
            "double" => DataTypeIds.Double,
            "float" => DataTypeIds.Float,
            "int32" => DataTypeIds.Int32,
            "int64" => DataTypeIds.Int64,
            "boolean" => DataTypeIds.Boolean,
            "string" => DataTypeIds.String,
            "datetime" => DataTypeIds.DateTime,
            _ => DataTypeIds.BaseDataType
        };
    }
}
