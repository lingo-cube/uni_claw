#!/usr/bin/env python3
"""UI-TARS grounding BENCHMARK vs paired uiautomator truth (pixel space).

For each (image, truth-row): query the model for the row's text; classify the
answer as HIT (box y within tolerance of truth center-y), MISS (box far),
or NOT-VISIBLE; accumulate latency/tokens. Pixel space = 1080x2400.
"""
import json, re, sys, time, urllib.request, base64

END = "http://127.0.0.1:8000"
MODEL = "UI-TARS-2B-SFT"
truth = json.load(open("/tmp/ui-tars-bench/truth.json"))

def ask(png, target):
    b64 = base64.b64encode(open(png, "rb").read()).decode()
    payload = {
        "model": MODEL,
        "messages": [{"role": "user", "content": [
            {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}},
            {"type": "text", "text": (
                f"In this UI screenshot, locate the element with the text '{target}'. "
                "Answer <box>(x1,y1,x2,y2)</box> with the text. If not visible: 'not visible'."
            )},
        ]}],
        "max_tokens": 64, "temperature": 0.0, "top_p": 0.1,
    }
    t0 = time.time()
    req = urllib.request.Request(END + "/v1/chat/completions", data=json.dumps(payload).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=240) as r:
        body = json.loads(r.read().decode())
    return body["choices"][0]["message"]["content"], time.time() - t0

def parse_box(raw):
    m = re.search(r"<box>\s*\(?([\d.,]+)\)?\s*</box>", raw) or re.search(r"\(([\d,]+)\)", raw)
    if not m: return None
    nums = [float(x) for x in m.group(1).split(",")]
    if len(nums) == 4: return nums
    if len(nums) == 2: return [nums[0], nums[1], nums[0], nums[1]]
    return None

results = []
summary = {}
for img, rows in truth.items():
    png = f"/tmp/ui-tars-bench/{img}.png"
    per_img = {"visible_truth": len(rows), "hit100": 0, "hit200": 0, "miss": 0, "notvisible": 0}
    for r in rows:
        raw, dt = ask(png, r["text"])
        box = parse_box(raw)
        low = raw.lower()
        status = "notvisible" if "not visible" in low else ("box" if box else "unparseable")
        if status == "box":
            cy = (box[1] + box[3]) / 2
            dy = abs(cy - r["cy"])
            if dy <= 100: status = "hit100"
            elif dy <= 200: status = "hit200"
            else: status = "miss"
        per_img[status if status in ("hit100","hit200","miss","notvisible") else "unparseable"] = \
            per_img.get(status if status in ("hit100","hit200","miss","notvisible") else "unparseable", 0) + 1
        results.append({"image": img, "target": r["text"], "truth_y": r["cy"],
                        "raw": raw[:80], "dt": round(dt, 1), "status": status})
        print(f"[{img}] '{r['text'][:36]}' truth_y={r['cy']} -> {status}{' dy=' + str(abs((parse_box(raw)[1]+parse_box(raw)[3])/2 - r['cy']) if parse_box(raw) else '')}")
    summary[img] = per_img

json.dump({"results": results, "summary": summary}, open("/tmp/ui-tars-bench/bench-results.json", "w"),
          ensure_ascii=False, indent=1)
print("\n== SUMMARY =="); print(json.dumps(summary, ensure_ascii=False, indent=1))