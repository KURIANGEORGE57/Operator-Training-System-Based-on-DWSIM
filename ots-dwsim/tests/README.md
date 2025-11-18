# OTS-DWSIM Testing Guide

This directory contains the test suite for the DWSIM Operator Training System (OTS).

## Overview

The test infrastructure uses modern .NET testing practices with the following stack:

- **xUnit** - Test framework for writing and running tests
- **Moq** - Mocking framework for isolating dependencies
- **FluentAssertions** - Expressive assertion library for readable tests
- **Coverlet** - Code coverage analysis tool
- **ReportGenerator** - Coverage report visualization

## Test Projects

### DWSIM.OTS.SimulationHost.Tests

Tests for the Simulation Host REST API service (`ots-dwsim/src/dwsim-host`).

**Key test classes:**
- `SessionManagerTests.cs` - Comprehensive tests for session lifecycle management

**Coverage areas:**
- Session creation and configuration
- Session state management (start, pause, stop)
- Tag read/write operations
- Snapshot creation and restoration
- Time stepping and time factor control
- Event logging
- Error handling and validation

### DWSIM.OTS.ControlGateway.Tests

Tests for the OPC UA Control Gateway service (`ots-dwsim/src/control-gateway`).

**Key test classes:**
- `OpcUaServerTests.cs` - Tests for OPC UA server implementation

**Coverage areas:**
- OPC UA server startup and shutdown
- Address space creation from mappings
- Folder hierarchy creation
- Tag node creation with various data types
- Tag value updates and reads
- Engineering units and metadata
- Server lifecycle management

## Running Tests

### Run All Tests

```bash
# From repository root
dotnet test ots-dwsim/tests

# From tests directory
cd ots-dwsim/tests
dotnet test
```

### Run Specific Test Project

```bash
# Simulation Host tests only
dotnet test ots-dwsim/tests/DWSIM.OTS.SimulationHost.Tests

# Control Gateway tests only
dotnet test ots-dwsim/tests/DWSIM.OTS.ControlGateway.Tests
```

### Run with Verbose Output

```bash
dotnet test ots-dwsim/tests --verbosity detailed
```

### Run Specific Test Class or Method

```bash
# Run all tests in a class
dotnet test --filter "FullyQualifiedName~SessionManagerTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~SessionManagerTests.CreateSessionAsync_WithValidFlowsheet_ShouldReturnSessionId"
```

## Code Coverage

### Generate Coverage Report Locally

```bash
# Run tests with coverage collection
dotnet test ots-dwsim/tests \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

# Install ReportGenerator (one-time setup)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML coverage report
reportgenerator \
  -reports:"./coverage/**/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:"Html;MarkdownSummary"

# Open report in browser (Linux)
xdg-open ./coverage/report/index.html

# Or on macOS
open ./coverage/report/index.html

# Or on Windows
start ./coverage/report/index.html
```

### View Coverage Summary

```bash
# Generate Markdown summary
reportgenerator \
  -reports:"./coverage/**/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:"MarkdownSummary"

cat ./coverage/report/Summary.md
```

### Coverage Targets

| Component | Current Target | Long-term Goal |
|-----------|---------------|----------------|
| OTS SimulationHost | 70% | 80%+ |
| OTS ControlGateway | 70% | 80%+ |
| DWSIM Core Libraries | 50% | 60%+ |
| DWSIM Unit Operations | 50% | 70%+ |

## CI/CD Integration

Tests run automatically on GitHub Actions for:
- All pushes to `main`, `develop`, and `claude/**` branches
- All pull requests to `main` and `develop`

The CI pipeline:
1. Builds all projects
2. Runs all tests with coverage collection
3. Generates coverage reports
4. Uploads test results and coverage as artifacts
5. Displays coverage summary in workflow summary
6. Warns if coverage falls below 50% threshold

**View CI results:**
- Go to the [Actions tab](../../.github/workflows/ci.yml)
- Select a workflow run
- Check the "Code Coverage Analysis" job
- Download coverage report from artifacts

## Writing Tests

### Test Structure

Follow the **Arrange-Act-Assert** (AAA) pattern:

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and mocks
    var mockRepository = new Mock<IFlowsheetRepository>();
    mockRepository.Setup(x => x.ResolveFlowsheetPathAsync("test"))
        .ReturnsAsync("/path/to/flowsheet.dwxmz");

    var sessionManager = new SessionManager(mockRepository.Object, mockLogger.Object);

    // Act - Execute the method under test
    var result = await sessionManager.CreateSessionAsync(request);

    // Assert - Verify the results
    result.Should().NotBeNull();
    result.SessionId.Should().NotBeNullOrEmpty();
}
```

### Naming Conventions

**Test methods:** `MethodName_Scenario_ExpectedBehavior`

Examples:
- `CreateSessionAsync_WithValidFlowsheet_ShouldReturnSessionId`
- `ReadTagAsync_WithNonExistentSession_ShouldThrowKeyNotFoundException`
- `UpdateTagValueAsync_WhenNotRunning_ShouldNotThrow`

**Test classes:** `ClassNameUnderTestTests`

Examples:
- `SessionManagerTests`
- `OpcUaServerTests`

### Using Mocks

```csharp
// Create a mock
var mockLogger = new Mock<ILogger<SessionManager>>();

