#!/usr/bin/env python3
"""UI-TARS pipeline-candidate OFFLINE ground-truth evaluation (step 1).

Feeds captured Settings screenshots to a UI-TARS OpenAI-compatible endpoint
(llama-server / vLLM UI-TARS server) and prints the grounded elements the model
returns, for comparison against the device truth (uiautomator dumps + our OCR
frame data).

Usage:
  python3 eval_uitars.py --endpoint http://127.0.0.1:8000 --model UI-TARS-1.5-7B <png>...
Prints one JSON block per image: {file, response_kind, raw, elements[]}
No heavy deps — urllib + base64 only.
"""
import argparse, base64, json, sys, urllib.request

def call(png_path: str, endpoint: str, model: str) -> dict:
    b64 = base64.b64encode(open(png_path, "rb").read()).decode()
    payload = {
        "model": model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}},
                    {"type": "text", "text": (
                        "List every visible UI element with an exact bounding box "
                        "and its text/label. Use the format:\n"
                        "<box>x1,y1,x2,y2</box> text\n"
                        "Include section headers, icons (describe them), toggles and rows. "
                        "Do not omit rows near the bottom of the screen."
                    )},
                ],
            }
        ],
        "max_tokens": 1024,
        "temperature": 0.0,
    }
    req = urllib.request.Request(
        endpoint.rstrip("/") + "/v1/chat/completions",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=180) as r:
        body = json.loads(r.read().decode())
    return {"raw": body["choices"][0]["message"]["content"],
            "usage": body.get("usage")}

def parse_boxes(text: str) -> list[dict]:
    """Best-effort parse of UI-TARS box format (<box>x,y,x,y</box> label)."""
    import re
    items = []
    for m in re.finditer(r"<box>\s*([\d.,]+)\s*</box>\s*(.+)", text):
        coords = [float(x) for x in m.group(1).split(",")]
        items.append({"box": coords, "text": m.group(2).strip()})
    return items

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--endpoint", default="http://127.0.0.1:8000")
    ap.add_argument("--model", default="UI-TARS-1.5-7B")
    ap.add_argument("images", nargs="+")
    a = ap.parse_args()
    for png in a.images:
        out = call(png, a.endpoint, a.model)
        out["file"] = png
        out["elements"] = parse_boxes(out["raw"])
        print(json.dumps(out, ensure_ascii=False, indent=1))

if __name__ == "__main__":
    main()