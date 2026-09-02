#!/usr/bin/env python3
"""Type-classification benchmark: VLM element-type judgment vs XML truth.

For each (image, truth-element): ask the served VLM what KIND of element the
text is. Answer must be exactly one of row_title / row_subtitle /
section_label. Score accuracy + per-class precision/recall + confusion.
"""
import json, re, sys, time, urllib.request, base64
from collections import Counter

END = "http://127.0.0.1:8000"
TAG = sys.argv[1] if len(sys.argv) > 1 else "run"
truth = json.load(open("/tmp/ui-tars-bench/type-truth.json"))

PROMPT = (
    "Look at this Android settings screenshot. Find the text '{t}'. "
    "Classify what kind of UI element that text is. Answer with EXACTLY one "
    "word from this list and nothing else:\n"
    "- row_title (the main title of a clickable settings menu row)\n"
    "- row_subtitle (a smaller description line under a menu row title)\n"
    "- section_label (a standalone group/category heading, not clickable)\n"
    "Answer:"
)

def ask(png, t):
    b64 = base64.b64encode(open(png, "rb").read()).decode()
    payload = {
        "model": "bench",
        "messages": [{"role": "user", "content": [
            {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}},
            {"type": "text", "text": PROMPT.format(t=t)},
        ]}],
        "max_tokens": 24, "temperature": 0.0, "top_p": 0.1,
    }
    t0 = time.time()
    req = urllib.request.Request(END + "/v1/chat/completions",
        data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=240) as r:
        body = json.loads(r.read().decode())
    return body["choices"][0]["message"]["content"].strip(), time.time() - t0

VALID = {"row_title", "row_subtitle", "section_label"}
results = []
for img, rows in truth.items():
    png = f"/tmp/ui-tars-bench/{img}.png"
    for r in rows:
        raw, dt = ask(png, r["text"])
        m = re.search(r"(row_title|row_subtitle|section_label)", raw)
        pred = m.group(1) if m else "unparseable"
        results.append({"image": img, "target": r["text"], "truth": r["type"],
                        "pred": pred, "raw": raw[:80], "dt": round(dt, 2)})

# scoring
cls = ["row_title", "row_subtitle", "section_label"]
print(f"\n== TYPE ACCURACY ({TAG}) ==")
ok = sum(1 for r in results if r["pred"] == r["truth"])
print(f"overall: {ok}/{len(results)} = {ok/len(results):.0%}")
for c in cls:
    tp = sum(1 for r in results if r["truth"] == c and r["pred"] == c)
    fp = sum(1 for r in results if r["truth"] != c and r["pred"] == c)
    fn = sum(1 for r in results if r["truth"] == c and r["pred"] != c)
    prec = tp/(tp+fp) if tp+fp else 0
    rec = tp/(tp+fn) if tp+fn else 0
    print(f"  {c:14s} P={prec:5.0%} R={rec:5.0%} (tp={tp} fp={fp} fn={fn})")
unp = sum(1 for r in results if r["pred"] == "unparseable")
print(f"  unparseable: {unp}")
print("\nconfusion (truth -> pred):")
conf = Counter((r["truth"], r["pred"]) for r in results)
for (t, p), n in sorted(conf.items()):
    mark = "" if t == p else "  <-- ERR"
    print(f"  {t:14s} -> {p:14s} x{n}{mark}")
json.dump(results, open(f"/tmp/ui-tars-bench/type-results-{TAG}.json", "w"),
          ensure_ascii=False, indent=1)
print(f"\nsaved: type-results-{TAG}.json")