// Setup method behavior
mockRepository
    .Setup(x => x.ResolveFlowsheetPathAsync("flowsheet"))
    .ReturnsAsync("/path/to/flowsheet.dwxmz");

// Verify method was called
mockLogger.Verify(
    x => x.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created session")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

### Using FluentAssertions

```csharp
// Value assertions
result.Should().NotBeNull();
result.SessionId.Should().NotBeNullOrEmpty();
result.Status.Should().Be(SessionStatus.Created);

// Collection assertions
sessions.Should().HaveCount(3);
sessions.Should().Contain(s => s.SessionName == "Test Session");

// Date/time assertions
result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

// Exception assertions
var act = async () => await sessionManager.GetSessionAsync("invalid");
await act.Should().ThrowAsync<KeyNotFoundException>();
```

### Test Categories

Use `[Trait]` to categorize tests:

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task FastUnitTest() { }

[Fact]
[Trait("Category", "Integration")]
public async Task SlowerIntegrationTest() { }
```

Run tests by category:
```bash
dotnet test --filter "Category=Unit"
```

## Best Practices

### DO

✅ Write tests for all public APIs and business logic
✅ Test both happy paths and error cases
✅ Use descriptive test names that explain the scenario
✅ Keep tests focused - one assertion concept per test
✅ Use mocks to isolate the code under test
✅ Clean up resources (sessions, files) in tests
✅ Use `async`/`await` properly for async code
✅ Test edge cases and boundary conditions

### DON'T

❌ Don't test private methods directly - test through public APIs
❌ Don't write tests that depend on other tests
❌ Don't use hard-coded sleeps - use proper async patterns
❌ Don't test framework code (ASP.NET, xUnit, etc.)
❌ Don't ignore failing tests - fix or remove them
❌ Don't commit commented-out tests

## Troubleshooting

### Tests Fail Due to Missing DWSIM Dependencies

Some tests may fail if DWSIM automation components cannot be loaded. This is expected in the current skeleton implementation. The tests are structured to pass when the full DWSIM integration is completed.

**Current workaround:** Tests mock the DWSIM interfaces and test the OTS logic independently.

### Coverage Report Not Generating

Ensure you have ReportGenerator installed:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Update to latest version:
```bash
dotnet tool update -g dotnet-reportgenerator-globaltool
```

### Tests Run Slowly

- Use `[Trait("Category", "Slow")]` to mark slow tests
- Run fast tests only: `dotnet test --filter "Category!=Slow"`
- Consider using `Task.Run` for CPU-bound test setup
- Check for accidental `Thread.Sleep()` calls

### Mock Setup Not Working

Ensure exact parameter matching:
```csharp
// This will only match exact string "test"
mock.Setup(x => x.Method("test")).ReturnsAsync(result);

// This will match any string
mock.Setup(x => x.Method(It.IsAny<string>())).ReturnsAsync(result);
```

## Future Enhancements

### Planned Test Additions

1. **Integration Tests**
   - End-to-end API tests using WebApplicationFactory
   - Real OPC UA client connection tests
   - Multi-session scenario tests

2. **Performance Tests**
   - Benchmark session creation time
   - Tag read/write throughput
   - Memory usage under load
   - Concurrent session handling

3. **DWSIM Core Tests**
   - Thermodynamics property calculations
   - Flash algorithm accuracy
   - Unit operation convergence
   - Flowsheet solver tests

4. **Contract Tests**
   - OpenAPI/Swagger schema validation
   - OPC UA information model compliance
   - Scenario file validation

### Testing Tools to Add

- **BenchmarkDotNet** - Performance benchmarking
- **TestContainers** - Docker-based integration tests
- **Bogus** - Fake data generation
- **Verify** - Snapshot testing
- **Stryker.NET** - Mutation testing

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [FluentAssertions Documentation](https://fluentassertions.com/introduction)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## Contributing

When adding new features:
1. Write tests first (TDD approach recommended)
2. Aim for >80% coverage on new code
3. Ensure all tests pass locally before pushing
4. Update this README if adding new test categories or tools

## License

Tests follow the same license as the main DWSIM project.
