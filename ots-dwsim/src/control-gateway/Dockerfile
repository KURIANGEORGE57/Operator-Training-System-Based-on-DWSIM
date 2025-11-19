# DWSIM OTS Control Gateway (OPC UA Server) Dockerfile
# Multi-stage build for production deployment

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY ots-dwsim/src/control-gateway/*.csproj ots-dwsim/src/control-gateway/

# Restore dependencies
WORKDIR /src/ots-dwsim/src/control-gateway
RUN dotnet restore

# Copy source code
WORKDIR /src
COPY ots-dwsim/src/control-gateway/ ots-dwsim/src/control-gateway/

# Build
WORKDIR /src/ots-dwsim/src/control-gateway
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create directories
RUN mkdir -p /app/logs /app/config && chmod -R 777 /app/logs

# Expose OPC UA port
EXPOSE 4840

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production

# Run application
ENTRYPOINT ["dotnet", "DWSIM.OTS.ControlGateway.dll"]
