#!/usr/bin/env python3
"""RUN-006 directive-follower double (leader-file decision bridge responder).

Tier downgrade: the real DSH model bridge (dedicated instance on 3081) could
not be started from this session (sandbox denies ~/.dsh profile writes), so
this responder answers each Coverage Director directive literally, per the
SETTINGS-COVERAGE-003/004 leader-pass precedents. It invents no targets and
never taps confirm-side controls; unknown-route directives are answered with
defer (SETTINGS-COVERAGE-004 A1/A2 semantics).
"""
import json
import os
import re
import sys
import time
import glob

INBOX = sys.argv[1]
DIRECTIVE_RE = re.compile(r"COVERAGE DIRECTIVE \(authoritative for this turn\): (.+)")
TARGET_RE = re.compile(r"'<(.+?)>'")


def elements(req):
    return (req.get("context") or {}).get("elements") or []


def role_for(req, desc):
    for el in elements(req):
        if el.get("text") == desc:
            role = el.get("role")
            if role in ("ui.element", "scrollable", "switch"):
                return role
    return "ui.element"


def in_elements(req, desc):
    return any(el.get("text") == desc for el in elements(req))


def decide(req):
    obj = (req.get("context") or {}).get("objective", "")
    m = DIRECTIVE_RE.search(obj)
    did = req["decisionId"]
    if not m:
        return {"kind": "defer", "decisionId": did}
    d = m.group(1).strip()
    t = TARGET_RE.search(d)
    if d.startswith("Observe again"):
        # unknown-route: only a correlated Defer is accepted (SETTINGS-COVERAGE-004)
        return {"kind": "defer", "decisionId": did, "spec": {}}
    if not t:
        return {"kind": "defer", "decisionId": did, "spec": {}}
    desc = t.group(1)
    if d.startswith("Scroll"):
        role = role_for(req, desc)
        step = {"targetRole": role, "targetDescriptor": desc, "effectClass": "swipe-up"}
    else:
        role = role_for(req, desc)
        step = {"targetRole": role, "targetDescriptor": desc, "effectClass": "tap"}
    note = "" if in_elements(req, desc) else " [target not in live element list; host guard authoritative]"
    return {
        "kind": "act",
        "decisionId": did,
        "proposal": {
            "decisionId": did,
            "steps": [step],
            "justification": "RUN-006 directive-follower double: executed the coverage directive literally" + note,
        },
    }


def main():
    os.makedirs(INBOX, exist_ok=True)
    print(f"[follower] watching {INBOX}", flush=True)
    seen = set()
    idle = 0
    while True:
        progressed = False
        for f in sorted(glob.glob(os.path.join(INBOX, "request-*.json"))):
            base = os.path.basename(f)[len("request-"):-len(".json")]
            if base in seen:
                continue
            resp = os.path.join(INBOX, f"response-{base}.json")
            if os.path.exists(resp):
                seen.add(base)
                continue
            try:
                req = json.load(open(f))
            except Exception as e:
                print(f"[follower] skip unreadable {base}: {e}", flush=True)
                continue
            decision = decide(req)
            tmp = resp + ".tmp"
            with open(tmp, "w") as fh:
                json.dump(decision, fh, ensure_ascii=False)
            os.replace(tmp, resp)
            seen.add(base)
            progressed = True
            print(f"[follower] answered {base} -> {decision['kind']}", flush=True)
        if not progressed:
            idle += 1
            if idle > 360:  # ~30 min without new requests
                print("[follower] idle timeout, exiting", flush=True)
                return
            time.sleep(5)
        else:
            idle = 0
            time.sleep(0.3)


if __name__ == "__main__":
    main()
