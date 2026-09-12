#!/usr/bin/env python3
"""PHASE26 SLOW VLM SHADOW benchmark — fresh linked assets only.

Consumes ONLY the fresh screenshots with paired uiautomator truth
(/tmp/ui-tars-bench/*.png + truth.json), asks the SHADOW VLM (UI-TARS 2B,
llama-server on :8000) per case, and emits a three-way table
(human/truth vs fast-perception vs slow-VLM-proposal) with role verdicts.
Shadow-only: proposals never touch runtime authority.
"""
import base64, json, re, time, urllib.request

END = "http://127.0.0.1:8000"; MODEL = "UI-TARS-2B-SFT"
TRUTH = json.load(open("/tmp/ui-tars-bench/truth.json"))
IMG = "/tmp/ui-tars-bench"

def ask(png, text, extra=""):
    b64 = base64.b64encode(open(png, "rb").read()).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}},
        {"type": "text", "text": (
            f"In this UI screenshot: locate '{text}'. Answer <box>(x1,y1,x2,y2)</box> + the text. "
            f"Then classify its role in one word from: title,section-header,row,toggle,description,search. {extra}")},
    ]}], "max_tokens": 96, "temperature": 0.0, "top_p": 0.1}
    t0 = time.time()
    req = urllib.request.Request(END + "/v1/chat/completions", data=json.dumps(payload).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=240) as r:
        body = json.loads(r.read().decode())
    return body["choices"][0]["message"]["content"], round(time.time() - t0, 1), body.get("usage", {}).get("total_tokens", 0)

# ── bench set: (family, png, target, truth_row?, expected_role) ──
CASES = [
    # POSITIVE
    ("OCR_CORRUPTION", "accessibility.png", "Hear a description of what's happening in supported movies and shows", "Hear a description of what’s happening on screen in supported movies and shows", "description"),
    ("WALLPAPER_SHORTREAD", "root-scrolled.png", "Wallpaper", "Wallpaper", "row"),
    ("BLUETOOTH_FRAGMENT", "root-top.png", "Bluetooth, pairing", "Bluetooth, pairing", "row"),
    ("SECTION_HEADER", "accessibility.png", "Captions", "Captions", "section-header"),
    ("SECTION_HEADER2", "accessibility.png", "Audio", "Audio", "section-header"),
    ("DEEP_DESCRIPTION", "display-child.png", "Will never turn on automatically", "Will never turn on automatically", "description"),
    ("SUB_VALUE", "display-child.png", "Not set", "Not set", "description"),
    # NEGATIVE / CONTROL
    ("ORDINARY_ROW", "root-top.png", "Notifications", "Notifications", "row"),
    ("ORDINARY_ROW2", "root-scrolled.png", "Security & privacy", "Security & privacy", "row"),
    ("ORDINARY_ROW3", "display-child.png", "Screen timeout", "Screen timeout", "row"),
    ("TOGGLE", "accessibility.png", "On", "On", "toggle"),
    ("ROOT_TITLE", "root-top.png", "Settings", "Settings", "title"),
    ("SEARCH", "root-top.png", "Search settings", "Search settings", "search"),
]
rows = []
for fam, png, target, truth_text, exp_role in CASES:
    raw, dt, toks = ask(f"{IMG}/{png}", target)
    box = re.findall(r"<box>\s*\(?([\d.,]+)\)?\s*</box>", raw)
    role = re.findall(r"\b(title|section-header|row|toggle|description|search)\b", raw)
    truth_band = next((r["cy"] for r in TRUTH[png[:-4]] if truth_text.lower().replace(" ","") in r["text"].lower().replace(" ","")), None)
    rows.append({"family": fam, "image": png, "target": target, "truth_present": truth_band is not None,
                 "truth_cy": round(truth_band,0) if truth_band else None, "expected_role": exp_role,
                 "vlm_raw": raw[:90], "vlm_box": box[:1], "vlm_role": role[:1], "latency_s": dt, "tokens": toks})
    print(f"[{fam}] {target[:44]} truth={'Y' if truth_band is not None else '-'} exp={exp_role} vlm_role={role[:1]} lat={dt}s")
json.dump(rows, open("/tmp/ui-tars-bench/shadow-bench.json", "w"), ensure_ascii=False, indent=1)
print("\nwrote /tmp/ui-tars-bench/shadow-bench.json")