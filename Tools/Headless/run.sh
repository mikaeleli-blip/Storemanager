#!/bin/sh
# Headless balance run: compiles the game against a fake UnityEngine and simulates N days.
# Usage: Tools/Headless/run.sh [days] [scripted|idle] [seed]
# Needs mono (apt install mono-mcs mono-runtime).
set -e
cd "$(dirname "$0")"
mcs -nologo -out:/tmp/storemanager-sim.exe -r:System.Core.dll -nowarn:108,414 FakeUnity.cs Runner.cs $(find ../../Assets -name '*.cs' -not -path '*/Editor/*')
mono /tmp/storemanager-sim.exe "${1:-8}" "${2:-scripted}" "${3:-7}" "${4:-normal}"
