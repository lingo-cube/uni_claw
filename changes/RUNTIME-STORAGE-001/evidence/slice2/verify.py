#!/usr/bin/env python3
"""Verify the bounded runtime World async slice, not the remaining all-domain migration."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
read = lambda p: json.loads(p.read_text())
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
entry = read(HERE / "entry.json")
candidate = read(HERE / "candidate-inputs-v1.json")["files"]
assert subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip() == entry["base"]
assert set(candidate) == set(entry["files"])
for p, h in candidate.items(): assert sha(ROOT / p) == h == sha(HERE / "candidate-source-v1" / p), p
protected = {p: h for p, h in entry["inputs"].items() if p not in candidate}
for p, h in protected.items(): assert sha(ROOT / p) == h, p
current = {str(p.relative_to(ROOT)) for directory in ("src", "tests", "tools") for p in (ROOT / directory).rglob("*")
    if p.is_file() and p.suffix in (".cs", ".csproj", ".props", ".targets") and not {"bin", "obj"}.intersection(p.parts)}
current.add("global.json")
assert current == set(entry["inputs"]) | {p for p in candidate if Path(p).suffix == ".cs"}
prior = entry["priorSeal"]
assert sha(ROOT / prior["path"]) == prior["sha256"]
for p, h in read(ROOT / prior["path"])["files"].items(): assert sha(ROOT / p) == h, p
archive = read(HERE / "baseline-archive.json")
assert sha(ROOT / archive["path"]) == archive["sha256"]
with tarfile.open(ROOT / archive["path"], "r:gz") as source:
    for p, h in entry["inputs"].items(): assert hashlib.sha256(source.extractfile(p).read()).hexdigest() == h, p
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
def rows(name): return ET.parse(HERE / (name + ".trx")).findall(".//t:UnitTestResult", ns)
def failed(name): return {r.attrib["testName"] for r in rows(name) if r.attrib["outcome"] == "Failed"}
assert len(rows("kernel-final")) == 1208 and failed("kernel-final") == failed("baseline-kernel-three") and len(failed("kernel-final")) == 3
assert len(rows("host-final")) == 148 and failed("host-final") == failed("baseline-ownerid") and len(failed("host-final")) == 1
for name, passed in (("kernel-final", 1205), ("host-final", 147), ("dsh-final", 24)):
    assert sum(r.attrib["outcome"] == "Passed" for r in rows(name)) == passed
    assert all(r.attrib["outcome"] in ("Passed", "Failed") for r in rows(name))
assert len(rows("dsh-final")) == 24 and not failed("dsh-final")
for result, count in (("kernel-final", 10), ("host-final", 4)):
    cases = [r for r in rows(result) if "WorldAsyncStorageTests" in r.attrib["testName"]]
    assert len(cases) == count and all(r.attrib["outcome"] == "Passed" for r in cases)
registration = next(r for r in rows("kernel-final") if "SourceFiles_AreRegistered" in r.attrib["testName"])
assert "WorldModel.Async.cs" not in registration.find(".//t:Message", ns).text
assert "CS0246" in (HERE / "world-owner-red.log").read_text()
assert "TimeoutException" in (HERE / "kernel-wait-red.log").read_text()
assert "CS8417" in (HERE / "file-async-red.log").read_text()
for p, h in read(HERE / "test-assembly-fingerprints.json").items(): assert sha(ROOT / p) == h, p
binding = read(HERE / "candidate-verification-binding.json")
assert binding["sourceHashes"] == candidate
for p, h in binding["sha256"].items(): assert sha(ROOT / p) == h, p

directory = HERE / "host-final/kill"
ready, killed, expected = read(directory / "ready.json"), read(directory / "kill-observed.json"), read(directory / "expected.json")
assert ready["DriverPending"] and not ready["IsTerminal"] and ready["Belief"] is None and ready["PublishedWorldRevisions"] == 0
assert ready["OwnerPosition"]["Sequence"] == 2 and ready["SourceRows"] == 3 and ready["EvidenceAdmissions"] == 1
assert killed["Id"] == ready["ProcessId"] and killed["HasExited"] and killed["ExitCode"] != 0
readers = [read(directory / ("reader-" + str(n) + ".json")) for n in (0, 1)]
assert len({ready["ProcessId"], readers[0]["ProcessId"], readers[1]["ProcessId"]}) == 3
for reader in readers:
    assert reader["RunIsReadOnly"] and reader["EvidenceIsReadOnly"] and not reader["ContinuationSupported"] and not reader["IsTerminal"]
    assert reader["Operations"] == expected["Rows"] and len(reader["HistoricalRevisions"]) == 1 and reader["Position"]["Sequence"] == 3
for p, h in read(directory / "source-hashes.json").items(): assert sha(directory / p).upper() == h
assert json.loads((directory / "world.journal.jsonl").read_text().splitlines()[0])["schema"] == "uniclaw.world.history.v2"
size = read(HERE / "host-final/size/size.json")
assert size["observations"] == size["revisions"] == 128 and size["operations"] == 257
assert size["fullBytes"] == 7951934 and size["deltaBytes"] == 1107547

world = (ROOT / "src/UniClaw.Kernel/World/WorldModel.Async.cs").read_text()
assert world.count("RecordOperationAsync(") == 8
for forbidden in ("Task.Run", "FileStream", "UniClaw.Host"): assert forbidden not in world
owner = (ROOT / "src/UniClaw.Kernel/World/WorldModel.HistoryRecording.cs").read_text()
assert "await asyncJournal.AppendOperationAsync" in owner and "Interlocked.CompareExchange" in owner
native = (ROOT / "src/UniClaw.Host/Runtime/FileWorldJournal.cs").read_text()
commit = native[native.index("private async ValueTask CommitAsync"):native.index("private async ValueTask CommitHeadAsync")]
assert commit.index("_stream.WriteAsync") < commit.index("_stream.FlushAsync") < commit.index("await CommitHeadAsync") < commit.index("_written++")
assert "if (!_writeGate.Wait(0))" in native and "FileOptions.Asynchronous" in native and "world-operation-exceeds-byte-limit" in native
assert "await _writeGate.WaitAsync()" in native and "Task.Run" not in native
kernel = (ROOT / "src/UniClaw.Kernel/UniKernel.cs").read_text()
for name in ("JudgeRelevanceAsync", "ReconcileAsync", "RegisterContinuityDemandAsync", "ResolveConflictAsync", "RecordObservedNavigationAsync", "RecordObservedViewportTransitionAsync"):
    assert "await _world." + name in kernel, name
driver = (ROOT / "src/UniClaw.Kernel/Runtime/KernelRunDriver.cs").read_text()
assert driver.count("await _kernel.ResolveAuthorityConflictsAsync") == 2 and "await _kernel.VerifyPostActionEffectAsync" in driver
for name in ("src/UniClaw.Host/HostRunner.cs", "src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs"):
    text = (ROOT / name).read_text()
    assert "await using var worldJournal" in text and "await kernel.AdmitContractAsync" in text and "await driver.DriveAsync" in text
review = read(HERE / "review-summary.json")
if "--pre-review" not in sys.argv: assert review["standards"]["remaining"] == review["spec"]["remaining"] == 0
print(json.dumps({"passed": True, "candidateFiles": len(candidate), "protectedBuildInputs": len(protected),
    "priorSealedArtifacts": len(read(ROOT / prior["path"])["files"]), "newKernelCases": 10, "newFileAndKillCases": 4,
    "kernelPassed": 1205, "kernelExistingFailuresReproducedOnEntry": 3, "hostPassed": 147,
    "hostExistingFailureReproducedOnEntry": 1, "dshPassed": 24, "actualCommitBeforeOwnerAckKills": 1, "freshReaders": 2,
    "pendingOwnerPosition": 2, "originalStoredWorldPosition": 3, "nativeWorldOperationsAsync": 8,
    "worldInitializationAsync": False, "allStorageDomainsAsync": False, "parentChangeComplete": False,
    "additionalWorldBatchReductionClaimed": False, "continuationSupported": False,
    "reviewsComplete": review["standards"]["remaining"] == review["spec"]["remaining"] == 0}, indent=2))
