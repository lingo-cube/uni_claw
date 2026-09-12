#!/usr/bin/env python3
"""Benchmark raw RGBA vs JPEG paths for the canonical perception service.

Usage:
  UNICLAW_YOLO_MODEL=platforms/perception/models/yolo/android_ui_detection_yolov8/best.pt \
  python3 platforms/perception/cli/benchmark_raw.py --image artifacts/assets/screenshots/settings-home-api35-full-20260803.png --runs 100
"""

from __future__ import annotations

import argparse
import json
import os
import statistics
import sys
import time
from io import BytesIO
from pathlib import Path

import requests
from PIL import Image

# Add tools/ to path for server imports if needed
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))


def bench_endpoint(
    url: str,
    body: bytes,
    headers: dict[str, str],
    runs: int,
    warmup: int = 3,
) -> list[float]:
    """Benchmark an endpoint with warmup."""
    latencies: list[float] = []

    # Warmup
    for _ in range(warmup):
        requests.post(url, data=body, headers=headers, timeout=30)

    # Measure
    for i in range(runs):
        t0 = time.perf_counter()
        resp = requests.post(url, data=body, headers=headers, timeout=30)
        elapsed = (time.perf_counter() - t0) * 1000
        if resp.status_code != 200:
            print(f"  [{i+1}/{runs}] ERROR: HTTP {resp.status_code}: {resp.text[:200]}")
            continue
        latencies.append(elapsed)
        if (i + 1) % 20 == 0:
            print(f"  [{i+1}/{runs}] last={elapsed:.1f}ms")

    return latencies


def print_stats(name: str, latencies: list[float]) -> dict:
    """Print and return percentile stats."""
    if not latencies:
        print(f"\n{name}: NO DATA")
        return {}

    sorted_lat = sorted(latencies)
    stats = {
        "count": len(latencies),
        "mean": statistics.mean(latencies),
        "p50": sorted_lat[int(len(sorted_lat) * 0.50)],
        "p95": sorted_lat[int(len(sorted_lat) * 0.95)],
        "p99": sorted_lat[int(len(sorted_lat) * 0.99)],
        "min": min(latencies),
        "max": max(latencies),
    }
    print(f"\n{name}:")
    print(f"  count={stats['count']}  mean={stats['mean']:.1f}ms")
    print(f"  P50={stats['p50']:.1f}ms  P95={stats['p95']:.1f}ms  P99={stats['p99']:.1f}ms")
    print(f"  min={stats['min']:.1f}ms  max={stats['max']:.1f}ms")
    return stats


def main():
    parser = argparse.ArgumentParser(description="Benchmark raw RGBA vs JPEG vision paths")
    parser.add_argument("--image", required=True, type=Path, help="Input screenshot PNG/JPEG")
    parser.add_argument("--runs", default=100, type=int, help="Number of benchmark runs per path")
    parser.add_argument("--base-url", default="http://127.0.0.1:8765", help="Vision server base URL")
    parser.add_argument("--crop-top", default=0.0625, type=float, help="Crop top ratio")
    parser.add_argument("--crop-bottom", default=0.0625, type=float, help="Crop bottom ratio")
    parser.add_argument("--max-width", default=720, type=int, help="Max width for resize")
    args = parser.parse_args()

    img = Image.open(args.image).convert("RGBA")
    print(f"Image: {args.image}")
    print(f"Size: {img.width}x{img.height}  Mode: {img.mode}")
    print(f"Runs: {args.runs} per path")
    print(f"Server: {args.base_url}")

    # Check server health
    try:
        health = requests.get(f"{args.base_url}/health", timeout=5)
        print(f"Health: {health.json()}")
    except Exception as e:
        print(f"ERROR: Cannot reach server at {args.base_url}: {e}")
        print("Start with: python3 -m uvicorn uniclaw_perception.server:app --app-dir platforms/perception --host 127.0.0.1 --port 8765")
        return 1

    # ── Prepare JPEG body (old path) ──
    img_rgb = img.convert("RGB")
    jpeg_buf = BytesIO()
    img_rgb.save(jpeg_buf, format="JPEG", quality=85)
    jpeg_bytes = jpeg_buf.getvalue()
    print(f"\nJPEG body: {len(jpeg_bytes)} bytes ({len(jpeg_bytes)/1024:.0f} KB)")

    # ── Prepare raw RGBA body (new path) ──
    raw_bytes = img.tobytes()
    print(f"RGBA body: {len(raw_bytes)} bytes ({len(raw_bytes)/1024:.0f} KB)")

    # ── Benchmark JPEG path ──
    print("\n── JPEG path (/v1/analyze) ──")
    jpeg_latencies = bench_endpoint(
        f"{args.base_url}/v1/analyze",
        jpeg_bytes,
        {"Content-Type": "image/jpeg"},
        args.runs,
    )

    # ── Benchmark raw path ──
    print("\n── Raw RGBA path (/v1/analyze_raw) ──")
    raw_latencies = bench_endpoint(
        f"{args.base_url}/v1/analyze_raw",
        raw_bytes,
        {
            "Content-Type": "application/octet-stream",
            "X-Image-Width": str(img.width),
            "X-Image-Height": str(img.height),
        },
        args.runs,
    )

    # ── Results ──
    jpeg_stats = print_stats("JPEG (/v1/analyze)", jpeg_latencies)
    raw_stats = print_stats("Raw RGBA (/v1/analyze_raw)", raw_latencies)

    if jpeg_stats and raw_stats:
        delta_p50 = raw_stats["p50"] - jpeg_stats["p50"]
        delta_p95 = raw_stats["p95"] - jpeg_stats["p95"]
        print(f"\n── Delta (raw - jpeg) ──")
        print(f"  P50: {delta_p50:+.1f}ms")
        print(f"  P95: {delta_p95:+.1f}ms")

        # Size comparison
        ratio = len(raw_bytes) / len(jpeg_bytes)
        print(f"\n── Size ──")
        print(f"  Raw/JPEG ratio: {ratio:.1f}x")
        print(f"  Raw overhead: {len(raw_bytes) - len(jpeg_bytes):,} bytes")

        # frombytes micro-benchmark
        print(f"\n── frombytes micro-benchmark ──")
        from io import BytesIO as BIO
        t0 = time.perf_counter()
        for _ in range(100):
            _img = Image.frombytes("RGBA", (img.width, img.height), raw_bytes)
        frombytes_us = (time.perf_counter() - t0) / 100 * 1_000_000
        print(f"  frombytes: {frombytes_us:.1f} µs/call")

        t0 = time.perf_counter()
        for _ in range(100):
            _img = Image.open(BIO(jpeg_bytes))
        open_us = (time.perf_counter() - t0) / 100 * 1_000_000
        print(f"  open(JPEG): {open_us:.1f} µs/call")

        return 0 if delta_p50 <= 0 else 0  # always 0 — informative only

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
