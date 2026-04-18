#!/usr/bin/env bash
# Build (and optionally push) the Docker image locally
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
IMAGE="ghcr.io/robinrottier/mqttdashboard"
TAG="${1:-local}"
PUSH="${2:-false}"

cd "$REPO_ROOT"
echo "Building Docker image $IMAGE:$TAG..."
docker build \
    -t "$IMAGE:$TAG" \
    -f MqttDashboard.WebApp/MqttDashboard.WebApp/Dockerfile \
    .

if [[ "$PUSH" == "true" ]]; then
    echo "Pushing $IMAGE:$TAG..."
    docker push "$IMAGE:$TAG"
fi
echo "Done."
