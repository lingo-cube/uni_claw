#!/usr/bin/env python3
"""Verify Group A; preserve independent Owners and separately identify concurrent input changes."""
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
concurrent = read(HERE / "concurrent-inputs-v1.json")["files"]
assert subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip() == entry["base"]
assert set(candidate) == set(entry["files"]) and len(candidate) == 20
for p, h in candidate.items():
    assert sha(ROOT / p) == h == sha(HERE / "candidate-source-v1" / p), p
protected = {p: h for p, h in entry["inputs"].items() if p not in candidate}
assert set(concurrent) == {"tests/UniClaw.Host.Dsh.Tests/SettingsCoveragePopupTaskSimulationTests.cs"}
for p, h in protected.items():
    if p in concurrent:
        assert h == concurrent[p]["entrySha256"]
        assert sha(ROOT / p) == concurrent[p]["observedSha256"] == sha(HERE / "concurrent-source-v1" / p), p
    else:
        assert sha(ROOT / p) == h, p
current = {str(p.relative_to(ROOT)) for directory in ("src", "tests", "tools") for p in (ROOT / directory).rglob("*")
    if p.is_file() and p.suffix in (".cs", ".csproj", ".props", ".targets") and not {"bin", "obj"}.intersection(p.parts)}
current.add("global.json")
assert current == set(entry["inputs"]) | {p for p in candidate if Path(p).suffix == ".cs"}
prior = entry["priorSeal"]
assert sha(ROOT / prior["path"]) == prior["sha256"]
for p, h in read(ROOT / prior["path"])["files"].items():
    assert sha(ROOT / p) == h, p
baseline = HERE.parent / "slice3"
baseline_sources = read(baseline / "entry.json")["inputs"]
baseline_sources.update({p: h for p, h in read(baseline / "candidate-inputs-v1.json")["files"].items()
    if Path(p).suffix in (".cs", ".csproj", ".props", ".targets")})
assert entry["inputs"] == baseline_sources and entry["baseline"]["allEntryBuildHashesMatch"]
for project in ("UniClaw.Kernel.Tests", "UniClaw.Host.Tests"):
    assert "UniClaw.Host.Dsh.Tests" not in (ROOT / "tests" / project / (project + ".csproj")).read_text()
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
for name, total, passed in (("kernel-final", 1248, 1245), ("host-final", 292, 291), ("dsh-final", 24, 24)):
    results = rows(HERE, name)
    assert len(results) == total and sum(r.attrib["outcome"] == "Passed" for r in results) == passed
    assert all(r.attrib["outcome"] in ("Passed", "Failed") for r in results)
    if name != "dsh-final":
        assert failed(HERE, name) == failed(baseline, name)
    else:
        assert all("SettingsCoveragePopupTaskSimulationTests" not in r.attrib["testName"] for r in results)
for result, selector, count in (("kernel-final", "GroupedStorage", 22), ("host-final", "ControlPlanAsyncStorageTests", 9)):
    cases = [r for r in rows(HERE, result) if selector in r.attrib["testName"]]
    assert len(cases) == count and all(r.attrib["outcome"] == "Passed" for r in cases)
def registration(directory):
    message = next(r for r in rows(directory, "kernel-final") if "SourceFiles_AreRegistered" in r.attrib["testName"]).find(".//t:Message", ns).text
    return {line for line in message.splitlines() if line.startswith("src/")}
before, after = registration(baseline), registration(HERE)
assert after < before and {line.split(": ", 1)[0] for line in before - after} == {"src/UniClaw.Kernel/Runtime/KernelRunDriver.PlanHistory.cs"}
for name, marker in (("control-red.log", "CS0246"), ("plan-red.log", "CS0246"), ("files-red.log", "CS8417")):
    assert marker in (HERE / name).read_text()
for p, h in read(HERE / "test-assembly-fingerprints.json").items():
    assert sha(ROOT / p) == h, p
binding = read(HERE / "candidate-verification-binding.json")
assert binding["sourceHashes"] == candidate
assert set(binding["effectiveBuildHashes"]) == current
for p, h in {**binding["sha256"], **binding["effectiveBuildHashes"]}.items():
    assert sha(ROOT / p) == h, p

