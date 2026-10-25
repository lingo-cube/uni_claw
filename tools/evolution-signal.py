#!/usr/bin/env python3
"""Non-authoritative failure intake. No Change creation, model calls or repair."""
from __future__ import annotations
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent


def digest(data):
    return hashlib.sha256(data).hexdigest()


def reference(path, root):
    path = Path(path).resolve()
    return {"path": str(path.relative_to(root)) if path.is_relative_to(root) else str(path),
            "sha256": digest(path.read_bytes())}


def git_context(root):
    def capture(*args):
        result = subprocess.run(["git", *args], cwd=root, text=True, capture_output=True)
        return result.stdout.strip() if result.returncode == 0 else None
    status = capture("status", "--porcelain", "--untracked-files=normal")
    return {"head": capture("rev-parse", "HEAD"),
            "dirtyPaths": status.splitlines() if status else [],
            "gitAvailable": status is not None}


def signal(lane, source, verdict, identity, context, **extra):
    return {"schema": "uniclaw.evolution.signal.v0", "authority": "non-authoritative",
            "lane": lane, "source": source, "verdict": verdict,
            "failureKey": digest(json.dumps([lane, identity], sort_keys=True).encode()),
            "context": context, "preliminaryClass": None,
            "ts": datetime.datetime.now(datetime.timezone.utc).isoformat(), **extra}


def scan_dev(trx_paths, root=ROOT, intent="verify", context=None):
    context = context if context is not None else git_context(root)
    context = {**context, "intent": intent}
    signals = []
    for path in map(Path, trx_paths):
        try:
            ref = reference(path, root)
            tree = ET.parse(path).getroot()
            results = [e for e in tree.iter() if e.tag.split("}")[-1] == "UnitTestResult"]
            if not results:
                raise ValueError("no test results")
            for result in results:
                outcome = result.get("outcome")
                if outcome in ("Passed", "NotExecuted", "Skipped"):
                    continue
                name = result.get("testName", "unknown")
                message = next((e.text or "" for e in result.iter()
                                if e.tag.split("}")[-1] == "Message"), "")
                signals.append(signal("dev-verify", {"kind": "trx", **ref},
                    "failed" if outcome == "Failed" else "unknown", [name, outcome, message], context,
                    failure={"testName": name, "outcome": outcome, "message": message},
                    reproduce={"filter": "FullyQualifiedName=" + name,
                               "limitation": "test filter only; project and immutable inputs must be locked before replay"}))
        except (OSError, ET.ParseError, ValueError) as error:
            signals.append(signal("dev-verify", {"kind": "trx", "path": str(path)},
                "unknown", [str(path), str(error)], context, uncertainty=[str(error)]))
    return signals


def write_json(path, value, exclusive=False):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x" if exclusive else "w", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def scan_run(run_dir, root=ROOT):
    path = Path(run_dir) / "facts.json"
    context = {"intent": "diagnosis", "runStatus": None}
    try:
        source = {"kind": "runDir", **reference(path, root)}
        facts = json.loads(path.read_text(encoding="utf-8-sig"))
        status = facts.get("status")
        context.update(runStatus=status, terminal=facts.get("terminal"),
                       reason=facts.get("reason"))
        if status == "Completed" and facts.get("terminal") is True:
            return []
        if status in ("Running", "Created", "Consulting", "Executing"):
            return []
        stopped = status in ("TerminalNotProven", "UnconfirmedDelivery", "Failed",
            "SafeStopped", "Cancelled", "Faulted", "BudgetExhausted", "Blocked")
        return [signal("live-run", source, "failed" if stopped else "unknown",
            [str(Path(run_dir).resolve()), status, facts.get("reason")], context,
            reproduce=None, replayStatus="pending-reproduction",
            uncertainty=["runDir export/replay is not implemented; this is not a replay-ready case"])]
    except (OSError, ValueError, AttributeError) as error:
        return [signal("live-run", {"kind": "runDir", "path": str(path)}, "unknown",
            [str(path), str(error)], context, reproduce=None,
            replayStatus="pending-reproduction", uncertainty=[str(error)])]


