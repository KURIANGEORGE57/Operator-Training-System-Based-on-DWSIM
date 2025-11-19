# DWSIM OTS Orchestrator Dockerfile
# Multi-stage build for production deployment

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY src/orchestrator/DWSIM.OTS.Orchestrator.csproj src/orchestrator/
RUN dotnet restore "src/orchestrator/DWSIM.OTS.Orchestrator.csproj"

# Copy source code and build
COPY src/orchestrator/ src/orchestrator/
WORKDIR /src/src/orchestrator
RUN dotnet build "DWSIM.OTS.Orchestrator.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "DWSIM.OTS.Orchestrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chmod 777 /app/logs

# Expose port
EXPOSE 5001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5001/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

# Run application
ENTRYPOINT ["dotnet", "DWSIM.OTS.Orchestrator.dll"]
