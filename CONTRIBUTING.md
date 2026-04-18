# Contributing to MqttDashboard

Thank you for considering a contribution!

## Building locally

Requirements: .NET 10 SDK, Docker (optional).

```bash
# Install the WASM workload (first time only)
dotnet workload install wasm-tools

# Build the solution
dotnet build MqttDashboard.slnx

# Run tests
dotnet test MqttDashboard.slnx
```

Run with Docker Compose (builds from source):

```bash
docker compose up --build
```

Then open <http://localhost:8080>.

## Project layout

```
src/
  MqttDashboard.Client/       Razor class library — shared pages, components, services
  MqttDashboard.Server/       Server-side services — MQTT client, SignalR hub, API controllers
  MqttDashboard.WebApp/       Blazor Web App host (InteractiveAuto — WASM + server-side)
  MqttDashboard.WebAppServerOnly/  Alternate host — pure Blazor Server (no WASM download)
tests/
  MqttDashboard.Client.Tests/
  MqttDashboard.Server.Tests/
```

## Submitting a pull request

1. Fork the repository and create a feature branch from `main`.
2. Make your changes; add or update tests where appropriate.
3. Run `dotnet test MqttDashboard.slnx` and confirm everything passes.
4. Open a PR with a clear description of what changed and why.

## Reporting bugs

Open a GitHub Issue with steps to reproduce, expected behaviour, and actual behaviour.
Include the app version (shown in the About dialog) and your deployment method (Docker / binary / Home Assistant).