def scan_compositions(paths, root=ROOT, intent="verify", context=None):
    """Read pinned bytes directly; this identifies drift, not its causal owner."""
    context = {**(context if context is not None else git_context(root)), "intent": intent}
    items = []
    for path in map(Path, paths):
        source = {"kind": "composition", "path": str(path)}
        try:
            source = {"kind": "composition", **reference(path, root)}
            manifest = json.loads(path.read_text())
            if manifest.get("schemaVersion") != "component-replay-composition.v1" or not isinstance(manifest.get("pins"), list) or not manifest["pins"]:
                raise ValueError("unsupported/empty composition: " + str(path))
            for pin in manifest["pins"]:
                if not isinstance(pin.get("path"), str) or not isinstance(pin.get("sha256"), str):
                    raise ValueError("invalid composition pin")
                target = (root / pin["path"]).resolve()
                if not target.is_relative_to(root):
                    raise ValueError("composition path escapes repo: " + pin["path"])
                actual = digest(target.read_bytes()) if target.is_file() else None
                if actual == pin["sha256"]:
                    continue
                items.append(signal("dev-verify", source, "hash-mismatch",
                    [source["path"], pin["path"], pin["sha256"], actual], context,
                    drift={"manifest": source["path"], "path": pin["path"],
                           "expected": pin["sha256"], "actual": actual},
                    uncertainty=["asset drift only; root cause is not inferred from dirty paths"]))
        except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
            items.append(signal("dev-verify", source, "unknown", [str(path), str(error)],
                                context, uncertainty=[str(error)]))
    return items


def persist(signals, out_dir, root=ROOT, known_issues=None):
    out_dir = Path(out_dir).resolve()
    known = json.loads(Path(known_issues).read_text())["issues"] if known_issues else []
    paths = []
    for item in signals:
        key = item["failureKey"]
        issue = next((issue for issue in known if issue["failureKey"] == key), None)
        if issue is None:
            for case_path in sorted(root.glob("changes/*/cases/*.json")):
                state_path = case_path.parent.parent / "state.md"
                if not state_path.exists() or "lifecycle_state: closed" in state_path.read_text():
                    continue
                case = json.loads(case_path.read_text())
                if case.get("signal", {}).get("failureKey") == key:
                    issue = {"ref": reference(case_path, root)}
                    break
        entry = out_dir / "intake" / (key + ".json")
        if (item["context"].get("intent") == "expected-red" and item["verdict"] == "failed"
                and item["source"]["kind"] == "trx"):
            disposition = "expected-red"
        elif issue:
            disposition = "known-issue"
        else:
            try:
                write_json(entry, {"failureKey": key, "lane": item["lane"],
                    "source": item["source"], "status": "candidate", "changeCreated": False}, exclusive=True)
                disposition = "candidate"
            except FileExistsError:
                disposition = "duplicate"
        item["triage"] = {"disposition": disposition, "changeCreated": False,
                          "reference": issue["ref"] if issue else (str(entry) if disposition in ("candidate", "duplicate") else None)}
        path = out_dir / "signals" / (uuid.uuid4().hex + ".json")
        write_json(path, item, exclusive=True)
        paths.append(str(path))
    return paths


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="lane", required=True)
    dev = sub.add_parser("dev", help="scan archived/current TRX; this does not execute tests")
    dev.add_argument("--trx", action="append", required=True)
    dev.add_argument("--intent", choices=("verify", "expected-red"), required=True)
    dev.add_argument("--repo-root", type=Path, default=ROOT)
    dev.add_argument("--out-dir", type=Path, required=True)
    dev.add_argument("--known-issues", type=Path)
    dev.add_argument("--composition", type=Path, action="append", default=[])
    run = sub.add_parser("run", help="inspect run facts without modifying the run")
    run.add_argument("--run-dir", type=Path, required=True)
    run.add_argument("--repo-root", type=Path, default=ROOT)
    run.add_argument("--out-dir", type=Path, required=True)
    run.add_argument("--known-issues", type=Path)
    run.add_argument("--diagnosis", type=Path, help="reference an existing diagnosis packet; never copy its conclusions")
    argv = sys.argv[1:]
    # Existing RuntimeToolHost entry convention supplies --run-dir/--out-dir.
    args = parser.parse_args((["run"] + argv) if argv and argv[0].startswith("--") and "--run-dir" in argv else argv)
    try:
        items = (scan_dev(args.trx, args.repo_root.resolve(), args.intent) if args.lane == "dev"
                 else scan_run(args.run_dir, args.repo_root.resolve()))
        if args.lane == "dev":
            items += scan_compositions(args.composition, args.repo_root.resolve(), args.intent)
        else:
            if args.out_dir.resolve().is_relative_to(args.run_dir.resolve()):
                raise ValueError("signal output must be outside the input run directory")
            if args.diagnosis:
                packet = json.loads(args.diagnosis.read_text())
                if packet.get("schema") != "uniclaw.diagnosis.packet.v0" or packet.get("authority") != "non-authoritative":
                    raise ValueError("invalid/non-authoritative diagnosis required")
                for item in items:
                    item["diagnosis"] = reference(args.diagnosis, args.repo_root.resolve())
        paths = persist(items, args.out_dir, args.repo_root.resolve(), args.known_issues)
        print(json.dumps({"signals": paths, "changeCreated": False}))
        return 0
    except (OSError, ValueError, KeyError) as error:
        print(str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
