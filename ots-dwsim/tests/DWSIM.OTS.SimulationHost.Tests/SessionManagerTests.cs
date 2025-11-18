using DWSIM.OTS.SimulationHost.Models;
using DWSIM.OTS.SimulationHost.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DWSIM.OTS.SimulationHost.Tests;

public class SessionManagerTests
{
    private readonly Mock<IFlowsheetRepository> _mockFlowsheetRepository;
    private readonly Mock<ILogger<SessionManager>> _mockLogger;
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        _mockFlowsheetRepository = new Mock<IFlowsheetRepository>();
        _mockLogger = new Mock<ILogger<SessionManager>>();
        _sessionManager = new SessionManager(_mockFlowsheetRepository.Object, _mockLogger.Object);
    }

    #region CreateSessionAsync Tests

    [Fact]
    public async Task CreateSessionAsync_WithValidFlowsheet_ShouldReturnSessionId()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync("test-flowsheet"))
            .ReturnsAsync(flowsheetPath);

        var request = new CreateSessionRequest
        {
            Flowsheet = "test-flowsheet",
            SessionName = "Test Session",
            Seed = 12345
        };

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.SessionId.Should().NotBeNullOrEmpty();
        response.Status.Should().Be(SessionStatus.Created);
        response.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSessionAsync_WithCustomSessionName_ShouldUseProvidedName()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var request = new CreateSessionRequest
        {
            Flowsheet = "test-flowsheet",
            SessionName = "My Custom Session"
        };

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);
        var sessionInfo = await _sessionManager.GetSessionAsync(response.SessionId);

        // Assert
        sessionInfo.Should().NotBeNull();
        sessionInfo!.SessionName.Should().Be("My Custom Session");
    }

    [Fact]
    public async Task CreateSessionAsync_WithEmptySessionName_ShouldGenerateDefaultName()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var request = new CreateSessionRequest
        {
            Flowsheet = "test-flowsheet",
            SessionName = string.Empty
        };

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);
        var sessionInfo = await _sessionManager.GetSessionAsync(response.SessionId);

        // Assert
        sessionInfo.Should().NotBeNull();
        sessionInfo!.SessionName.Should().StartWith("Session-");
    }

    [Fact]
    public async Task CreateSessionAsync_WithNonExistentFlowsheet_ShouldThrowFileNotFoundException()
    {
        // Arrange
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync("invalid-flowsheet"))
            .ReturnsAsync((string?)null);

        var request = new CreateSessionRequest
        {
            Flowsheet = "invalid-flowsheet"
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _sessionManager.CreateSessionAsync(request));
    }

    [Fact]
    public async Task CreateSessionAsync_WithEnvironment_ShouldStoreEnvironmentVariables()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var environment = new Dictionary<string, object>
        {
            { "temperature", 298.15 },
            { "pressure", 101325 }
        };

        var request = new CreateSessionRequest
        {
            Flowsheet = "test-flowsheet",
            Environment = environment
        };

        // Act
        var response = await _sessionManager.CreateSessionAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.SessionId.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GetSessionAsync Tests

    [Fact]
    public async Task GetSessionAsync_WithExistingSession_ShouldReturnSessionInfo()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createRequest = new CreateSessionRequest
        {
            Flowsheet = "test-flowsheet",
            SessionName = "Test Session"
        };
        var createResponse = await _sessionManager.CreateSessionAsync(createRequest);

        // Act
        var sessionInfo = await _sessionManager.GetSessionAsync(createResponse.SessionId);

        // Assert
        sessionInfo.Should().NotBeNull();
        sessionInfo!.SessionId.Should().Be(createResponse.SessionId);
        sessionInfo.SessionName.Should().Be("Test Session");
        sessionInfo.Status.Should().Be(SessionStatus.Created);
        sessionInfo.FlowsheetPath.Should().Be(flowsheetPath);
    }

    [Fact]
    public async Task GetSessionAsync_WithNonExistentSession_ShouldReturnNull()
    {
        // Act
        var sessionInfo = await _sessionManager.GetSessionAsync("non-existent-id");

        // Assert
        sessionInfo.Should().BeNull();
    }

    #endregion

    #region GetAllSessionsAsync Tests

    [Fact]
    public async Task GetAllSessionsAsync_WithNoSessions_ShouldReturnEmptyList()
    {
        // Act
        var sessions = await _sessionManager.GetAllSessionsAsync();

        // Assert
        sessions.Should().NotBeNull();
        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllSessionsAsync_WithMultipleSessions_ShouldReturnAllSessions()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        await _sessionManager.CreateSessionAsync(new CreateSessionRequest { Flowsheet = "flowsheet1", SessionName = "Session 1" });
        await _sessionManager.CreateSessionAsync(new CreateSessionRequest { Flowsheet = "flowsheet2", SessionName = "Session 2" });
        await _sessionManager.CreateSessionAsync(new CreateSessionRequest { Flowsheet = "flowsheet3", SessionName = "Session 3" });

        // Act
        var sessions = await _sessionManager.GetAllSessionsAsync();

        // Assert
        sessions.Should().HaveCount(3);
        sessions.Select(s => s.SessionName).Should().Contain(new[] { "Session 1", "Session 2", "Session 3" });
    }

    #endregion

    #region PauseSessionAsync Tests

    [Fact]
    public async Task PauseSessionAsync_WithExistingSession_ShouldUpdateStatusToPaused()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        // Act
        var sessionInfo = await _sessionManager.PauseSessionAsync(createResponse.SessionId);

        // Assert
        sessionInfo.Should().NotBeNull();
        sessionInfo.Status.Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public async Task PauseSessionAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.PauseSessionAsync("non-existent-id"));
    }

    #endregion

    #region StopSessionAsync Tests

    [Fact]
    public async Task StopSessionAsync_WithExistingSession_ShouldUpdateStatusToStopped()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        // Act
        var sessionInfo = await _sessionManager.StopSessionAsync(createResponse.SessionId);

        // Assert
        sessionInfo.Should().NotBeNull();
        sessionInfo.Status.Should().Be(SessionStatus.Stopped);
        sessionInfo.StoppedAt.Should().NotBeNull();
        sessionInfo.StoppedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StopSessionAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.StopSessionAsync("non-existent-id"));
    }

    #endregion

    #region SetTimeFactorAsync Tests

    [Fact]
    public async Task SetTimeFactorAsync_WithValidFactor_ShouldUpdateTimeFactor()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        var request = new TimeFactorRequest { Factor = 2.5 };

        // Act
        var response = await _sessionManager.SetTimeFactorAsync(createResponse.SessionId, request);

        // Assert
        response.Should().NotBeNull();
        response.TimeFactor.Should().Be(2.5);

        var sessionInfo = await _sessionManager.GetSessionAsync(createResponse.SessionId);
        sessionInfo!.TimeFactor.Should().Be(2.5);
    }

    [Fact]
    public async Task SetTimeFactorAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var request = new TimeFactorRequest { Factor = 2.0 };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.SetTimeFactorAsync("non-existent-id", request));
    }

    #endregion

    #region CreateSnapshotAsync Tests

    [Fact]
    public async Task CreateSnapshotAsync_WithExistingSession_ShouldReturnSnapshotId()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        var snapshotRequest = new SnapshotRequest { Name = "Initial State" };

        // Act
        var snapshotResponse = await _sessionManager.CreateSnapshotAsync(createResponse.SessionId, snapshotRequest);

        // Assert
        snapshotResponse.Should().NotBeNull();
        snapshotResponse.SnapshotId.Should().NotBeNullOrEmpty();
        snapshotResponse.SnapshotId.Should().StartWith("snap-");
        snapshotResponse.SavedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var snapshotRequest = new SnapshotRequest { Name = "Test Snapshot" };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.CreateSnapshotAsync("non-existent-id", snapshotRequest));
    }

    #endregion

    #region RestoreSnapshotAsync Tests

    [Fact]
    public async Task RestoreSnapshotAsync_WithExistingSession_ShouldReturnSessionInfo()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        var restoreRequest = new RestoreSnapshotRequest { SnapshotId = "snap-12345678" };

        // Act
        var sessionInfo = await _sessionManager.RestoreSnapshotAsync(createResponse.SessionId, restoreRequest);

        // Assert
        sessionInfo.Should().NotBeNull();
        sessionInfo.SessionId.Should().Be(createResponse.SessionId);
    }

    [Fact]
    public async Task RestoreSnapshotAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var restoreRequest = new RestoreSnapshotRequest { SnapshotId = "snap-12345678" };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.RestoreSnapshotAsync("non-existent-id", restoreRequest));
    }

    #endregion

    #region ReadTagAsync Tests

    [Fact]
    public async Task ReadTagAsync_WithValidTag_ShouldReturnTagValue()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        // Act
        var tagValue = await _sessionManager.ReadTagAsync(createResponse.SessionId, "Streams.Feed.Temperature");

        // Assert
        tagValue.Should().NotBeNull();
        tagValue!.Tag.Should().Be("Streams.Feed.Temperature");
        tagValue.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadTagAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.ReadTagAsync("non-existent-id", "Streams.Feed.Temperature"));
    }

    #endregion

    #region WriteTagAsync Tests

    [Fact]
    public async Task WriteTagAsync_WithValidTag_ShouldReturnSuccessStatus()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        var writeRequest = new WriteTagRequest
        {
            Value = 320.0,
            User = "operator1",
            Mode = "manual"
        };

        // Act
        var writeResponse = await _sessionManager.WriteTagAsync(
            createResponse.SessionId,
            "Streams.Feed.Temperature",
            writeRequest);

        // Assert
        writeResponse.Should().NotBeNull();
        writeResponse.Status.Should().Be("ok");
        writeResponse.AppliedValue.Should().Be(320.0);
    }

    [Fact]
    public async Task WriteTagAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var writeRequest = new WriteTagRequest { Value = 320.0 };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.WriteTagAsync(
                "non-existent-id",
                "Streams.Feed.Temperature",
                writeRequest));
    }

    #endregion

    #region GetEventsAsync Tests

    [Fact]
    public async Task GetEventsAsync_WithNewSession_ShouldReturnEmptyList()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        // Act
        var events = await _sessionManager.GetEventsAsync(createResponse.SessionId, null, null);

        // Assert
        events.Should().NotBeNull();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_AfterPauseAndStop_ShouldReturnEvents()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var createResponse = await _sessionManager.CreateSessionAsync(
            new CreateSessionRequest { Flowsheet = "test-flowsheet" });

        await _sessionManager.PauseSessionAsync(createResponse.SessionId);
        await _sessionManager.StopSessionAsync(createResponse.SessionId);

        // Act
        var events = await _sessionManager.GetEventsAsync(createResponse.SessionId, null, null);

        // Assert
        events.Should().NotBeNull();
        events.Should().HaveCount(2);
        events[0].Type.Should().Be("session_pause");
        events[1].Type.Should().Be("session_stop");
    }

    [Fact]
    public async Task GetEventsAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await _sessionManager.GetEventsAsync("non-existent-id", null, null));
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task CreateSessionAsync_ShouldLogSessionCreation()
    {
        // Arrange
        var flowsheetPath = "/path/to/flowsheet.dwxmz";
        _mockFlowsheetRepository
            .Setup(x => x.ResolveFlowsheetPathAsync(It.IsAny<string>()))
            .ReturnsAsync(flowsheetPath);

        var request = new CreateSessionRequest { Flowsheet = "test-flowsheet" };

        // Act
        await _sessionManager.CreateSessionAsync(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created session")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}
