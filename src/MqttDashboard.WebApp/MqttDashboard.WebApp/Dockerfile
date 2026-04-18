# Build stage — needs full SDK to compile all projects
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Python (required by the wasm-tools workload for AOT compilation)
RUN apt-get update && apt-get install -y python3 && ln -sf /usr/bin/python3 /usr/bin/python && rm -rf /var/lib/apt/lists/*

# Install the WASM workload required for Blazor WebAssembly builds
RUN dotnet workload install wasm-tools

# Copy all project files (for layer caching on package restore)
COPY src/Directory.Build.props      src/
COPY src/MqttDashboard.Client/MqttDashboard.Client.csproj                                                           src/MqttDashboard.Client/
COPY src/MqttDashboard.Data/MqttDashboard.Data.csproj                                                               src/MqttDashboard.Data/
COPY src/MqttDashboard.Mqtt/MqttDashboard.Mqtt.csproj                                                               src/MqttDashboard.Mqtt/
COPY src/MqttDashboard.Server/MqttDashboard.Server.csproj                                                           src/MqttDashboard.Server/
COPY src/MqttDashboard.SourceGenerators/MqttDashboard.SourceGenerators.csproj                                       src/MqttDashboard.SourceGenerators/
COPY src/MqttDashboard.WebApp/MqttDashboard.WebApp.Client/MqttDashboard.WebApp.Client.csproj                        src/MqttDashboard.WebApp/MqttDashboard.WebApp.Client/
COPY src/MqttDashboard.WebApp/MqttDashboard.WebApp/MqttDashboard.WebApp.csproj                                      src/MqttDashboard.WebApp/MqttDashboard.WebApp/

# Restore dependencies (after workload install so WASM targets are available)
RUN dotnet restore src/MqttDashboard.WebApp/MqttDashboard.WebApp/MqttDashboard.WebApp.csproj

# Copy the rest of the source and .git so MinVer can resolve the version from tags
COPY .git/ .git/
COPY src/MqttDashboard.Client/      src/MqttDashboard.Client/
COPY src/MqttDashboard.Data/        src/MqttDashboard.Data/
COPY src/MqttDashboard.Mqtt/        src/MqttDashboard.Mqtt/
COPY src/MqttDashboard.Server/      src/MqttDashboard.Server/
COPY src/MqttDashboard.SourceGenerators/ src/MqttDashboard.SourceGenerators/
COPY src/MqttDashboard.WebApp/      src/MqttDashboard.WebApp/

# Publish the server project (this also builds & includes the WASM client).
# MinVer reads .git to resolve the version from the nearest tag automatically.
RUN dotnet publish src/MqttDashboard.WebApp/MqttDashboard.WebApp/MqttDashboard.WebApp.csproj \
    -c Release \
    -o /app/publish

# Runtime stage — lightweight ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create data directory for dashboard persistence
RUN mkdir -p /app/data

COPY --from=build /app/publish .

EXPOSE 8080 8081

# 8080 — read-only public port (configured via ReadOnlyPorts in appsettings)
# 8081 — read-write admin port
ENV ASPNETCORE_URLS=http://+:8080;http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MqttDashboard.WebApp.dll"]
