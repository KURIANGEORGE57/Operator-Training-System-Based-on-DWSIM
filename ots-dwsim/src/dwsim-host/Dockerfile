# DWSIM OTS Simulation Host Dockerfile
# Multi-stage build for production deployment

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy DWSIM dependencies
COPY DWSIM.Automation/ DWSIM.Automation/
COPY DWSIM.Interfaces/ DWSIM.Interfaces/
COPY DWSIM.SharedClasses/ DWSIM.SharedClasses/
COPY DWSIM.Thermodynamics/ DWSIM.Thermodynamics/
COPY DWSIM.FlowsheetBase/ DWSIM.FlowsheetBase/

# Copy OTS project files
COPY ots-dwsim/src/dwsim-host/DWSIM.OTS.SimulationHost.csproj ots-dwsim/src/dwsim-host/

# Restore dependencies
WORKDIR /src/ots-dwsim/src/dwsim-host
RUN dotnet restore "DWSIM.OTS.SimulationHost.csproj"

# Copy source code
WORKDIR /src
COPY ots-dwsim/src/dwsim-host/ ots-dwsim/src/dwsim-host/

# Build
WORKDIR /src/ots-dwsim/src/dwsim-host
RUN dotnet build "DWSIM.OTS.SimulationHost.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "DWSIM.OTS.SimulationHost.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Create directories
RUN mkdir -p /app/logs /app/data/flowsheets && chmod -R 777 /app/logs /app/data

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV FlowsheetPath=/app/data/flowsheets

# Run application
ENTRYPOINT ["dotnet", "DWSIM.OTS.SimulationHost.dll"]
