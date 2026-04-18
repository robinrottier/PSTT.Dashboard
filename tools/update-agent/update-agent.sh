#!/bin/bash
set -euo pipefail

SERVICE="${1:-}"
COMPOSE_FILE="${2:-}"
WATCHTOWER_CONTAINER="${3:-}"
WORKDIR="${4:-.}"

# If watchtower container name supplied, run watchtower once
if [[ -n "$WATCHTOWER_CONTAINER" ]]; then
  echo "Running watchtower --run-once for $WATCHTOWER_CONTAINER"
  docker run --rm --name watchtower -v /var/run/docker.sock:/var/run/docker.sock nickfedor/watchtower --run-once "$WATCHTOWER_CONTAINER"
  exit $?
fi

if [[ -z "$SERVICE" ]]; then
  echo '{"error":"service required"}' >&2
  exit 2
fi

# Change to workdir if provided
cd "$WORKDIR" || true

if [[ -n "$COMPOSE_FILE" ]]; then
  echo "Using compose file: $COMPOSE_FILE"
  docker compose -f "$COMPOSE_FILE" pull "$SERVICE"
  docker compose -f "$COMPOSE_FILE" up -d "$SERVICE"
else
  docker compose pull "$SERVICE"
  docker compose up -d "$SERVICE"
fi

echo '{"status":"ok"}'
