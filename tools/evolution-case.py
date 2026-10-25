#!/usr/bin/env python3
"""Export sealed component replay inputs. No live effects, repair or promotion."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import sys
import subprocess
import uuid
import xml.etree.ElementTree as ET
from jsonschema import validate, ValidationError

ROOT = Path(__file__).resolve().parent.parent
PROJECT = "tools/EvolutionReplay/UniClaw.EvolutionReplay.Tests.csproj"
CARRIER = "tools/EvolutionReplay/ExportedCaseTests.cs"
FILTER = "FullyQualifiedName=UniClaw.EvolutionReplay.ExportedCaseTests.IndependentPopupLabel"
REALIZATION = ["tools/evolution-case.py", PROJECT, CARRIER,
               "src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs",
               "schemas/evolution-exported-case.schema.json"]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def relative(path):
    path = Path(path).resolve()
    return path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path)


def dump(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode()


def export_case(args):
    run = args.run_dir.resolve()
    out = args.out_dir.resolve()
    oracle_path = args.oracle.resolve()
    require(not out.exists(), "case-output-exists")
    require(not out.is_relative_to(run), "output-inside-source-run")
    require(not oracle_path.is_relative_to(run), "oracle-must-be-independent-of-run")
    require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", args.id), "invalid-case-id")
    require(re.fullmatch(r"capture-[A-Za-z0-9_-]+", args.capture_id), "invalid-capture-id")
    raw = {}
    def read(path):
        raw[path] = path.read_bytes()
        return raw[path]
    facts = json.loads(read(run / "facts.json"))
    require(facts.get("schemaVersion") == "uniclaw.settings-coverage-facts.v2", "unsupported-facts-schema")
    require(facts.get("status") in {"Completed", "TerminalNotProven", "UnconfirmedDelivery", "Failed", "SafeStopped", "Cancelled", "Faulted", "BudgetExhausted", "Blocked"}, "source-run-not-final")
    trace = json.loads(read(run / "settings-trace.json"))
    steps = json.loads(read(run / "coverage-steps.json"))
    matches = [(i, frame) for i, frame in enumerate(trace) if frame.get("captureId") == args.capture_id]
    require(len(matches) == 1 and matches[0][0] > 0, "capture-not-unique-or-no-previous-observation")
    index, frame = matches[0]
    require(frame.get("context") == "PostActionEffectFlow" and frame.get("observationCycleId"), "capture-not-post-action-observation")
    trigger = [step for step in steps if step.get("postCaptureId") == args.capture_id]
    require(len(trigger) == 1 and trigger[0].get("receiptOutcome") == "DeliveryCompleted" and trigger[0].get("receiptId"), "capture-receipt-correlation-missing")
    require(sum(step.get("receiptId") == trigger[0]["receiptId"] for step in steps) == 1, "receipt-not-unique")
    oracle = json.loads(read(oracle_path))
    expected = [case for case in oracle["cases"] if case.get("id") == args.oracle_case]
    require(isinstance(oracle.get("source"), str) and bool(oracle["source"].strip()), "independent-oracle-source-required")
    require(len(expected) == 1 and type(expected[0].get("popupAtTrigger")) is bool, "independent-oracle-label-required")
    require(expected[0].get("triggerTarget") == trigger[0].get("targetDescriptor"), "oracle-target-mismatch")
    previous = trace[index - 1].get("captureId", "")
    require(re.fullmatch(r"capture-[A-Za-z0-9_-]+", previous), "invalid-previous-capture")
    files = [(run / "facts.json", "facts"), (run / "settings-trace.json", "observation-trace"),
             (run / "coverage-steps.json", "steps"), (oracle_path, "oracle")]
    for capture, role in [(previous, "previous-xml"), (args.capture_id, "capture-xml")]:
        path = run / "evidence" / (capture + ".xml")
        xml = ET.fromstring(read(path))
        require(bool(xml.findall(".//node")), "empty-xml-observation")
        files.append((path, role))
    entries = [{"path": "inputs/" + role + (".xml" if role.endswith("xml") else ".json"), "sha256": sha(raw[path]),
                "role": role, "originalPath": relative(path)} for path, role in files]
    manifest = {
        "schema": "uniclaw.evolution.case.v0", "id": args.id, "sourceChange": args.source_change,
        "adapter": "settings-popup.v1", "inputBase": "case-directory",
        "source": {"runDir": relative(run), "captureId": args.capture_id, "traceIndex": index,
                   "previousCaptureId": previous, "receiptId": trigger[0]["receiptId"]},
        "context": {"hostPackage": args.host_package}, "inputs": entries,
        "expected": {"claim": "Popup classification agrees with independent curated label",
                     "oracle": "inputs/oracle.json", "oracleCase": args.oracle_case,
                     "popupPresent": expected[0]["popupAtTrigger"],
                     "qualification": "curated fixture label; not original window-stack evidence"},
        "reproduce": {"project": PROJECT, "filter": FILTER,
                      "command": shlex.join(["python3", str(ROOT / "tools/evolution-case.py"), "replay", "--case", str(out / "case.json")])},
        "realization": [{"path": p, "sha256": sha((ROOT / p).read_bytes())} for p in REALIZATION],
        "baseline": {"path": "src/UniClaw.Host/SettingsPopupClassifier.cs",
                     "sha256": sha((ROOT / "src/UniClaw.Host/SettingsPopupClassifier.cs").read_bytes()),
                     "enforced": False},
        "owner": "UniClaw.Host.SettingsPopupClassifier", "fdpExpectation": "XML popup classification seam",
        "candidateDirection": "Evidence-led classification; preserve SIM-017 semantic gate",
        "gate": {"required": True, "refs": ["changes/SIM-017/state.md"],
                 "decisions": ["Window evidence and classification semantics remain unapproved"]},
        "residualRisks": ["component-only; not full Run replay or live proof",
                          "previous recorded observation is not independent pre-action association",
                          "no recorded CANCEL/clearance or per-frame timestamps are invented",
                          "oracle epistemic independence still requires human review"]}
    if args.signal:
        item = json.loads(read(args.signal.resolve()))
        validate(item, json.loads((ROOT / "schemas/evolution-signal.schema.json").read_text()))
        require(item["lane"] == "live-run" and item["source"]["kind"] == "runDir", "signal-not-live-run")
        require((ROOT / item["source"]["path"]).resolve() == run / "facts.json" and item["source"].get("sha256") == sha(raw[run / "facts.json"]), "signal-run-mismatch")
        manifest["signal"] = {"kind": "run-dir", "ref": relative(args.signal),
            "sha256": sha(raw[args.signal.resolve()]), "failureKey": item["failureKey"]}
    if args.diagnosis:
        packet = json.loads(read(args.diagnosis.resolve()))
        validate(packet, json.loads((ROOT / "schemas/diagnosis-packet.schema.json").read_text()))
        require(Path(packet["subject"]["runDir"]).resolve() == run, "diagnosis-run-mismatch")
        manifest["diagnosis"] = {"path": relative(args.diagnosis), "sha256": sha(raw[args.diagnosis.resolve()])}
    require(all(path.read_bytes() == data for path, data in raw.items()), "source-changed-during-export")
    validate(manifest, json.loads((ROOT / "schemas/evolution-exported-case.schema.json").read_text()))
    out.mkdir(parents=True, exist_ok=False)
    for entry, (path, _) in zip(entries, files):
        target = out / entry["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as stream:
            stream.write(raw[path])
    data = dump(manifest)
    with (out / "case.json").open("xb") as stream:
        stream.write(data)
    with (out / "case.sha256").open("x") as stream:
        stream.write(sha(data) + "\n")
    return {"case": str(out / "case.json"), "manifestSha256": sha(data), "scope": "component-only"}


def verify_case(path):
    path = path.resolve()
    directory = path.parent
    data = path.read_bytes()
    require(sha(data) == (directory / "case.sha256").read_text().strip(), "case-manifest-hash-mismatch")
    case = json.loads(data)
    validate(case, json.loads((ROOT / "schemas/evolution-exported-case.schema.json").read_text()))
    inputs = {}
    for entry in case["inputs"]:
        target = (directory / entry["path"]).resolve()
        require(target.is_relative_to(directory) and target != path, "input-outside-case")
        content = target.read_bytes()
        require(sha(content) == entry["sha256"], "input-hash-mismatch:" + entry["path"])
        require(entry["role"] not in inputs, "duplicate-input-role")
        inputs[entry["role"]] = content
    require(set(inputs) == {"facts", "observation-trace", "steps", "previous-xml", "capture-xml", "oracle"}, "input-role-missing")
    oracle = json.loads(inputs["oracle"])
    labels = [label for label in oracle["cases"] if label["id"] == case["expected"]["oracleCase"]]
    require(len(labels) == 1 and labels[0]["popupAtTrigger"] == case["expected"]["popupPresent"], "case-oracle-mismatch")
    require({entry["path"] for entry in case["realization"]} == set(REALIZATION), "realization-set-mismatch")
    for entry in case["realization"]:
        require(sha((ROOT / entry["path"]).read_bytes()) == entry["sha256"], "realization-drift:" + entry["path"])
    require(case["reproduce"]["project"] == PROJECT and case["reproduce"]["filter"] == FILTER, "unsupported-replay-carrier")
    for name, field in [("diagnosis", "path"), ("signal", "ref")]:
        if name in case:
            ref = case[name]
            require(sha((ROOT / ref[field]).read_bytes()) == ref["sha256"], "reference-hash-mismatch:" + name)
    return case, sha(data)


def replay_case(args):
    case, manifest_hash = verify_case(args.case_path)
    out = (args.out_dir or ROOT / "evidence/evolution/replays" / uuid.uuid4().hex).resolve()
    require(not out.is_relative_to(args.case_path.resolve().parent), "replay-output-inside-locked-case")
    original_run = (ROOT / case["source"]["runDir"]).resolve()
    require(not out.is_relative_to(original_run), "replay-output-inside-original-run")
    out.mkdir(parents=True, exist_ok=False)
    command = ["dotnet", "test", str(ROOT / PROJECT), "-m:1", "-nr:false",
               "--filter", FILTER, "--logger", "trx;LogFileName=case.trx", "--results-directory", str(out)]
    environment = {**os.environ, "UNICLAW_EVOLUTION_CASE": str(args.case_path.resolve())}
    with (out / "carrier.txt").open("w") as log:
        try:
            completed = subprocess.run(command, cwd=ROOT, env=environment, stdout=log,
                                       stderr=subprocess.STDOUT, text=True, timeout=120)
            exit_code = completed.returncode
        except subprocess.TimeoutExpired:
            exit_code = 124
    results = []
    if (out / "case.trx").is_file():
        results = [element for element in ET.parse(out / "case.trx").getroot().iter()
                   if element.tag.split("}")[-1] == "UnitTestResult"]
    status, message = "unusable", "carrier did not produce one usable result"
    if len(results) == 1:
        result = results[0]
        message = next((element.text or "" for element in result.iter() if element.tag.split("}")[-1] == "Message"), "")
        if result.get("outcome") == "Passed" and exit_code == 0:
            status = "matched"
        elif result.get("outcome") == "Failed" and "independent-popup-label-mismatch:" in message:
            status = "mismatch"
    require(verify_case(args.case_path)[1] == manifest_hash, "case-changed-during-replay")
    output = {"schema": "uniclaw.evolution.replay.v0", "authority": "non-authoritative",
              "status": status, "manifestSha256": manifest_hash, "inputs": case["inputs"],
              "scope": "component-only", "recordedEffectExecution": False, "message": message,
              "carrierExitCode": exit_code, "trx": str(out / "case.trx"), "gate": case["gate"]}
    with (out / "replay.json").open("xb") as stream:
        stream.write(dump(output))
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    export = commands.add_parser("export")
    for option in ["run-dir", "oracle", "out-dir"]:
        export.add_argument("--" + option, type=Path, required=True)
    for option in ["capture-id", "oracle-case", "id", "source-change"]:
        export.add_argument("--" + option, required=True)
    export.add_argument("--host-package", default="com.android.settings")
    export.add_argument("--signal", type=Path)
    export.add_argument("--diagnosis", type=Path)
    check = commands.add_parser("verify")
    check.add_argument("--case", dest="case_path", type=Path, required=True)
    replay = commands.add_parser("replay")
    replay.add_argument("--case", dest="case_path", type=Path, required=True)
    replay.add_argument("--out-dir", type=Path)
    args = parser.parse_args()
    try:
        if args.command == "export":
            result = export_case(args)
        elif args.command == "replay":
            result = replay_case(args)
        else:
            result = {"status": "valid", "manifestSha256": verify_case(args.case_path)[1]}
        print(json.dumps(result))
        return {"matched": 0, "mismatch": 1, "unusable": 2}.get(result.get("status"), 0)
    except (OSError, ValueError, KeyError, TypeError, AttributeError, ET.ParseError, ValidationError) as error:
        print(json.dumps({"status": "unusable", "reason": str(error)}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
