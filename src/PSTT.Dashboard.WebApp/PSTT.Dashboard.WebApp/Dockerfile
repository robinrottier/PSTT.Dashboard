# Build stage — needs full SDK to compile all projects
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Python (required by the wasm-tools workload for AOT compilation)
RUN apt-get update && apt-get install -y python3 && ln -sf /usr/bin/python3 /usr/bin/python && rm -rf /var/lib/apt/lists/*

# Install the WASM workload required for Blazor WebAssembly builds
RUN dotnet workload install wasm-tools

# Copy all project files (for layer caching on package restore)
COPY src/Directory.Build.props      src/
COPY src/PSTT.Dashboard.Client/PSTT.Dashboard.Client.csproj                                                           src/PSTT.Dashboard.Client/
COPY src/PSTT.Dashboard.Server/PSTT.Dashboard.Server.csproj                                                           src/PSTT.Dashboard.Server/
COPY src/PSTT.Dashboard.SourceGenerators/PSTT.Dashboard.SourceGenerators.csproj                                       src/PSTT.Dashboard.SourceGenerators/
COPY src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp.Client/PSTT.Dashboard.WebApp.Client.csproj                        src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp.Client/
COPY src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp.csproj                                      src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/
COPY libs/PSTT/src/Directory.Build.props                                                                              libs/PSTT/src/
COPY libs/PSTT/src/PSTT.Data/PSTT.Data.csproj                                                                          libs/PSTT/src/PSTT.Data/
COPY libs/PSTT/src/PSTT.Mqtt/PSTT.Mqtt.csproj                                                                          libs/PSTT/src/PSTT.Mqtt/
COPY libs/PSTT/src/PSTT.Remote/PSTT.Remote.csproj                                                                      libs/PSTT/src/PSTT.Remote/
COPY libs/PSTT/src/PSTT.Remote.AspNetCore/PSTT.Remote.AspNetCore.csproj                                              libs/PSTT/src/PSTT.Remote.AspNetCore/
COPY libs/Blazor.Diagrams/Directory.Packages.props                                                                    libs/Blazor.Diagrams/
COPY libs/Blazor.Diagrams/src/Directory.Build.props                                                                   libs/Blazor.Diagrams/src/
COPY libs/Blazor.Diagrams/src/Blazor.Diagrams.Core/Blazor.Diagrams.Core.csproj                                        libs/Blazor.Diagrams/src/Blazor.Diagrams.Core/
COPY libs/Blazor.Diagrams/src/Blazor.Diagrams/Blazor.Diagrams.csproj                                                  libs/Blazor.Diagrams/src/Blazor.Diagrams/

# Restore dependencies (after workload install so WASM targets are available)
RUN dotnet restore src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp.csproj

# Copy the rest of the source and .git so MinVer can resolve the version from tags
COPY .git/ .git/
COPY src/PSTT.Dashboard.Client/      src/PSTT.Dashboard.Client/
COPY src/PSTT.Dashboard.Server/      src/PSTT.Dashboard.Server/
COPY src/PSTT.Dashboard.SourceGenerators/ src/PSTT.Dashboard.SourceGenerators/
COPY src/PSTT.Dashboard.WebApp/      src/PSTT.Dashboard.WebApp/
COPY libs/PSTT/src/                  libs/PSTT/src/
COPY libs/Blazor.Diagrams/Directory.Packages.props  libs/Blazor.Diagrams/
COPY libs/Blazor.Diagrams/src/       libs/Blazor.Diagrams/src/

# Publish the server project (this also builds & includes the WASM client).
# MinVer reads .git to resolve the version from the nearest tag automatically.
RUN dotnet publish src/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp/PSTT.Dashboard.WebApp.csproj \
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

ENTRYPOINT ["dotnet", "PSTT.Dashboard.WebApp.dll"]
