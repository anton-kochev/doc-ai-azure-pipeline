#!/bin/bash

API_URL="${API_URL:-http://localhost:5053}"
APP_ENV="${APP_ENV:-development}"
LOG_LEVEL="${LOG_LEVEL:-info}"
ENABLE_CONSOLE_LOGGING="${ENABLE_CONSOLE_LOGGING:-true}"

cat > "${1:-$(dirname "$0")/..}/config.json" <<EOF
{
  "apiUrl": "$API_URL",
  "environment": "$APP_ENV",
  "appName": "Document Processor Receiver",
  "version": "1.0.0",
  "features": {
    "fileUpload": true,
    "darkMode": true
  },
  "logging": {
    "level": "$LOG_LEVEL",
    "enableConsole": $ENABLE_CONSOLE_LOGGING
  }
}
EOF
