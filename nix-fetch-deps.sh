#!/usr/bin/env bash
set -euo pipefail

rm -rf packageDir
dotnet restore --packages=packageDir ./src/build.proj
nuget-to-json packageDir > src/deps.json
rm -rf packageDir
