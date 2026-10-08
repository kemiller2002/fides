#!/bin/sh
# Builds the Fides client into a trimmed browser-wasm application and runs its
# end-to-end flow inside the .NET WebAssembly runtime under Node. Needs the
# wasm-tools workload (dotnet workload install wasm-tools) and Node 18+.
set -eu
root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
out="${1:-$root/dist/wasm}"
rm -rf "$out"
dotnet publish "$root/tests/Fides.Client.Wasm/Fides.Client.Wasm.fsproj" -c Release -o "$out"
cd "$out/wwwroot"
result="$(node main.mjs)"
printf '%s\n' "$result"
case "$result" in
  *FIDES-WASM-OK*) exit 0 ;;
  *) exit 1 ;;
esac
