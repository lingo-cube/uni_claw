#!/usr/bin/env python3
"""Classify one campaign run's failure path (Phase 2.6 push)."""
import json, sys

P = sys.argv[1] if len(sys.argv) > 1 else "/tmp/p26-p2026-runH-stage.json"

st = json.load(open(P))
tr = [(x.get("reason") or "") for x in st.get("runtimeTrace", [])]

fires = {
    "root_epoch": [], "child_entered": [], "child_epoch": [],
    "settle": [], "left": [], "invalidated": [], "unknown": [],
    "exhausted": [], "completed": [], "other": [],
}
for r in tr:
    if "inventory complete" in r and "SettingsSubpage" in r: fires["child_epoch"].append(r[:120])
    elif "inventory complete" in r: fires["root_epoch"].append(r[:120])
    elif "transition did not settle" in r or "did not settle" in r: fires["settle"].append(r[:130])
    elif "left the container" in r or "left container" in r: fires["left"].append(r[:130])
    elif "INVALIDATED" in r: fires["invalidated"].append(r[:140])
    elif "Unknown interaction" in r: fires["unknown"].append(r[:120])
    elif "exhausted" in r: fires["exhausted"].append(r[:110])
    elif "ContinueExploring" in r or "Completed" in r: fires["completed"].append(r[:120])
    else:
        fires["other"].append(r[:120]) if r else None

acc = [a["sequenceNumber"] for a in st.get("acceptedViewportDecisions", [])]
print(f"run: {P.split('/')[-1]}")
print(f"accepted: {acc}")
for k in ("root_epoch","child_entered","settle","left","invalidated","unknown","exhausted","completed"):
    if fires[k]:
        print(f"== {k} ({len(fires[k])})")
        for x in fires[k][-3:]: print("   ", x)
# last line = terminal signal
print("== last trace lines")
for r in tr[-6:]:
    if r: print("   ", r[:160])