# Moedim.Mcp.Fabric Docker Image
# Multi-stage build for optimized container size

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY Directory.Build.props Directory.Packages.props ./
COPY Moedim.Mcp.Fabric.sln ./
COPY src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj src/Moedim.Mcp.Fabric/
COPY test/Moedim.Mcp.Fabric.Tests/Moedim.Mcp.Fabric.Tests.csproj test/Moedim.Mcp.Fabric.Tests/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ src/
COPY test/ test/

# Build and publish
RUN dotnet publish src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Use the built-in non-root user (available in .NET 8+ images)
USER $APP_UID

# Expose HTTP port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:5000/health || exit 1

# Entry point - HTTP mode with HTTPS redirect disabled (TLS terminates at ingress)
ENTRYPOINT ["dotnet", "Moedim.Mcp.Fabric.dll", "--http", "--disable-https-redirect"]
