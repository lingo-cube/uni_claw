#!/usr/bin/env python3
"""Verify this bounded observation slice without claiming all-domain migration complete."""
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
entry = read(HERE.parent / "entry.json")
candidate = read(HERE / "candidate-inputs-v2.json")["files"]
assert subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip() == "a05176f3c66e0a3206f49dde463f51df1335975c"
assert set(candidate) == set(read(HERE.parent / "ownership.json")["files"])
for p, h in candidate.items(): assert sha(ROOT / p) == h == sha(HERE / "candidate-source-v2" / p), p
protected = {p: h for p, h in entry["inputs"].items() if p not in candidate}
for p, h in protected.items(): assert sha(ROOT / p) == h, p
current = {str(p.relative_to(ROOT)) for d in ("src", "tests", "tools") for p in (ROOT / d).rglob("*")
    if p.is_file() and p.suffix in (".cs", ".csproj", ".props", ".targets") and not {"bin", "obj"}.intersection(p.parts)}
current.add("global.json")
assert current == set(entry["inputs"]) | {p for p in candidate if Path(p).suffix == ".cs"}
prior = entry["priorSealed"]
assert sha(ROOT / prior["manifest"]) == prior["sha256"]
for p, h in read(ROOT / prior["manifest"])["files"].items(): assert sha(ROOT / p) == h, p
baseline = read(HERE / "baseline-archive.json")
assert sha(ROOT / baseline["path"]) == baseline["sha256"]
with tarfile.open(ROOT / baseline["path"], "r:gz") as archive:
    for p, h in entry["inputs"].items(): assert hashlib.sha256(archive.extractfile(p).read()).hexdigest() == h, p
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
def rows(name): return ET.parse(HERE / (name + ".trx")).findall(".//t:UnitTestResult", ns)
def failed(name): return {r.attrib["testName"] for r in rows(name) if r.attrib["outcome"] == "Failed"}
assert len(rows("kernel-final")) == 1198 and len(failed("kernel-final")) == 3
assert failed("kernel-final") == failed("baseline-three-retry") and len(rows("baseline-three-retry")) == 3
for name, count in (("host-final", 288), ("dsh-final", 24)):
    assert len(rows(name)) == count and all(r.attrib["outcome"] == "Passed" for r in rows(name)), name
assert len([r for r in rows("kernel-final") if "EvidenceBatchAdmissionTests" in r.attrib["testName"]]) == 9
host_cases = [r for r in rows("host-final") if "EvidenceBatchStorageTests" in r.attrib["testName"]]
assert len(host_cases) == 6
message = rows("readme-current")[0].find(".//t:Message", ns).text
for name in ("EvidenceLedger.Batch.cs", "EvidenceLedger.History.cs", "FileEvidenceJournal.cs"): assert name not in message
assert "CS0246" in (HERE / "ledger-red.log").read_text()
io = read(HERE / "host-final/io-counts.json")
assert io["Records"] == 64 and not io["PowerLossProof"]
for field in ("FrameWrites", "FrameFlushes", "HeadWrites", "HeadFlushes"):
    assert io["Single"][field] - 1 == 64 and io["Batch"][field] - 1 == 1
assert io["Single"]["FrameBytesSubmitted"] == io["Batch"]["FrameBytesSubmitted"]
assert io["Batch"]["PeakBatchRecords"] == 64 and io["Batch"]["PeakBatchBytes"] <= 1024 * 1024
d = HERE / "host-final/kill"
ready, killed, expected = read(d / "ready.json"), read(d / "kill-observed.json"), read(d / "expected.json")
assert not ready["IsTerminal"] and ready["PublishedAdmissions"] == ready["PublishedCanonical"] == 0 and ready["Belief"] is None
assert ready["SourceRows"] == 4 and killed["Id"] == ready["ProcessId"] and killed["HasExited"] and killed["ExitCode"] != 0
readers = [read(d / ("reader-" + str(n) + ".json")) for n in (0, 1)]
assert len({ready["ProcessId"], readers[0]["ProcessId"], readers[1]["ProcessId"]}) == 3
for reader in readers:
    assert reader["IsReadOnly"] and reader["Admissions"] == 4 and reader["Canonical"] == 2 and reader["Rows"] == expected["Rows"]
for name, h in read(d / "source-hashes.json").items(): assert sha(d / name).upper() == h
batch = (ROOT / "src/UniClaw.Kernel/Evidence/EvidenceLedger.Batch.cs").read_text()
for forbidden in ("Task.Run", "FileStream", "UniClaw.Host", "Thread.Sleep"): assert forbidden not in batch
native = (ROOT / "src/UniClaw.Host/Runtime/FileEvidenceJournal.cs").read_text()
assert "FileOptions.Asynchronous" in native and "_stream.WriteAsync" in native and "_stream.FlushAsync" in native
assert "CommitHeadAsync" in native and native.count("GetAwaiter().GetResult()") == 1
assert "if (!_writeGate.Wait(0))" in native
assert "new Dictionary<string, EvidenceRecord>(_canonicalRecords" not in batch
driver = (ROOT / "src/UniClaw.Kernel/Runtime/KernelRunDriver.cs").read_text()
assert "await _kernel.ProcessBatchAsync" in driver and "await DriveCore(cancellationToken)" in driver
for name in ("src/UniClaw.Host/Program.cs", "src/UniClaw.Host.Dsh/Program.cs", "src/UniClaw.Host.Dsh/RuntimeHttpServer.cs"):
    assert "await HostRunner.RunOnceAsync" in (ROOT / name).read_text()
for p, h in read(HERE / "test-assembly-fingerprints.json").items(): assert sha(ROOT / p) == h, p
binding = read(HERE / "candidate-verification-binding.json")
assert binding["sourceHashes"] == candidate
for p, h in binding["sha256"].items(): assert sha(ROOT / p) == h, p
review = read(HERE / "review-summary.json")
if "--pre-review" not in sys.argv:
    assert review["pending"] is None and review["spec"]["remaining"] == review["standards"]["remaining"] == 0
print(json.dumps({"passed": True, "slice": "observation Evidence async batch only", "candidateFiles": len(candidate),
    "unchangedUnownedBuildInputs": len(protected), "priorSealedArtifacts": prior["files"],
    "newLedgerDriverCases": 9, "newFileCases": 6, "kernelPassed": 1195, "kernelExistingFailuresReproducedOnEntry": 3,
    "hostPassed": 288, "dshPassed": 24, "actualNonterminalBatchKills": 1, "freshReaders": 2,
    "managedFrameWriteCalls": {"single": 64, "batch": 1}, "headWrites": {"single": 64, "batch": 1},
    "physicalDiskSyscallsMeasured": False, "allStorageDomainsMigrated": False, "parentChangeComplete": False,
    "continuationSupported": False, "reviewsComplete": review["pending"] is None}, indent=2))
