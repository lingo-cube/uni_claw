#!/usr/bin/env python3
"""Architecture design persistence checks. Does not execute product or certify it."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]


def main():
    target = HERE / "verification.json"
    target.write_text(json.dumps({"result": "CHECKING", "scope": "DESIGN_ONLY"}, indent=2) + "\n")
    state_path = ROOT / "changes/RUNTIME-PLAN-002/state.md"
    state = state_path.read_text()
    flow = (ROOT / "changes/RUNTIME-PLAN-002/flow-contract.md").read_text()
    review = (ROOT / "changes/RUNTIME-PLAN-002/architecture-review.md").read_text()
    assert re.search(r"^lifecycle_state: persisted · disposition: none", state, re.M)
    assert "设计基线已接受并持久化" in state and "等待Owner主动开启后续任务" in state
    assert all(f"D{i}" in state for i in range(1, 20))
    assert "Traversal Control和Recovery都是Control Loop" in flow
    assert "handler不直接改写这些状态" in flow
    assert "driver只编排" in review and "不自动进入PLAN/IMPLEMENT" in review

    entry = json.loads((HERE / "entry.json").read_text())
    for name, digest in entry["review_sources"].items():
        assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == digest, name
    index = (ROOT / "changes/INDEX.md").read_text()
    before_index = (HERE / "before/changes/INDEX.md").read_text()
    rows = lambda text: [line for line in text.splitlines() if line.startswith("| ")]
    unrelated = lambda text: [line for line in rows(text) if not line.startswith("| RUNTIME-PLAN-002 |")]
    assert unrelated(index) == unrelated(before_index)
    assert sum(line.startswith("| RUNTIME-PLAN-002 | persisted |") for line in rows(index)) == 1
    assert "> 统计：206 changes · 11 open。" in index

    commands = [
        [sys.executable, "evidence/runtime-plan-002/verify-docs.py"],
        ["git", "diff", "--check", "--", "CONTEXT.md", "changes/INDEX.md", "changes/RUNTIME-PLAN-002"],
    ]
    checks = []
    for command in commands:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
        checks.append(dict(command=command, exit_code=result.returncode, stdout=result.stdout, stderr=result.stderr))
        assert result.returncode == 0, checks[-1]

    links, examples = 0, 0
    docs = list((ROOT / "changes/RUNTIME-PLAN-002").glob("*.md"))
    for p in docs:
        text = p.read_text()
        assert len(re.findall(r"^```", text, re.M)) % 2 == 0, str(p)
        assert all(line == line.rstrip() for line in text.splitlines()), str(p)
        for link in re.findall(r"(?<!!)\[[^\]]+\]\(([^)]+)\)", text):
            if link.startswith("#") or re.match(r"^[a-z]+:", link):
                continue
            assert (p.parent / link.split("#", 1)[0]).resolve().exists(), (p, link)
            links += 1
        for example in re.findall(r"```json\s*\n(.*?)\n```", text, re.S):
            json.loads(example)
            examples += 1
    compile(Path(__file__).read_text(), str(__file__), "exec")
    tracked = ["CONTEXT.md", "changes/INDEX.md", "changes/RUNTIME-PLAN-002/state.md",
               "changes/RUNTIME-PLAN-002/flow-contract.md", "changes/RUNTIME-PLAN-002/architecture-review.md",
               "changes/RUNTIME-PLAN-002/discussion-reference.md", "changes/RUNTIME-PLAN-002/protocol-sketch.md",
               "evidence/runtime-plan-002/verify-docs.py"]
    result = dict(
        kind="ARCHITECTURE_DESIGN_REVIEW_AND_PERSISTENCE", result="PASS_DESIGN_PERSISTED",
        method="architecture-review.md principles/authority matrix plus source hashes, doc links, generated index and state checks",
        expected="single responsibility and explicit Owner seams; persisted accepted design; no automatic implementation",
        actual=dict(design_principles="SATISFIED_AT_DESIGN_LEVEL", blocking_architecture_findings=0,
            clarified_owner_statements=2, lifecycle_state="persisted", design_decisions="D1-D19",
            links_checked=links, json_examples_syntax_checked=examples, unrelated_index_rows_preserved=True,
            awaiting="Owner explicitly opens next task"),
        evidence=["changes/RUNTIME-PLAN-002/architecture-review.md", "entry.json", "before/"],
        checks=checks,
        current_artifacts={name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in tracked},
        review_limit="logical architecture and documented contracts, not implementation conformance or universal completeness",
        future_implementation_obligations=[f"K{i}" for i in range(1, 9)],
        product_implementation_changed=False, product_tests_run=False, model_or_device_runs=0,
        old_derivation_regraded=False, commits_or_pushes=False, background_or_new_task_started=False)
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({key: result[key] for key in ["result", "actual", "review_limit"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
