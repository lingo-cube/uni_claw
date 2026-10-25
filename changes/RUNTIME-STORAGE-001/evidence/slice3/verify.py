#!/usr/bin/env python3
"""Verify the frozen Run async slice without claiming all-domain migration is complete."""
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
assert set(candidate) == set(entry["files"]) and len(candidate) == 17
for p, h in candidate.items():
    assert sha(ROOT / p) == h == sha(HERE / "candidate-source-v1" / p), p
protected = {p: h for p, h in entry["inputs"].items() if p not in candidate}
for p, h in protected.items():
    assert sha(ROOT / p) == h, p
current = {str(p.relative_to(ROOT)) for directory in ("src", "tests", "tools") for p in (ROOT / directory).rglob("*")
    if p.is_file() and p.suffix in (".cs", ".csproj", ".props", ".targets") and not {"bin", "obj"}.intersection(p.parts)}
current.add("global.json")
assert current == set(entry["inputs"]) | {p for p in candidate if Path(p).suffix == ".cs"}
prior = entry["priorSeal"]
assert sha(ROOT / prior["path"]) == prior["sha256"]
for p, h in read(ROOT / prior["path"])["files"].items():
    assert sha(ROOT / p) == h, p
baseline = HERE.parent / "slice2"
baseline_sources = read(baseline / "entry.json")["inputs"]
baseline_sources.update({p: h for p, h in read(baseline / "candidate-inputs-v1.json")["files"].items()
    if Path(p).suffix in (".cs", ".csproj", ".props", ".targets")})
assert entry["inputs"] == baseline_sources and entry["baseline"]["allEntryBuildHashesMatch"]
archive = read(HERE / "baseline-archive.json")
assert sha(ROOT / archive["path"]) == archive["sha256"]
with tarfile.open(ROOT / archive["path"], "r:gz") as source:
    for p, h in entry["inputs"].items():
        assert hashlib.sha256(source.extractfile(p).read()).hexdigest() == h, p
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
def rows(directory, name):
    return ET.parse(directory / (name + ".trx")).findall(".//t:UnitTestResult", ns)
def failed(directory, name):
    return {r.attrib["testName"] for r in rows(directory, name) if r.attrib["outcome"] == "Failed"}
for name, total, passed in (("kernel-final", 1226, 1223), ("host-final", 201, 200), ("dsh-final", 24, 24)):
    results = rows(HERE, name)
    assert len(results) == total and sum(r.attrib["outcome"] == "Passed" for r in results) == passed
    assert all(r.attrib["outcome"] in ("Passed", "Failed") for r in results)
    if name != "dsh-final":
        assert failed(HERE, name) == failed(baseline, name)
for result, count in (("kernel-final", 18), ("host-final", 5)):
    cases = [r for r in rows(HERE, result) if "RunAsyncStorageTests" in r.attrib["testName"]]
    assert len(cases) == count and all(r.attrib["outcome"] == "Passed" for r in cases)
def message(directory, name, test):
    return next(r for r in rows(directory, name) if test in r.attrib["testName"]).find(".//t:Message", ns).text
old_registration = {line for line in message(baseline, "kernel-final", "SourceFiles_AreRegistered").splitlines() if line.startswith("src/")}
registration = {line for line in message(HERE, "kernel-final", "SourceFiles_AreRegistered").splitlines() if line.startswith("src/")}
assert registration < old_registration
assert {line.split(": ", 1)[0] for line in old_registration - registration} == {
    "src/UniClaw.Kernel/Run/RunModel.History.cs", "src/UniClaw.Kernel/Run/RunModel.TraversalHistory.cs"}
assert "CS0246" in (HERE / "run-owner-red.log").read_text()
assert "TimeoutException" in (HERE / "kernel-wait-red.log").read_text()
assert "CS8417" in (HERE / "file-async-red.log").read_text()
assert "Expected: Cancelled" in (HERE / "handler-cancel-red.log").read_text()
for p, h in read(HERE / "test-assembly-fingerprints.json").items():
    assert sha(ROOT / p) == h, p
binding = read(HERE / "candidate-verification-binding.json")
assert binding["sourceHashes"] == candidate
for p, h in binding["sha256"].items():
    assert sha(ROOT / p) == h, p

