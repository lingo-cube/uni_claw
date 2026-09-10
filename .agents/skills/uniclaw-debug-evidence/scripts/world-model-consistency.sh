#!/usr/bin/env bash
# world-model-consistency.sh — the single agent-runnable World Model
# consistency command (WMP-002 evidence route, DBG-001).
#
# Contract:
#   - Locates the repository root from its own path (caller cwd irrelevant).
#   - Runs the WMP-002 deterministic World Model test set (canonical oracle +
#     materialization probe + performance + benchmark probe).
#   - Exit 0 = all green (optimized paths item-for-item equal to the naive
#     canonical scans on every covered fixture); non-zero = at least one
#     mismatch. Test output is passed through untouched — on failure look for
#     the stable `WMP-DIVERGENCE schema=wmp-canonical-oracle/1 ...` lines
#     (machine-parseable first-divergence reports).
#   - No network diagnostics, no human interaction, no host session state,
#     no reliance on uncommitted files; does not modify product state
#     (standard bin/obj build outputs only). Note: `dotnet restore` may
#     attempt a NuGet vulnerability-index check (NU1900 warning) and still
#     succeeds offline — package restore consumes declared packages only.
set -uo pipefail
# No `-e`: the single exec below must pass the test exit code through
# verbatim; every earlier command has its own explicit error path.

# The --filter expression below IS the WMP-002 deterministic World Model set;
# if that set changes, update changes/WMP-002/state.md D1 in the same change.

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
while [ "$root" != "/" ]; do
    if [ -f "$root/AGENTS.md" ] && [ -f "$root/UniClaw.Kernel.slnx" ]; then
        break
    fi
    root="$(dirname -- "$root")"
done
if [ ! -f "$root/UniClaw.Kernel.slnx" ]; then
    echo "world-model-consistency: repository root not found (walked up from $(dirname -- "${BASH_SOURCE[0]}")); need AGENTS.md + UniClaw.Kernel.slnx" >&2
    exit 2
fi

cd "$root" || exit 2
# detailed verbosity is REQUIRED for diagnosis: the deterministic probe
# evidence lines (WMP-MAT / WMP-STAGE / WMP-ORACLE / WMP-PROBE) are
# ITestOutputHelper output and are invisible at default verbosity.
# WMP-DIVERGENCE failure lines are assertion messages and surface either way.
exec dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --nologo \
    --logger "console;verbosity=detailed" \
    --filter "FullyQualifiedName~WorldModelCanonicalOracleTests|FullyQualifiedName~WorldModelMaterializationProbeTests|FullyQualifiedName~WorldModelPerformanceTests|FullyQualifiedName~WorldModelBenchmarkProbeTests"
