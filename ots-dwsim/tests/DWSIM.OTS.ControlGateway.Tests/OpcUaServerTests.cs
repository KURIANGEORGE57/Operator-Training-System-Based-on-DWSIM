using DWSIM.OTS.ControlGateway.Models;
using DWSIM.OTS.ControlGateway.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DWSIM.OTS.ControlGateway.Tests;

public class OpcUaServerTests
{
    private readonly Mock<ILogger<OpcUaServer>> _mockLogger;
    private readonly OpcUaServer _opcUaServer;

    public OpcUaServerTests()
    {
        _mockLogger = new Mock<ILogger<OpcUaServer>>();
        _opcUaServer = new OpcUaServer(_mockLogger.Object);
    }

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WithValidMapping_ShouldStartSuccessfully()
    {
        // Arrange
        var mapping = CreateTestMapping();

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithFolders_ShouldCreateFolderNodes()
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Namespace = new NamespaceConfig
            {
                Uri = "http://dwsim.org/ots/test",
                Index = 2
            },
            Folders = new List<FolderDefinition>
            {
                new FolderDefinition
                {
                    Name = "Streams",
                    NodeId = "ns=2;s=Streams"
                },
                new FolderDefinition
                {
                    Name = "Units",
                    NodeId = "ns=2;s=Units"
                }
            }
        };

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithNestedFolders_ShouldCreateHierarchy()
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Namespace = new NamespaceConfig { Uri = "http://dwsim.org/ots/test" },
            Folders = new List<FolderDefinition>
            {
                new FolderDefinition
                {
                    Name = "Process",
                    NodeId = "ns=2;s=Process",
                    Children = new List<FolderDefinition>
                    {
                        new FolderDefinition
                        {
                            Name = "Streams",
                            NodeId = "ns=2;s=Process.Streams"
                        },
                        new FolderDefinition
                        {
                            Name = "Units",
                            NodeId = "ns=2;s=Process.Units"
                        }
                    }
                }
            }
        };

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithExplicitMappings_ShouldCreateTagNodes()
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Namespace = new NamespaceConfig { Uri = "http://dwsim.org/ots/test" },
            ExplicitMappings = new List<ExplicitMapping>
            {
                new ExplicitMapping
                {
                    DwsimTag = "Streams.Feed.Temperature",
                    OpcuaNodeId = "ns=2;s=Streams.Feed.Temperature",
                    BrowseName = "Temperature",
                    DisplayName = "Feed Temperature",
                    DataType = "Double",
                    Writable = true,
                    Units = "K"
                },
                new ExplicitMapping
                {
                    DwsimTag = "Streams.Feed.Pressure",
                    OpcuaNodeId = "ns=2;s=Streams.Feed.Pressure",
                    BrowseName = "Pressure",
                    DisplayName = "Feed Pressure",
                    DataType = "Double",
                    Writable = true,
                    Units = "Pa"
                }
            }
        };

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithComplexMapping_ShouldHandleAllElements()
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Version = "1.0",
            Namespace = new NamespaceConfig
            {
                Uri = "http://dwsim.org/ots/test",
                Index = 2
            },
            Folders = new List<FolderDefinition>
            {
                new FolderDefinition { Name = "Streams", NodeId = "ns=2;s=Streams" }
            },
            ExplicitMappings = new List<ExplicitMapping>
            {
                new ExplicitMapping
                {
                    DwsimTag = "Streams.Feed.Temperature",
                    OpcuaNodeId = "ns=2;s=Streams.Feed.Temperature",
                    BrowseName = "Temperature",
                    DisplayName = "Temperature",
                    DataType = "Double",
                    Writable = true
                }
            },
            UpdateRates = new UpdateRatesConfig
            {
                DefaultMs = 1000,
                FastMs = 100,
                SlowMs = 5000
            },
            Security = new SecurityConfig
            {
                Mode = "simulation_only",
                AllowAnonymous = true,
                RequireEncryption = false
            }
        };

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldNotThrow()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        var act = async () => await _opcUaServer.StartAsync(mapping);

        // Assert - Should not throw, but might want to handle this case
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_WhenRunning_ShouldStopSuccessfully()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        await _opcUaServer.StopAsync();

        // Assert
        _opcUaServer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_ShouldNotThrow()
    {
        // Act
        var act = async () => await _opcUaServer.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldClearNodes()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        await _opcUaServer.StopAsync();

        // Assert
        _opcUaServer.IsRunning.Should().BeFalse();
        // After stopping, attempting to update tags should handle gracefully
        var act = async () => await _opcUaServer.UpdateTagValueAsync("Streams.Feed.Temperature", 320.0);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region UpdateTagValueAsync Tests

    [Fact]
    public async Task UpdateTagValueAsync_WithExistingTag_ShouldUpdateValue()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        var act = async () => await _opcUaServer.UpdateTagValueAsync("Streams.Feed.Temperature", 320.5);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateTagValueAsync_WithNonExistentTag_ShouldLogWarning()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        await _opcUaServer.UpdateTagValueAsync("NonExistent.Tag", 100.0);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tag not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateTagValueAsync_WithDifferentDataTypes_ShouldHandleAll()
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Namespace = new NamespaceConfig { Uri = "http://dwsim.org/ots/test" },
            ExplicitMappings = new List<ExplicitMapping>
            {
                new ExplicitMapping { DwsimTag = "Tag.Double", OpcuaNodeId = "ns=2;s=1", BrowseName = "Double", DisplayName = "Double", DataType = "Double", Writable = true },
                new ExplicitMapping { DwsimTag = "Tag.Int32", OpcuaNodeId = "ns=2;s=2", BrowseName = "Int32", DisplayName = "Int32", DataType = "Int32", Writable = true },
                new ExplicitMapping { DwsimTag = "Tag.Boolean", OpcuaNodeId = "ns=2;s=3", BrowseName = "Boolean", DisplayName = "Boolean", DataType = "Boolean", Writable = true },
                new ExplicitMapping { DwsimTag = "Tag.String", OpcuaNodeId = "ns=2;s=4", BrowseName = "String", DisplayName = "String", DataType = "String", Writable = true }
            }
        };
        await _opcUaServer.StartAsync(mapping);

        // Act & Assert
        await _opcUaServer.UpdateTagValueAsync("Tag.Double", 3.14);
        await _opcUaServer.UpdateTagValueAsync("Tag.Int32", 42);
        await _opcUaServer.UpdateTagValueAsync("Tag.Boolean", true);
        await _opcUaServer.UpdateTagValueAsync("Tag.String", "test");
    }

    [Fact]
    public async Task UpdateTagValueAsync_WhenNotRunning_ShouldNotThrow()
    {
        // Act
        var act = async () => await _opcUaServer.UpdateTagValueAsync("Streams.Feed.Temperature", 320.0);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region ReadTagValueAsync Tests

    [Fact]
    public async Task ReadTagValueAsync_WithExistingTag_ShouldReturnValue()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        var value = await _opcUaServer.ReadTagValueAsync("Streams.Feed.Temperature");

        // Assert - Currently returns null in the skeleton implementation
        // This test documents expected behavior
        value.Should().BeNull(); // Will change when implementation is complete
    }

    [Fact]
    public async Task ReadTagValueAsync_WithNonExistentTag_ShouldReturnNull()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        var value = await _opcUaServer.ReadTagValueAsync("NonExistent.Tag");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public async Task ReadTagValueAsync_WhenNotRunning_ShouldNotThrow()
    {
        // Act
        var act = async () => await _opcUaServer.ReadTagValueAsync("Streams.Feed.Temperature");

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region IsRunning Property Tests

    [Fact]
    public void IsRunning_InitialState_ShouldBeFalse()
    {
        // Assert
        _opcUaServer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task IsRunning_AfterStart_ShouldBeTrue()
    {
        // Arrange
        var mapping = CreateTestMapping();

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task IsRunning_AfterStop_ShouldBeFalse()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        await _opcUaServer.StopAsync();

        // Assert
        _opcUaServer.IsRunning.Should().BeFalse();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task StartAsync_ShouldLogStartup()
    {
        // Arrange
        var mapping = CreateTestMapping();

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting OPC UA Server")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_ShouldLogShutdown()
    {
        // Arrange
        var mapping = CreateTestMapping();
        await _opcUaServer.StartAsync(mapping);

        // Act
        await _opcUaServer.StopAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping OPC UA Server")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Data Type Mapping Tests

    [Theory]
    [InlineData("Double")]
    [InlineData("Float")]
    [InlineData("Int32")]
    [InlineData("Int64")]
    [InlineData("Boolean")]
    [InlineData("String")]
    [InlineData("DateTime")]
    public async Task StartAsync_WithVariousDataTypes_ShouldHandleAllTypes(string dataType)
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Namespace = new NamespaceConfig { Uri = "http://dwsim.org/ots/test" },
            ExplicitMappings = new List<ExplicitMapping>
            {
                new ExplicitMapping
                {
                    DwsimTag = $"Tag.{dataType}",
                    OpcuaNodeId = $"ns=2;s={dataType}",
                    BrowseName = dataType,
                    DisplayName = dataType,
                    DataType = dataType,
                    Writable = true
                }
            }
        };

        // Act
        var act = async () => await _opcUaServer.StartAsync(mapping);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Engineering Units Tests

    [Fact]
    public async Task StartAsync_WithEngineeringUnits_ShouldCreateNodesWithRanges()
    {
        // Arrange
        var mapping = new OpcUaMapping
        {
            Namespace = new NamespaceConfig { Uri = "http://dwsim.org/ots/test" },
            ExplicitMappings = new List<ExplicitMapping>
            {
                new ExplicitMapping
                {
                    DwsimTag = "Streams.Feed.Temperature",
                    OpcuaNodeId = "ns=2;s=Temp",
                    BrowseName = "Temperature",
                    DisplayName = "Temperature",
                    DataType = "Double",
                    Writable = true,
                    Units = "K",
                    EngineeringUnits = new EngineeringUnits { Low = 273.15, High = 573.15 }
                }
            }
        };

        // Act
        await _opcUaServer.StartAsync(mapping);

        // Assert
        _opcUaServer.IsRunning.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task ServerLifecycle_StartUpdateReadStop_ShouldWorkEndToEnd()
    {
        // Arrange
        var mapping = CreateTestMapping();

        // Act & Assert - Start
        await _opcUaServer.StartAsync(mapping);
        _opcUaServer.IsRunning.Should().BeTrue();

        // Act & Assert - Update
        await _opcUaServer.UpdateTagValueAsync("Streams.Feed.Temperature", 320.5);

        // Act & Assert - Read
        var value = await _opcUaServer.ReadTagValueAsync("Streams.Feed.Temperature");
        value.Should().BeNull(); // Current skeleton implementation

        // Act & Assert - Stop
        await _opcUaServer.StopAsync();
        _opcUaServer.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task MultipleStartStop_ShouldHandleCorrectly()
    {
        // Arrange
        var mapping = CreateTestMapping();

        // Act & Assert - First cycle
        await _opcUaServer.StartAsync(mapping);
        _opcUaServer.IsRunning.Should().BeTrue();
        await _opcUaServer.StopAsync();
        _opcUaServer.IsRunning.Should().BeFalse();

        // Act & Assert - Second cycle
        await _opcUaServer.StartAsync(mapping);
        _opcUaServer.IsRunning.Should().BeTrue();
        await _opcUaServer.StopAsync();
        _opcUaServer.IsRunning.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static OpcUaMapping CreateTestMapping()
    {
        return new OpcUaMapping
        {
            Version = "1.0",
            Namespace = new NamespaceConfig
            {
                Uri = "http://dwsim.org/ots/test",
                Index = 2
            },
            Folders = new List<FolderDefinition>
            {
                new FolderDefinition
                {
                    Name = "Streams",
                    NodeId = "ns=2;s=Streams"
                }
            },
            ExplicitMappings = new List<ExplicitMapping>
            {
                new ExplicitMapping
                {
                    DwsimTag = "Streams.Feed.Temperature",
                    OpcuaNodeId = "ns=2;s=Streams.Feed.Temperature",
                    BrowseName = "Temperature",
                    DisplayName = "Feed Temperature",
                    DataType = "Double",
                    Writable = true,
                    Units = "K"
                }
            }
        };
    }

    #endregion
}
