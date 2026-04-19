# Contributing to PSTT.Dashboard

Thank you for considering a contribution!

## Building locally

Requirements: .NET 10 SDK, Docker (optional).

```bash
# Clone with submodules (PSTT library is a git submodule)
git clone --recurse-submodules https://github.com/robinrottier/PSTT.Dashboard
# or, if already cloned:
git submodule update --init --recursive

# Install the WASM workload (first time only)
dotnet workload install wasm-tools

# Build the solution
dotnet build PSTT.Dashboard.slnx

# Run tests
dotnet test PSTT.Dashboard.slnx
```

Run with Docker Compose (builds from source):

```bash
docker compose up --build
```

Then open <http://localhost:8080>.

## Project layout

```
src/
  PSTT.Dashboard.Client/       Razor class library — shared pages, components, services
  PSTT.Dashboard.Server/       Server-side services — MQTT client, SignalR hub, API controllers
  PSTT.Dashboard.WebApp/       Blazor Web App host (InteractiveAuto — WASM + server-side)
  PSTT.Dashboard.WebAppServerOnly/  Alternate host — pure Blazor Server (no WASM download)
libs/
  PSTT/                        PSTT data transport library (git submodule)
tests/
  PSTT.Dashboard.Client.Tests/
  PSTT.Dashboard.Server.Tests/
  PSTT.Dashboard.IntegrationTests/
```

## Submitting a pull request

1. Fork the repository and create a feature branch from `main`.
2. Make your changes; add or update tests where appropriate.
3. Run `dotnet test PSTT.Dashboard.slnx` and confirm everything passes.
4. Open a PR with a clear description of what changed and why.

## Reporting bugs

Open a GitHub Issue with steps to reproduce, expected behaviour, and actual behaviour.
Include the app version (shown in the About dialog) and your deployment method (Docker / binary / Home Assistant).
