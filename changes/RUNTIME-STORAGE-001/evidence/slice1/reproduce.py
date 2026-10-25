#!/usr/bin/env python3
"""Fresh output only. Existing three Kernel failures stay explicit."""
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[4]
HOST = "|".join("FullyQualifiedName~" + n for n in (
    "ControlTraversalHistoryFileTests", "TraversalHistoryFileTests", "RunWorldRecoveryConsistencyTests",
    "RunEvidenceRecoveryConsistencyTests", "RunRecoveryConsistencyTests", "RunStateRecoveryTests", "RunResourceRecovery",
    "RunResourceAccountingTests", "EvidenceRecoveryTests", "WorldHistoryRecoveryTests", "OriginalWorldStoragePreserves",
    "RunStateCrashProcessTests", "RunResourceCrashProcessTests", "RunRecoveryStorageAbstractionTests", "RuntimePlanHistoryFileTests",
    "EvidenceBatchStorageTests"))
if __name__ == "__main__":
    output = Path(tempfile.mkdtemp(prefix="uniclaw-async-evidence-")); print(output, flush=True)
    env = dict(os.environ, EVIDENCE_BATCH_EVIDENCE_DIR=str(output / "batch"), RUNTIME_PLAN_EVIDENCE_DIR=str(output / "plan"),
        CONTROL_TRAVERSAL_EVIDENCE_DIR=str(output / "control"))
    for project, filter_, name in (("UniClaw.Kernel.Tests", None, "kernel-all"), ("UniClaw.Host.Tests", HOST, "host"),
        ("UniClaw.Host.Dsh.Tests", "FullyQualifiedName~SettingsCoverageEventSimulationTests|FullyQualifiedName~RuntimeToolHostProcedureTests", "dsh")):
        command = ["dotnet", "test", str(ROOT / "tests" / project), "--no-restore", "--logger", f"trx;LogFileName={name}.trx", "--results-directory", str(output)]
        if filter_: command.extend(["--filter", filter_])
        with (output / (name + ".log")).open("w") as log:
            result = subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        print(name, "exit", result.returncode, flush=True)
        if result.returncode and project != "UniClaw.Kernel.Tests": raise SystemExit(result.returncode)
