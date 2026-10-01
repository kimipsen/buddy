#!/usr/bin/env bash
# Runs Stryker.NET (the dotnet-stryker local tool, args passed through) and fails loudly when it
# didn't actually test any mutants. See docs/backend/analysis/mutation-testing-strategy.md.
#
# Two workarounds live here:
# 1. Roslyn overlay. dotnet-stryker 5.0.0 bundles Roslyn 5.9, which can't compile this codebase's
#    C# preview features (the `union` Result<T>), so every mutated build fails. The script runs
#    a copy of the tool with the SDK's own Microsoft.CodeAnalysis(.CSharp).dll swapped in.
#    Set STRYKER_SDK_ROSLYN=0 to run the tool as shipped (e.g. once Stryker bundles a new enough
#    Roslyn).
# 2. Log guard. Stryker.NET can exit 0 when test discovery found no tests, the initial test run
#    failed, or the mutated build failed (it even prints "final mutation score is 0.00 %"), so
#    its exit code alone can't be trusted: the log is checked as well.
#
# Usage (from src/backend/buddy.IntegrationTests): ./run-stryker.sh [stryker args...]
# The full log is kept at $STRYKER_LOG (default StrykerOutput/stryker-run.log).
set -uo pipefail

log="${STRYKER_LOG:-StrykerOutput/stryker-run.log}"
mkdir -p "$(dirname "$log")"

fail() {
  echo "" >&2
  echo "ERROR: Stryker.NET did not test any mutants: $1" >&2
  echo "Full log: $log. See docs/backend/analysis/mutation-testing-strategy.md." >&2
  if [[ -n "${GITHUB_ACTIONS:-}" ]]; then echo "::error title=Stryker.NET::$1" >&2; fi
  exit 1
}

stryker=(dotnet stryker)
if [[ "${STRYKER_SDK_ROSLYN:-1}" != 0 ]]; then
  version=$(sed -n '/"dotnet-stryker"/,/}/s/.*"version": *"\([^"]*\)".*/\1/p' ../dotnet-tools.json)
  tool_dir=$(ls -d "${NUGET_PACKAGES:-$HOME/.nuget/packages}/dotnet-stryker/$version"/tools/net*/any 2>/dev/null | tail -1)
  sdk_version=$(dotnet --version)
  sdk_root=$(dotnet --list-sdks | sed -n "s/^$sdk_version \[\(.*\)\]$/\1/p")
  roslyn="$sdk_root/$sdk_version/Roslyn/bincore"
  [[ -n "$tool_dir" ]] || fail "dotnet-stryker $version is not restored (run dotnet tool restore in src/backend)"
  [[ -f "$roslyn/Microsoft.CodeAnalysis.CSharp.dll" ]] || fail "SDK Roslyn not found at $roslyn"
  copy="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/buddy-stryker-$version-sdk-$sdk_version"
  if [[ ! -f "$copy/.ready" ]]; then
    rm -rf "$copy" && mkdir -p "$copy" && cp -r "$tool_dir/." "$copy/" &&
      cp "$roslyn/Microsoft.CodeAnalysis.dll" "$roslyn/Microsoft.CodeAnalysis.CSharp.dll" "$copy/" &&
      touch "$copy/.ready" || fail "could not prepare the Roslyn-overlaid tool copy at $copy"
  fi
  echo "Running dotnet-stryker $version with the SDK $sdk_version Roslyn ($copy)"
  stryker=(dotnet "$copy/Stryker.CLI.dll")
fi

"${stryker[@]}" "$@" 2>&1 | tee "$log"
status=${PIPESTATUS[0]}

# Known failure lines: 4.x test discovery, initial test run failure, 5.x compile crash/rollback
# failure ("Failed to restore the project to a buildable state" + FTL), unhandled exceptions.
failures='Test discovery has been aborted|did not report any test|No test result reported|Number of tests found: 0 |failed to mutate your project|initial test ?run (failed|has more than)|Failed to restore the project to a buildable state|can not proceed further|\] FTL\]|FTL\] |Unhandled exception'
if grep -Eiq "$failures" "$log"; then
  fail "$(grep -Eim1 "$failures" "$log" | sed 's/^\[[^]]*\] //' | cut -c1-200)"
fi
[[ "$status" -eq 0 ]] || fail "Stryker exited with status $status"
# A run that tested mutants logs "<N> total mutants will be tested" (N > 0) and a final score.
grep -Eq '\b[1-9][0-9]* +total mutants will be tested' "$log" || fail "no mutants were tested (none in scope, or the run stopped early)"
grep -q 'The final mutation score is' "$log" || fail "no final mutation score in the log"
