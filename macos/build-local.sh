#!/bin/bash
# Compile the reviewed source on your own Mac. No quarantine removal or Gatekeeper changes.
set -euo pipefail
cd "$(dirname "$0")"
if ! xcrun --find swift >/dev/null 2>&1; then
  echo "Install Apple's Xcode Command Line Tools first: xcode-select --install"
  echo "After installation, run this script again."
  exit 1
fi
bash build.sh
echo
echo "Built locally: $(pwd)/dist/NotchPrompter.app"
echo "You can now open that app in Finder. Copy it to Applications if you want to keep it there."
echo "This is a local development build, not an Apple-notarized release."