directory = HERE / "host-final/kill"
ready, killed, expected = read(directory / "ready.json"), read(directory / "kill-observed.json"), read(directory / "expected.json")
assert ready["DriverPending"] and not ready["IsTerminal"] and ready["Handlers"] == ready["PublishedHandlers"] == 0
assert ready["OwnerPosition"]["Sequence"] == 5 and ready["SourceRows"] == 6 and ready["Consultations"] == ready["PublishedOperations"] == 1
assert killed["Id"] == ready["ProcessId"] and killed["HasExited"] and killed["ExitCode"] != 0
readers = [read(directory / ("reader-" + str(n) + ".json")) for n in (0, 1)]
assert len({ready["ProcessId"], readers[0]["ProcessId"], readers[1]["ProcessId"]}) == 3
for reader in readers:
    assert reader["IsReadOnly"] and reader["TraversalHistoryAvailable"] and not reader["ContinuationSupported"] and not reader["IsTerminal"]
    assert reader["Records"] == expected["Rows"] and reader["HistoryPosition"]["Sequence"] == 6
    assert len(reader["TraversalHandlers"]) == 1 and reader["TraversalHandlers"][0]["Status"] == 5  # PlanHandlerCallStatus.Running
for p, h in read(directory / "source-hashes.json").items():
    assert sha(directory / p).upper() == h
assert json.loads((directory / "run-state.journal.jsonl").read_text().splitlines()[0])["schema"] == "uniclaw.run.history.v2"
size = read(HERE / "host-final/size/size.json")
assert size["observations"] == size["revisions"] == 128 and size["operations"] == 257
assert size["fullBytes"] == 7951934 and size["deltaBytes"] == 1107547

owner = (ROOT / "src/UniClaw.Kernel/Run/RunModel.History.cs").read_text()
assert "await asyncJournal.AppendAsync(record)" in owner and "Interlocked.CompareExchange" in owner and "Task.Run" not in owner
model = (ROOT / "src/UniClaw.Kernel/Run/RunModel.cs").read_text()
admission = model[model.index("private async ValueTask<ContractAdmission> AdmitContractCoreAsync"):model.index("private static IReadOnlySet")]
assert admission.index("await CommitStateAsync") < admission.index("View = view") < admission.index("RunId = runId")
native = (ROOT / "src/UniClaw.Host/Runtime/FileRunStateJournal.cs").read_text()
commit = native[native.index("public async ValueTask AppendAsync"):native.index("private async ValueTask CommitHeadAsync")]
assert commit.index("_stream.WriteAsync") < commit.index("_stream.FlushAsync") < commit.index("await CommitHeadAsync") < commit.index("_payloads.Add")
assert native.count("FileOptions.Asynchronous | FileOptions.WriteThrough") == 2
for marker in ("if (!_writeGate.Wait(0))", "run-record-exceeds-byte-limit", "await _writeGate.WaitAsync()"):
    assert marker in native
assert "Task.Run" not in native
kernel = (ROOT / "src/UniClaw.Kernel/UniKernel.cs").read_text()
for name in ("AdmitContractAsync", "RecordCycleAsync", "RecordActionAsync", "TransitionToTerminalAsync"):
    assert "await Run." + name in kernel, name
driver = (ROOT / "src/UniClaw.Kernel/Runtime/KernelRunDriver.cs").read_text()
for name in ("SelectIntentAsync", "ActViaCurrentGroundingAsync", "EvaluateTerminalAsync", "BeginTraversalRecordsAsync", "RecordTraversalHandlerAsync", "RecordTraversalOperationAsync", "RecordTraversalProgressAsync", "RecordTraversalAsync"):
    assert "await _kernel." + name in driver, name
assert "await CancelPendingPlanLanguageCallAsync()" in driver
for name in ("src/UniClaw.Host/HostRunner.cs", "src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs"):
    text = (ROOT / name).read_text()
    assert "await using var runStateJournal" in text and "await kernel.AdmitContractAsync" in text and "await driver.DriveAsync" in text
review = read(HERE / "review-summary.json")
if "--pre-review" not in sys.argv:
    assert review["standards"]["remaining"] == review["spec"]["remaining"] == 0
print(json.dumps({"passed": True, "candidateFiles": len(candidate), "protectedBuildInputs": len(protected),
    "priorSealedArtifacts": len(read(ROOT / prior["path"])["files"]), "newKernelCases": 18, "newNativeFileAndKillCases": 5,
    "kernelPassed": 1223, "kernelExistingFailuresMatchedExactEntrySource": 3, "hostPassed": 200,
    "hostExistingFailureMatchedExactEntrySource": 1, "dshPassed": 24, "actualCommitBeforeOwnerAckKills": 1, "freshReaders": 2,
    "pendingOwnerPosition": 5, "originalStoredRunPosition": 6, "pendingHandlerCalls": 0,
    "originalReadmeViolations": len(old_registration), "remainingOriginalReadmeViolations": len(registration),
    "runCoreAndTraversalAsync": True, "runFileRequestsWriteThrough": True,
    "initializationAsync": False, "allStorageDomainsAsync": False, "parentChangeComplete": False,
    "additionalRunBatchReductionClaimed": False, "continuationSupported": False,
    "reviewsComplete": review["standards"]["remaining"] == review["spec"]["remaining"] == 0}, indent=2))
