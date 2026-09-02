#!/usr/bin/env python3
"""UI-TARS targeted grounding eval — per-disease-row queries.

UI-TARS is an instruction-grounding model: ask for ONE element at a time in
action style. For each (image, target) we ask "locate ... in this UI
screenshot" and record found/box/latency/raw — the exact KPIs for the Phase 2.6
disease families (Wallpaper short-read, bottom-row omission, section headers,
long captions, textless icons).
"""
import argparse, base64, json, time, urllib.request

UA_API = "http://127.0.0.1:8000"

def ask(png_path: str, target: str, model: str, max_tokens: int = 256) -> tuple[str, float, int]:
    b64 = base64.b64encode(open(png_path, "rb").read()).decode()
    payload = {
        "model": model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}},
                    {"type": "text", "text": (
                        f"In this UI screenshot, what is the position of the element with the text "
                        f"'{target}'? Answer with <box>(x1,y1,x2,y2)</box> and the element's text. "
                        "If it is not visible, answer: not visible."
                    )},
                ],
            }
        ],
        "max_tokens": max_tokens,
        "temperature": 0.0,
        "top_p": 0.1,
    }
    t0 = time.time()
    req = urllib.request.Request(UA_API + "/v1/chat/completions",
                                 data=json.dumps(payload).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=240) as r:
        body = json.loads(r.read().decode())
    dt = time.time() - t0
    return body["choices"][0]["message"]["content"], dt, body.get("usage", {}).get("total_tokens", 0)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="UI-TARS-2B-SFT")
    ap.add_argument("--out", default="/tmp/ui-tars-eval/targeted-results.json")
    ap.add_argument("targets", nargs="*")  # optional CLI overrides; else use built-in table
    a = ap.parse_args()

    E = "/Users/fran/Documents/Code/spacex/uni_claw/openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/artifacts/uitars-eval"
    table = [
        ("root-top.png",      "Wallpaper & style"),
        ("root-top.png",      "Accessibility"),
        ("root-top.png",      "Display"),
        ("root-top.png",      "Sound & vibration"),
        ("root-scrolled.png", "Wallpaper & style"),
        ("root-scrolled.png", "Home, lock screen, & security"),
        ("display-child.png", "Screen timeout"),
        ("display-child.png", "Dark theme"),
        ("display-child.png", "Will never turn on automatically"),
        ("accessibility.png", "Interaction controls"),
        ("accessibility.png", "Captions"),
        ("accessibility.png", "Audio"),
        ("accessibility.png", "Audio description"),
        ("accessibility.png", "Flash notifications"),
        ("accessibility.png", "Hear a description of what's happening in supported movies and shows"),
    ]
    rows = []
    for img, target in table:
        raw, dt, toks = ask(f"{E}/{img}", target, a.model)
        rows.append({"image": img, "target": target, "latency_s": round(dt, 1), "tokens": toks, "raw": raw[:300]})
        print(f"[{img}] '{target}' ({dt:.1f}s) -> {raw[:120]!r}")
    json.dump(rows, open(a.out, "w"), ensure_ascii=False, indent=1)
    print("\nwrote", a.out)

if __name__ == "__main__":
    main()