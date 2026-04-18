#!/usr/bin/env bash
# Usage: ./scripts/publish.sh [--runtime linux-arm64] [--configuration Release] [--version 1.0.0]
set -euo pipefail

RUNTIME="linux-arm64"
CONFIGURATION="Release"
VERSION=""
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT="$REPO_ROOT/src/MqttDashboard.WebApp/MqttDashboard.WebApp/MqttDashboard.WebApp.csproj"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --runtime) RUNTIME="$2"; shift 2;;
        --configuration) CONFIGURATION="$2"; shift 2;;
        --version) VERSION="$2"; shift 2;;
        *) echo "Unknown arg: $1"; exit 1;;
    esac
done

OUTPUT_DIR="$REPO_ROOT/artifacts/$RUNTIME"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"

echo "Publishing MqttDashboard for $RUNTIME..."

PUBLISH_ARGS=(
    publish "$PROJECT"
    -c "$CONFIGURATION"
    -r "$RUNTIME"
    --self-contained true
    -p:PublishSingleFile=true
    -p:IncludeNativeLibrariesForSelfExtract=true
    -o "$OUTPUT_DIR"
)
[[ -n "$VERSION" ]] && PUBLISH_ARGS+=(-p:Version="$VERSION")

dotnet "${PUBLISH_ARGS[@]}"

# Write sample appsettings
cat > "$OUTPUT_DIR/appsettings.sample.json" <<'EOF'
{
  "MqttSettings": {
    "Broker": "your-mqtt-broker-host",
    "Port": 1883,
    "Username": "",
    "Password": ""
  },
  "DiagramStorage": {
    "DataDirectory": "./data"
  },
  "AllowedPathBase": "",
  "Auth": {
    "AdminPasswordHash": ""
  }
}
EOF

# Create zip
ZIP_NAME="mqttdashboard-$RUNTIME.zip"
ZIP_PATH="$ARTIFACTS_DIR/$ZIP_NAME"
mkdir -p "$ARTIFACTS_DIR"
cd "$OUTPUT_DIR" && zip -r "$ZIP_PATH" . && cd -
echo "Created: $ZIP_PATH"
