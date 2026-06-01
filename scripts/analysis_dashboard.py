#!/usr/bin/env python3
"""Start the traversal analysis dashboard."""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.analysis import run_server


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Traversal Analysis Dashboard")
    parser.add_argument(
        "--trace-dir",
        type=str,
        default=".traces",
        help="Directory containing trace files (default: .traces)"
    )
    parser.add_argument(
        "--host",
        type=str,
        default="127.0.0.1",
        help="Host to bind to (default: 127.0.0.1)"
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8000,
        help="Port to bind to (default: 8000)"
    )

    args = parser.parse_args()

    trace_dir = Path(args.trace_dir)

    if not trace_dir.exists():
        print(f"⚠️  Trace directory '{trace_dir}' does not exist. Creating it...")
        trace_dir.mkdir(parents=True, exist_ok=True)

    run_server(trace_dir, args.host, args.port)


if __name__ == "__main__":
    main()
