# Update Agent

This folder contains a small host-side update agent for on-demand updates of the Dockerized application.

Goal
- Allow the running web app to request an update (pull a new image and restart a service) without giving the app direct control of Docker.

Components
- `update-agent.sh` — Bash script that runs `docker compose pull` + `docker compose up -d` for a named service, or runs `containrrr/watchtower --run-once` for a named container. It can be invoked directly or through the agent.
- `agent.py` — Minimal Python Flask HTTP wrapper that listens on `127.0.0.1` and exposes `POST /update`. It validates the `X-Update-Token` header against the `UPDATE_AGENT_TOKEN` environment variable and invokes `update-agent.sh`.
- `agent.service` — Example `systemd` unit to run the agent on Linux (copy to `/etc/systemd/system/agent.service` and edit `WorkingDirectory` and `Environment` as needed).

Security
- The agent binds to `127.0.0.1` only. Do not expose it to the internet.
- Requests must include the `X-Update-Token` header matching the `UPDATE_AGENT_TOKEN` env var to be accepted.
- The Flask wrapper executes `update-agent.sh` located in the same directory. Keep the directory and token secure and run the service under a dedicated user if possible.

Usage

1. Copy files to the host machine and place them in e.g. `/opt/mqttdashboard/tools/update-agent`.
   Add some permissions:
       chmod +x *.sh

2. Set the secret token in the environment or systemd unit:
   `export UPDATE_AGENT_TOKEN=your-secret`

3. Start the agent (for testing):
   `python3 agent.py --host 127.0.0.1 --port 8080`

4. Example request (server-side call, not from untrusted client):

   POST http://127.0.0.1:8080/update
   Headers:
     X-Update-Token: <your-secret>
   Body (JSON):
     { "service": "mqttdashboard_webapp", "composeFile": "docker-compose.yml" }

   Or to run watchtower once:
     { "watchtowerContainer": "my-watchtower-container" }

Systemd
- cd /opt/mqttdashboard/tools/update-agent
- Edit `agent.service` and set `WorkingDirectory` to the agent folder and `Environment` to your token.
- Enable and start:
  sudo systemctl daemon-reload
  sudo systemctl enable --now ./agent.service

Notes
- This approach avoids granting Docker socket access to your application container. The app should call your server-side endpoint which in turn invokes this agent on the host.
- The update process runs `docker compose pull` followed by `docker compose up -d` for the requested service. Ensure your compose file and service name match what you pass in the request.

Docker Compose (optional)
-------------------------

You can run the update agent as a container via the project's `docker-compose.override.yml`. This is convenient for local development but has security implications described below.

Summary
- The compose override adds an `update-agent` service and injects `UPDATE_AGENT_URL`/`UPDATE_AGENT_TOKEN` into the `mqttdashboard` service.
- From inside containers use: `POST http://update-agent:8080/update`.
- From the host use: `POST http://127.0.0.1:8080/update`.
- All requests must include header `X-Update-Token: <token>` where `<token>` equals the `UPDATE_AGENT_TOKEN` environment variable.

How to enable
1. Add a secret token to a `.env` file at repo root (git-ignored):

   UPDATE_AGENT_TOKEN=super-secret-token

2. Start compose (from repo root):

   docker compose up --build -d

3. The app (or your server-side code) can then trigger an update by POSTing JSON to the update endpoint.

Example request (server-side only)

POST http://update-agent:8080/update
Headers:
  X-Update-Token: super-secret-token
Body (JSON):
  { "service": "mqttdashboard", "composeFile": "docker-compose.yml" }

Security notes
- By default the override mounts `/var/run/docker.sock` into the `update-agent` container. This allows the agent to run `docker compose pull` / `docker compose up -d` but grants full Docker control to that container. Use only in trusted environments.
- If you do NOT want the agent to control Docker, remove the socket mount from `docker-compose.override.yml`. The agent will still expose an endpoint but will be unable to perform Docker operations.
- Keep `UPDATE_AGENT_TOKEN` secret. Do not commit it to the repository.
- The compose override binds the agent port to the host loopback (`127.0.0.1:8080`) so it is not publicly reachable. Containers on the same compose network can reach the agent by service name without exposing the port externally.
- Prefer the host-run agent approach (not in compose) if you want to avoid mounting the Docker socket into a container.

Allowing container access (host agent)
------------------------------------

If you run the update agent on the host (systemd or direct python) but want containers to call it via the host gateway, follow these steps:

1) Bind the agent to a non-loopback interface (for example `0.0.0.0`) so it listens on the host gateway:

   `python3 agent.py --host 0.0.0.0 --port 8081`

2) Opt-in to remote callers by setting `UPDATE_AGENT_ALLOW_REMOTE=true` in the environment. This enables the agent to accept non-loopback requests.

3) Restrict allowed remote source IPs using `UPDATE_AGENT_ALLOWED_CIDRS` (comma-separated CIDRs). The default covers common private networks including Docker bridge ranges:

   `127.0.0.1/32,::1/128,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16`

   Example (only allow typical Docker bridge range and loopback):

   `UPDATE_AGENT_ALLOWED_CIDRS=127.0.0.1/32,172.17.0.0/16`

4) From your container, use `host.docker.internal` (Docker Desktop) or add an extra_hosts entry on Linux to reach the host gateway:

   docker-compose snippet (app service):

   ```yaml
   environment:
     - UpdateAgent__Url=http://host.docker.internal:8081/update
   extra_hosts:
     - "host.docker.internal:host-gateway"
   ```

Example systemd unit environment lines (host-run agent)

In your `agent.service` add the environment variables to the unit file (or export them before running):

```
[Service]
WorkingDirectory=/opt/mqttdashboard/tools/update-agent
Environment=UPDATE_AGENT_TOKEN=super-secret-token
Environment=UPDATE_AGENT_ALLOW_REMOTE=true
Environment=UPDATE_AGENT_ALLOWED_CIDRS=127.0.0.1/32,172.17.0.0/16
ExecStart=/usr/bin/python3 agent.py --host 0.0.0.0 --port 8081
```

Security reminder: enabling remote access reduces the protection afforded by binding to loopback. Keep `UPDATE_AGENT_TOKEN` set and restrict `UPDATE_AGENT_ALLOWED_CIDRS` to the narrowest set of networks that need access (for example only your Docker bridge subnet).
