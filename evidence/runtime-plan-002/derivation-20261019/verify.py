#!/usr/bin/env python3
"""Verify analysis artifacts, not product conformance."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]


def main():
    path = HERE / "verification.json"
    path.write_text(json.dumps({"result": "CHECKING", "product_verdict": "NOT_PROVEN"}, indent=2) + "\n")
    commands = [[sys.executable, str(HERE / "audit.py"), "--check"],
                [sys.executable, str(ROOT / "evidence/runtime-plan-002/verify-docs.py")],
                ["git", "diff", "--check", "--", "changes/RUNTIME-PLAN-002", "CONTEXT.md"]]
    checks = []
    for command in commands:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
        checks.append(dict(command=command, exit_code=result.returncode,
                           stdout=result.stdout, stderr=result.stderr))
        assert result.returncode == 0, checks[-1]

    links, json_examples = 0, 0
    documents = list((ROOT / "changes/RUNTIME-PLAN-002").glob("*.md")) + [HERE / "scenario-matrix.md"]
    for p in documents:
        text = p.read_text()
        assert all(line == line.rstrip() for line in text.splitlines()), str(p)
        assert len(re.findall(r"^```", text, re.M)) % 2 == 0, str(p)
        for target in re.findall(r"(?<!!)\[[^\]]+\]\(([^)]+)\)", text):
            if target.startswith("#") or re.match(r"^[a-z]+:", target):
                continue
            file = target.split("#", 1)[0]
            assert (p.parent / file).resolve().exists(), (p, target)
            links += 1
        for block in re.findall(r"```json\s*\n(.*?)\n```", text, re.S):
            json.loads(block)
            json_examples += 1

    for name in ["audit.py", "check-model.py", "verify.py"]:
        compile((HERE / name).read_text(), name, "exec")
    inventory = json.loads((HERE / "inventory.json").read_text())
    model = json.loads((HERE / "model-results.json").read_text())
    assert model["result"] == "PASS_REFERENCE_CHECKS_ONLY"
    assert model["graph_root_depth_cases"] == 6244 and model["three_valued_rule_cases"] == 27
    assert model["naive_visited_counterexample"]["missed"] == [3]
    assert model["product_executions"] == 0
    assert inventory["summary"]["product_scenarios_executed"] == 0
    assert all(row["implementation"] == "UNVERIFIED" for row in inventory["library"])
    assert len({row["id"] for row in inventory["handler_cases"]}) == 16

    def fingerprint(p):
        return hashlib.sha256(p.read_bytes()).hexdigest()
    artifacts = [HERE / x for x in ["audit.py", "check-model.py", "model-results.json", "inventory.json", "scenario-matrix.md"]]
    artifacts += [ROOT / "changes/RUNTIME-PLAN-002" / x for x in
                  ["derivation-review.md", "mathematical-derivation.md", "philosophical-derivation.md"]]
    result = dict(
        kind="DERIVATION_REVIEW_ONLY", result="PASS_STATIC_AND_REFERENCE_CHECKS_ONLY",
        baseline="D1-D16 used with owner authorization; no new design decisions silently accepted",
        level="CONTRACT_WITH_BOUNDED_REFERENCE_MODELS",
        method="fresh input parsing and hashes; explicit scenario mapping; conditional proofs/counterexamples; bounded reference checks; links and syntax",
        expected="no input omission or inherited product PASS; scope/theorem assumptions explicit; counterexamples reproducible; documents recoverable",
        actual=dict(**inventory["summary"], graph_reference_cases=model["graph_root_depth_cases"],
                    rule_reference_cases=model["three_valued_rule_cases"],
                    reference_counterexamples_reproduced=["naive global visited", "Unknown treated as false"],
                    links_checked=links, json_examples_syntax_checked=json_examples,
                    unresolved_contract_obligations=[f"K{i}" for i in range(1, 9)]),
        evidence=["inventory.json", "scenario-matrix.md", "model-results.json", "entry.json"],
        checks=checks, artifacts={str(p.relative_to(ROOT)): fingerprint(p) for p in artifacts},
        paper_sources=[dict(title="Turing 1936 original paper", url="https://www.cs.virginia.edu/~robins/Turing_Paper_1936.pdf",
                            use="universal computation and undecidability boundary; no claim it proves product correctness"),
                       dict(title="Lamport 1977 original paper", url="https://lamport.azurewebsites.net/pubs/proving.pdf",
                            use="safety and liveness distinction; project-specific deductions are authored here")],
        product_verdict="NOT_PROVEN", formal_prover_used=False,
        new_mechanism_product_tests_run=False, model_or_device_runs=0,
        authority_or_protocol_changed=False, lifecycle_state="RESOLVE")
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({k: result[k] for k in ["result", "actual", "product_verdict"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
