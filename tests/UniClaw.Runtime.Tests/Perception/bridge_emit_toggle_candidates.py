#!/usr/bin/env python3
"""
TEST-ONLY bridge: run the REAL production perception pipeline against a
repo-owned reality fixture and emit the actionable toggle candidates as JSON.

Used by UniClaw.Runtime.Tests/Perception/PerceptionToggleToStateBeliefIntegrationTests.cs
to satisfy the parent 4.2 integration buyer WITHOUT manual candidate injection:
candidate type/bounds originate from the actual production pipeline
(uniclaw_perception.server._run_pipeline → fusion heuristics) applied to the
SAME fixture frame the C# side uses for ImageSwitchStateProvider state
extraction (same-frame identity, section 7).

No production code is imported from a path that mutates anything; this only
reads a fixture and prints structured output.
"""
import json
import os
import sys

from PIL import Image

# Anchor imports to the repository root (the test runner's working directory
# is the build output, not the repo root). The fixture path argument is
# absolute, so derive the repo root from it.
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
sys.path.insert(0, os.path.join(REPO_ROOT, "platforms", "perception"))
from uniclaw_perception import server as perception_server  # noqa: E402


def main() -> int:
    fixture = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        REPO_ROOT, "platforms", "perception", "tests", "fixtures", "reality",
        "developer-options-falsification.png",
    )
    image = Image.open(fixture).convert("RGB")
    perception_server._config = perception_server.load_config()
    evidence, _ = perception_server._run_pipeline(image, image.width, image.height)

    switches = [
        c for c in evidence.get("candidates", [])
        if c.get("type") == "switch"
    ]
    # Emit the minimal durable contract the C# side consumes: pixel bounds
    # (same coordinate space as the fixture PNG) + type + id. switch_state is
    # deliberately NOT authoritative (ImageSwitchStateProvider is).
    out = {
        "fixture": fixture,
        "width": image.width,
        "height": image.height,
        "candidates": [
            {
                "id": c.get("id"),
                "type": c.get("type"),
                "boundsPx": c.get("boundsPx"),
            }
            for c in switches
        ],
    }
    payload = json.dumps(out)
    if len(sys.argv) > 2:
        # Optional second argument: write to a file (avoids stdout pipe
        # buffering/deadlock concerns in the C# test host).
        with open(sys.argv[2], "w", encoding="utf-8") as fh:
            fh.write(payload)
    else:
        sys.stdout.write(payload + "\n")
        sys.stdout.flush()
    return 0


if __name__ == "__main__":
    sys.exit(main())
