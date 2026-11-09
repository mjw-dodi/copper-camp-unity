#!/bin/bash
# Double-click to play the Copper Camp web build. It serves the WebBuild folder on this Mac only
# (browsers won't run a Unity web build straight from a file) and opens it in your browser.
# Keep this window open while you play; close it to stop.
cd "$(dirname "$0")/WebBuild" || { echo "WebBuild folder not found next to this file."; exit 1; }
PORT=8777
if ! lsof -i :$PORT -sTCP:LISTEN >/dev/null 2>&1; then
  python3 -m http.server $PORT --bind 127.0.0.1 >/dev/null 2>&1 &
  SERVER=$!
  sleep 1
fi
open "http://localhost:$PORT/index.html"
echo "Copper Camp is running at http://localhost:$PORT/index.html"
echo "Your game saves in this browser, at this address. Close this window to stop."
[ -n "$SERVER" ] && wait $SERVER
