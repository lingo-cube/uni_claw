#!/usr/bin/env python3
"""仅校验本次文档交付；不运行或判定产品适配。"""
import json
from pathlib import Path
import re
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
DOCS = ["changes/RUNTIME-PLAN-002/state.md",
        "changes/RUNTIME-PLAN-002/discussion-reference.md",
        "changes/RUNTIME-PLAN-001/state.md",
        "changes/RUNTIME-PLAN-002/flow-contract.md",
        "changes/RUNTIME-PLAN-002/protocol-sketch.md"]


def main():
    entry = json.loads((HERE / "entry/snapshot.json").read_text())
    backup = Path(entry["backup"])
    report = dict(kind="DOCUMENT_DELIVERY_ONLY", change="RUNTIME-PLAN-002",
                  lifecycle_state="RESOLVE", final_contract_confirmed=False,
                  product_implementation_changed=False, product_tests_run=False,
                  model_or_device_runs=0, links_checked=0, missing_links=[])
    result_path = HERE / "verification.json"
    if not result_path.exists():
        result_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    for name in DOCS:
        p = ROOT / name
        text = p.read_text()
        assert len(re.findall(r"^```", text, re.M)) % 2 == 0, name
        assert all(line == line.rstrip() for line in text.splitlines()), name
        for target in re.findall(r"(?<!!)\[[^\]]+\]\(([^)]+)\)", text):
            if target.startswith("#") or re.match(r"^[a-z]+:", target):
                continue
            file = target.split("#", 1)[0]
            if not file:
                continue
            report["links_checked"] += 1
            if not (p.parent / file).resolve().exists():
                report["missing_links"].append(dict(source=name, target=target))
    assert not report["missing_links"], report["missing_links"]
    state = (ROOT / DOCS[0]).read_text()
    reference = (ROOT / DOCS[1]).read_text()
    assert "lifecycle_state: RESOLVE" in state
    assert all(x in state for x in ["## Intent", "## Acceptance", "## Verification", "## Status log"])
    # RESOLVE及未确认契约由state判定，不要求草案使用特定“待敲定”措辞。
    assert all(x in reference for x in ["输入", "执行", "输出", "不是已实现协议"])
    index = (ROOT / "changes/INDEX.md").read_text()
    old_index = (backup / "changes/INDEX.md").read_text()
    old_rows = [x for x in old_index.splitlines() if x.startswith("| ")]
    new_rows = [x for x in index.splitlines() if x.startswith("| ")]
    assert [x for x in new_rows if not x.startswith("| RUNTIME-PLAN-002 |")] == old_rows
    assert sum(x.startswith("| RUNTIME-PLAN-002 | RESOLVE |") for x in new_rows) == 1
    assert "[RUNTIME-PLAN-002]" in (ROOT / DOCS[2]).read_text()
    command = ["git", "diff", "--check", "--", *DOCS, "changes/INDEX.md"]
    p = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
    assert p.returncode == 0, p.stderr + p.stdout
    report.update(result="PASS_DOCUMENT_CHECKS_ONLY", old_index_rows_preserved=True,
                  status_and_index_consistent=True, fences_and_whitespace="PASS",
                  diff_check=dict(command=command, exit_code=p.returncode,
                                  stdout=p.stdout, stderr=p.stderr))
    result_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
