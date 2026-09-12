#!/usr/bin/env python3
"""FastScreen validation set (FSV-001) collection tool — replayable.

This tool is BOTH the live collector and the replay harness. Every frame in
`manifest.json` was captured through the `snap` subcommand below, and every
navigation recorded in `sequences/*.json` is expressed in the action
vocabulary implemented by the `_run_action` function, so `replay` re-walks the
exact command sequence.

Layout (all relative to this file's directory):
  frames/<sha12>.png         content-addressed screenshot (sha256 prefix)
  frames/<sha12>.meta.json   frame metadata
  uia/<sha12>.xml            uiautomator window dump for the frame
  sequences/<seqid>.json     ordered frame sequence + actions
  manifest.json              dataset manifest

Action vocabulary (used in sequences/*.json `actions` and `setup`):
  {action: "am",     params: {extra: "android.settings.WIFI_SETTINGS"}}
  {action: "tap",    params: {x, y}}
  {action: "swipe",  params: {x1, y1, x2, y2, dur}}
  {action: "key",    params: {key: "KEYCODE_BACK"}}   (or {code: 4})
  {action: "text",   params: {text: "zzz"}}
  {action: "uimode", params: {night: "yes"|"no"}}
  {action: "notify", params: {tag, title, text, big: true}}
  {action: "wait",   params: {ms}}

Usage:
  collect.py snap --stratum list --label settings-home --route android.settings.SETTINGS [--note "..." --seq SEQID]
  collect.py stats
  collect.py manifest
  collect.py validate
  collect.py replay [--out-dir REPLAY_ROOT] [--only SEQID]
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import struct
import subprocess
import sys
import time

SERIAL = "emulator-5554"
ROOT = os.path.dirname(os.path.abspath(__file__))
FRAMES_DIR = os.path.join(ROOT, "frames")
UIA_DIR = os.path.join(ROOT, "uia")
SEQS_DIR = os.path.join(ROOT, "sequences")
MANIFEST = os.path.join(ROOT, "manifest.json")
W = 1080
H = 2400
ADB = ["adb", "-s", SERIAL]

# --------------------------------------------------------------------------- #
# adb plumbing
# --------------------------------------------------------------------------- #

def adb(args, timeout=120, check=True):
    """Run an adb command; return decoded stdout (strips trailing newline)."""
    cmd = ADB + [str(a) for a in args]
    proc = subprocess.run(cmd, capture_output=True, timeout=timeout)
    if check and proc.returncode != 0:
        raise RuntimeError(
            f"adb {' '.join(cmd)} failed rc={proc.returncode}: "
            f"{proc.stderr.decode(errors='replace')[:400]}"
        )
    return proc.stdout.decode(errors="replace").strip()


def adb_bytes(args, timeout=120, check=True):
    """Run an adb command; return raw stdout bytes."""
    cmd = ADB + [str(a) for a in args]
    proc = subprocess.run(cmd, capture_output=True, timeout=timeout)
    if check and proc.returncode != 0:
        raise RuntimeError(
            f"adb {' '.join(cmd)} failed rc={proc.returncode}: "
            f"{proc.stderr.decode(errors='replace')[:400]}"
        )
    return proc.stdout


def run_action(a):
    """Execute one action dict from the sequence vocabulary (also used live)."""
    kind = a["action"]
    p = a.get("params", {})
    if kind == "am":
        adb(["shell", "am", "start", "-a", p["extra"]])
    elif kind == "tap":
        adb(["shell", "input", "tap", str(p["x"]), str(p["y"])])
    elif kind == "swipe":
        adb(["shell", "input", "swipe",
             str(p["x1"]), str(p["y1"]), str(p["x2"]), str(p["y2"]),
             str(p.get("dur", 300))])
    elif kind == "key":
        if "key" in p:
            adb(["shell", "input", "keyevent", p["key"]])
        else:
            adb(["shell", "input", "keyevent", str(p["code"])])
    elif kind == "text":
        adb(["shell", "input", "text", p["text"]])
    elif kind == "uimode":
        adb(["shell", "cmd", "uimode", "night", p["night"]])
    elif kind == "notify":
        extra = ["-S", "bigtext"] if p.get("big") else []
        cmd = ["shell", "cmd", "notification", "post"] + extra + \
              ["-t", p["title"], p["tag"], p["text"]]
        adb(cmd, check=False)
    elif kind == "wait":
        time.sleep(p.get("ms", 1000) / 1000.0)
    else:
        raise ValueError(f"unknown action kind: {kind}")

# --------------------------------------------------------------------------- #
# capture primitives
# --------------------------------------------------------------------------- #

def png_size(data: bytes):
    """Return (w, h) from PNG IHDR; raise ValueError if not a PNG."""
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG signature")
    if data[12:16] != b"IHDR":
        raise ValueError("missing IHDR chunk")
    w, h = struct.unpack(">II", data[16:24])
    return w, h


def current_focus():
    """Best-effort current focused window/activity for the meta route field."""
    out = adb(["shell", "dumpsys", "window", "|", "grep", "mCurrentFocus"],
              check=False, timeout=90)
    for line in out.splitlines():
        line = line.strip()
        if "mCurrentFocus" in line:
            # Window{<hex> u0 <activity-or-null>}
            import re
            m = re.search(r"u0\s+(.+)}$", line)
            focus = m.group(1) if m else line
            return line[:160], focus
    return "", ""


def capture(label, stratum, route, note=None, seq_id=None, wait_ms=1200,
            frames_dir=FRAMES_DIR, uia_dir=UIA_DIR):
    """Take one frame triplet. Returns the content-addressed frameId."""
    if wait_ms:
        time.sleep(wait_ms / 1000.0)
    # 1. screenshot
    raw = adb_bytes(["exec-out", "screencap", "-p"], timeout=180)
    if not raw:
        raise RuntimeError(f"capture {label}: screencap returned empty")
    w, h = png_size(raw)
    if (w, h) != (W, H):
        raise RuntimeError(
            f"capture {label}: unexpected PNG size {w}x{h} (want {W}x{H})")
    sha = hashlib.sha256(raw).hexdigest()[:12]
    frame_id = sha

    # 2. uiautomator dump (retry on transient idle-state failures)
    xml = dump_current_xml()

    # 3. route
    raw_focus, activity = current_focus()

    # 4. persist (dedupe on identical PNG content: reuse id, append seq ref)
    meta_path = os.path.join(frames_dir, frame_id + ".meta.json")
    reused = os.path.exists(os.path.join(frames_dir, frame_id + ".png"))
    utc = datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds")
    if reused and os.path.exists(meta_path):
        meta = json.load(open(meta_path))
    else:
        meta = {
            "frameId": frame_id,
            "stratum": stratum,
            "route": route,
            "activity": activity,
            "captureIndex": int(time.time() * 1000),
            "utc": utc,
        }
        if note:
            meta["note"] = note
        if seq_id:
            meta["sequenceRefs"] = [seq_id] if seq_id not in meta.get("sequenceRefs", []) \
                else meta["sequenceRefs"]
    if seq_id and not reused:
        meta["sequenceRefs"] = [seq_id]
    if seq_id and reused:
        refs = meta.setdefault("sequenceRefs", [])
        if seq_id not in refs:
            refs.append(seq_id)
    if note and "note" not in meta and not reused:
        meta["note"] = note
    meta.setdefault("label", label)

    with open(os.path.join(frames_dir, frame_id + ".png"), "wb") as f:
        f.write(raw)
    if xml is not None:
        with open(os.path.join(uia_dir, frame_id + ".xml"), "wb") as f:
            f.write(xml)
    with open(meta_path, "w") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")

    status = "reused" if reused else "new"
    xml_status = "xml" if xml is not None else "NO-XML"
    print(f"[{status}] {frame_id}  {stratum:12s} {label:28s} {xml_status} "
          f"focus={activity}")
    return frame_id, reused


# --------------------------------------------------------------------------- #
# subcommands
# --------------------------------------------------------------------------- #

def cmd_snap(args):
    if args.route is None:
        args.route = "none"
    capture(args.label, args.stratum, args.route,
            note=args.note, seq_id=args.seq, wait_ms=args.wait_ms)


def cmd_stats(_):
    frames = sorted(f for f in os.listdir(FRAMES_DIR) if f.endswith(".png"))
    by_stratum = {}
    for f in frames:
        stem = f[:-4]
        try:
            meta = json.load(open(os.path.join(FRAMES_DIR, stem + ".meta.json")))
            by_stratum.setdefault(meta.get("stratum", "?"), []).append(stem[:8])
        except Exception:
            by_stratum.setdefault("NO-META", []).append(stem[:8])
    print(f"frames: {len(frames)}")
    for k in sorted(by_stratum):
        print(f"  {k:12s} {len(by_stratum[k])}")
    seqs = sorted(f for f in os.listdir(SEQS_DIR) if f.endswith(".json"))
    print(f"sequences: {len(seqs)}")


def cmd_manifest(_):
    frames = sorted(f for f in os.listdir(FRAMES_DIR) if f.endswith(".png"))
    by_stratum = {}
    frame_lookup = {}
    for f in frames:
        stem = f[:-4]
        meta = json.load(open(os.path.join(FRAMES_DIR, stem + ".meta.json")))
        by_stratum.setdefault(meta.get("stratum", "?"), 0)
        by_stratum[meta["stratum"]] += 1
        frame_lookup[stem] = meta
    seq_ids = sorted(f[:-5] for f in os.listdir(SEQS_DIR) if f.endswith(".json"))
    manifest = {
        "schema": "uniclaw.fastscreenValset.v1",
        "device": {
            "avd": "p26_pixel",
            "serial": SERIAL,
            "resolution": f"{W}x{H}",
            "dpi": 420,
        },
        "collectedUtc": datetime.datetime.now(
            datetime.timezone.utc).isoformat(timespec="seconds"),
        "frameCount": len(frames),
        "byStratum": dict(sorted(by_stratum.items())),
        "sequenceCount": len(seq_ids),
        "sequences": seq_ids,
        "tool": "collect.py",
    }
    with open(MANIFEST, "w") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(json.dumps(manifest, indent=2, ensure_ascii=False))


def cmd_validate(_):
    problems = []
    frames = sorted(f for f in os.listdir(FRAMES_DIR) if f.endswith(".png"))
    for f in frames:
        stem = f[:-4]
        png_path = os.path.join(FRAMES_DIR, f)
        meta_path = os.path.join(FRAMES_DIR, stem + ".meta.json")
        xml_path = os.path.join(UIA_DIR, stem + ".xml")
        if not os.path.exists(meta_path):
            problems.append(f"{f}: missing meta")
            continue
        meta = json.load(open(meta_path))
        if meta.get("frameId") != stem:
            problems.append(f"{f}: meta.frameId mismatch")
        try:
            w, h = png_size(open(png_path, "rb").read())
            if (w, h) != (W, H):
                problems.append(f"{f}: size {w}x{h}")
        except ValueError as e:
            problems.append(f"{f}: not a valid PNG ({e})")
        if not os.path.exists(xml_path):
            problems.append(f"{f}: missing uia xml")
        else:
            xml = open(xml_path, "rb").read()
            if b"<hierarchy" not in xml[:2000]:
                problems.append(f"{f}: uia xml looks empty/invalid")
    seq_ids = sorted(f[:-5] for f in os.listdir(SEQS_DIR) if f.endswith(".json"))
    for sid in seq_ids:
        seq = json.load(open(os.path.join(SEQS_DIR, sid + ".json")))
        for fid in seq.get("frames", []):
            if not os.path.exists(os.path.join(FRAMES_DIR, fid + ".png")):
                problems.append(f"{sid}: references missing frame {fid}")
        kinds = [a["action"] for a in seq.get("actions", [])]
        ok = {"am", "tap", "swipe", "key", "text", "uimode", "notify", "wait"}
        for k in kinds:
            if k not in ok:
                problems.append(f"{sid}: unknown action kind {k}")
    if os.path.exists(MANIFEST):
        m = json.load(open(MANIFEST))
        if m.get("frameCount") != len(frames):
            problems.append("manifest frameCount mismatch")
    print("VALIDATE:", "OK" if not problems else f"{len(problems)} problem(s)")
    for p in problems:
        print("  -", p)
    return 1 if problems else 0


def dump_current_xml(timeout=120):
    """Fresh uiautomator dump with retry; returns bytes or None."""
    for attempt in range(5):
        adb(["shell", "rm", "-f", "/sdcard/window_dump.xml"], check=False, timeout=60)
        adb(["shell", "uiautomator", "dump", "/sdcard/window_dump.xml"],
            check=False, timeout=timeout)
        time.sleep(0.4)
        pul = subprocess.run(
            ADB + ["exec-out", "cat", "/sdcard/window_dump.xml"],
            capture_output=True, timeout=timeout)
        if pul.returncode == 0 and pul.stdout.strip():
            cand = pul.stdout
            if b"<hierarchy" in cand[:2000]:
                return cand
        time.sleep(1.0 + attempt)
    return None


def cmd_find(args):
    """Find a UI node by text/content-desc in the latest dump; print centers."""
    import re
    import xml.etree.ElementTree as ET
    raw = dump_current_xml()
    if raw is None:
        print("(dump failed: could not get idle state)")
        return
    root = ET.fromstring(raw.decode(errors="replace"))
    query = args.text.lower()
    hits = []
    for el in root.iter("node"):
        text = (el.get("text") or "").lower()
        desc = (el.get("content-desc") or "").lower()
        rid = el.get("resource-id", "") or ""
        if (args.text and (query in text or query in desc)) or \
           (args.resource_id and args.resource_id in rid):
            b = el.get("bounds", "")
            m = re.match(r"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", b)
            if m:
                x1, y1, x2, y2 = map(int, m.groups())
                cx, cy = (x1 + x2) // 2, (y1 + y2) // 2
                hits.append((cx, cy, el.get("class"), el.get("text"),
                             el.get("content-desc")))
    if args.first:
        hits = hits[:1]
    for cx, cy, cls, txt, desc in hits:
        print(f"tap {cx} {cy}  class={cls}  text={txt!r}  content-desc={desc!r}")
    print(f"({len(hits)} match(es))")


def cmd_replay(args):
    manifest = json.load(open(MANIFEST))
    out_frames = os.path.join(args.out_dir, "frames")
    out_uia = os.path.join(args.out_dir, "uia")
    os.makedirs(out_frames, exist_ok=True)
    os.makedirs(out_uia, exist_ok=True)
    total = 0
    for sid in manifest["sequences"]:
        if args.only and sid != args.only:
            continue
        seq = json.load(open(os.path.join(SEQS_DIR, sid + ".json")))
        print(f"== replay {sid} ({seq.get('kind')})")
        # base reset: home + wake
        run_action({"action": "am", "params": {"extra": "android.settings.SETTINGS"}})
        time.sleep(1.2)
        if seq.get("dark"):
            run_action({"action": "uimode", "params": {"night": "yes"}})
            time.sleep(1.0)
        for a in seq.get("setup", []):
            run_action(a)
        target = seq["frames"][0]  # frame reached after setup
        for a in seq.get("actions", []):
            if a.get("to") != target:
                # transition boundary: capture the frame reached so far
                meta = json.load(open(os.path.join(FRAMES_DIR, target + ".meta.json")))
                capture(
                    label=meta.get("label", target[:8]),
                    stratum=meta.get("stratum", "?"),
                    route=meta.get("route", "replay"),
                    note="replay" if args.note_replay else None,
                    wait_ms=args.wait_ms,
                    frames_dir=out_frames, uia_dir=out_uia,
                )
                total += 1
                target = a.get("to")
            run_action(a)
        # final frame of the sequence
        meta = json.load(open(os.path.join(FRAMES_DIR, target + ".meta.json")))
        capture(
            label=meta.get("label", target[:8]),
            stratum=meta.get("stratum", "?"),
            route=meta.get("route", "replay"),
            note="replay" if args.note_replay else None,
            wait_ms=args.wait_ms,
            frames_dir=out_frames, uia_dir=out_uia,
        )
        total += 1
        if seq.get("dark"):
            run_action({"action": "uimode", "params": {"night": "no"}})
    print(f"replay done: {total} frames -> {args.out_dir}")


def main():
    global SERIAL, ADB
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--serial", default=SERIAL)
    sub = ap.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("snap", help="capture one frame triplet")
    s.add_argument("--label", required=True)
    s.add_argument("--stratum", required=True,
                   choices=["list", "sidebar", "settings", "dialog",
                            "scrollable", "dense", "text-heavy", "icon-heavy"])
    s.add_argument("--route", default=None)
    s.add_argument("--note", default=None)
    s.add_argument("--seq", default=None)
    s.add_argument("--wait-ms", type=int, default=1200)
    s.set_defaults(fn=cmd_snap)

    sub.add_parser("stats", help="per-stratum frame counts").set_defaults(fn=cmd_stats)
    sub.add_parser("manifest", help="write manifest.json").set_defaults(fn=cmd_manifest)
    sub.add_parser("validate", help="cross-check dataset").set_defaults(fn=cmd_validate)

    f = sub.add_parser("find", help="locate a UI node in the current screen")
    f.add_argument("--text", default=None)
    f.add_argument("--resource-id", default=None)
    f.add_argument("--first", action="store_true")
    f.set_defaults(fn=cmd_find)

    r = sub.add_parser("replay", help="re-walk sequences and re-capture")
    r.add_argument("--out-dir", default=os.path.join(ROOT, "replays", "latest"))
    r.add_argument("--only", default=None)
    r.add_argument("--wait-ms", type=int, default=1200)
    r.add_argument("--note-replay", action="store_true")
    r.set_defaults(fn=cmd_replay)

    args = ap.parse_args()
    SERIAL = args.serial
    ADB = ["adb", "-s", SERIAL]
    sys.exit(args.fn(args) or 0)


if __name__ == "__main__":
    main()