for kind in ("control", "plan"):
    directory = HERE / "host-final/kill" / kind
    ready, killed, expected = read(directory / "ready.json"), read(directory / "kill-observed.json"), read(directory / "expected.json")
    assert ready["DriverPending"] and not ready["IsTerminal"] and ready["Handlers"] == ready["PublishedOperations"] == 0
    assert ready["Consultations"] == 1 and ready["PublishedPlan"] == (kind == "control")
    assert ready["SourceControlRows"] == ready["OwnerControlPosition"]["Sequence"] + (1 if kind == "control" else 0)
    assert ready["SourcePlanRows"] == ready["OwnerPlanPosition"]["Sequence"] + (1 if kind == "plan" else 0)
    assert killed["Id"] == ready["ProcessId"] and killed["HasExited"] and killed["ExitCode"] != 0
    readers = [read(directory / ("reader-" + str(n) + ".json")) for n in (0, 1)]
    assert len({ready["ProcessId"], readers[0]["ProcessId"], readers[1]["ProcessId"]}) == 3
    for reader in readers:
        assert reader["IsReadOnly"] and not reader["ContinuationSupported"] and not reader["IsTerminal"]
        assert reader["ControlRecords"] == expected["ControlRows"] and reader["PlanRecords"] == expected["PlanRows"]
        assert reader["ControlPosition"]["Sequence"] == ready["SourceControlRows"] and reader["PlanPosition"]["Sequence"] == ready["SourcePlanRows"]
    assert expected["ControlRows"][-1]["Operation"] == ("select" if kind == "control" else "initialize")
    assert expected["PlanRows"][-1]["Operation"] == "adopt"
    for p, h in read(directory / "source-hashes.json").items():
        assert sha(directory / p).upper() == h
size = read(HERE / "host-final/size/size.json")
assert size["observations"] == size["revisions"] == 128 and size["operations"] == 257
assert size["fullBytes"] == 7951934 and size["deltaBytes"] == 1107547
owner = (ROOT / "src/UniClaw.Kernel/Control/TraversalControl.History.cs").read_text()
assert "await asyncJournal.AppendAsync(row)" in owner and "Interlocked.CompareExchange" in owner and "Task.Run" not in owner
change = owner[owner.index("private async ValueTask<T> ChangeAsync"):owner.index("private async ValueTask AppendAsync")]
assert change.index("await AppendAsync(row)") < change.index("Load(state)")
native = (ROOT / "src/UniClaw.Host/Runtime/FileCommittedHistory.cs").read_text()
commit = native[native.index("public async ValueTask AppendAsync"):native.index("private async ValueTask CommitHeadAsync")]
assert commit.index("_stream.WriteAsync") < commit.index("_stream.FlushAsync") < commit.index("await CommitHeadAsync") < commit.index("_payloads.Add")
assert native.count("FileOptions.Asynchronous | FileOptions.WriteThrough") == 2 and "Task.Run" not in native
for marker in ("if (!_writeGate.Wait(0))", "-record-exceeds-byte-limit", "await _writeGate.WaitAsync()"):
    assert marker in native
for adapter in ("FileControlTraversalJournal", "FileRuntimePlanJournal"):
    source = (ROOT / "src/UniClaw.Host/Runtime" / (adapter + ".cs")).read_text()
    assert "FileCommittedHistory<" in source and "IAsyncDisposable" in source
kernel = (ROOT / "src/UniClaw.Kernel/UniKernel.cs").read_text()
assert "await _control.InitializeTraversalHistoryAsync" in kernel
driver = (ROOT / "src/UniClaw.Kernel/Runtime/KernelRunDriver.cs").read_text()
for name in ("EnsureRuntimePlanHistory", "BeginRuntimePlanReplacement", "CommitRuntimePlanAdoption"):
    assert "await " + name + "Async(" in driver
assert driver.index("await BeginRuntimePlanReplacementAsync(plan)") < driver.index("await AbandonPausedPlanAsync(\"plan:replaced-by-new-version\")") < driver.index("await CommitRuntimePlanAdoptionAsync(plan)")
for name in ("Select", "DrainProgressEvents", "Start", "ConfigureRules", "Abandon", "Returned", "ReadOnlyCompleted", "Navigated", "Pause", "Resume", "Gap", "Fail"):
    assert "await _traversal." + name + "Async(" in driver
for name in ("src/UniClaw.Host/HostRunner.cs", "src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs"):
    source = (ROOT / name).read_text()
    assert "await using var controlJournal" in source and "await using var planJournal" in source
review = read(HERE / "review-summary.json")
if "--pre-review" not in sys.argv:
    assert review["standards"]["remaining"] == review["spec"]["remaining"] == 0
print(json.dumps({"passed": True, "candidateFiles": len(candidate), "unchangedProtectedBuildInputs": len(protected) - len(concurrent),
    "separatelyObservedUnownedBuildInputs": len(concurrent), "priorSealedArtifacts": len(read(ROOT / prior["path"])["files"]),
    "newKernelCases": 22, "newNativeFileAndKillCases": 9, "kernelPassed": 1245, "kernelExistingFailuresMatchedEntry": 3,
    "hostPassed": 291, "hostExistingFailureMatchedEntry": 1, "dshPassed": 24, "actualCommitBeforeOwnerAckKills": 2, "freshReaders": 4,
    "sharedFileCommitBuyers": 2, "originalReadmeViolations": len(before), "remainingOriginalReadmeViolations": len(after),
    "controlAndPlanRuntimeAsync": True, "allStorageDomainsAsync": False, "parentChangeComplete": False,
    "additionalBatchReductionClaimed": False, "continuationSupported": False,
    "reviewsComplete": review["standards"]["remaining"] == review["spec"]["remaining"] == 0}, indent=2))
