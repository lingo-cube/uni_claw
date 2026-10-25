#!/usr/bin/env python3
"""Read-only compact verification; no product tests, services or cleanup."""
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
from test_assets import validate_run


def main():
    checks = {}
    for item in json.loads((HERE / 'candidate.json').read_text())['files']:
        path = ROOT / item['path']
        checks['source:' + item['path']] = path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == item['sha256']
    for name, count in [('layout-contracts-green-final.txt', 11), ('entry-contracts-green-final.txt', 15)]:
        output = (HERE / name).read_text()
        checks[name] = f'Ran {count} tests' in output and output.rstrip().endswith('OK')
    self_test = (ROOT / 'changes/TEST-ASSET-LAYOUT-001/evidence/self-test-final.txt').read_text()
    checks['self-test'] = 'FINAL_STATUS=PASS' in self_test and 'FAIL' not in '\n'.join(line for line in self_test.splitlines() if line.startswith('T'))
    for change in ('TEST-ASSET-LAYOUT-001', 'TEST-ASSET-AUTOMATION-001'):
        index = json.loads((ROOT / 'changes' / change / 'assets.json').read_text())
        checks[change + ':run-present'] = bool(index['runs'])
        for record in index['runs']:
            try:
                result = validate_run(ROOT, change, record['runId'])
                checks[change + ':' + record['runId']] = result['valid'] and result['files'] == 39
            except (OSError, ValueError): checks[change + ':' + record['runId']] = False
    receipt = json.loads((HERE / 'support-finalization.json').read_text())
    checks['support-finalized'] = not receipt['pending'] and all(item['absentAfter'] for item in receipt['deleted'])
    checks['reviews'] = all((HERE / f'review-{axis}-v3.md').is_file() for axis in ('standards', 'spec'))
    result = {'passed': all(checks.values()), 'checks': checks,
              'scope': 'LAYOUT + AUTOMATION verify-change slice1 only; no parent automation/product acceptance or live evidence'}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__': sys.exit(main())
