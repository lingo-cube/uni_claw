#!/usr/bin/env python3
"""Read-only validation of this slice and preserved prior evidence. No build/GC."""
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
from test_assets import validate_run


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    checks = {}
    for item in json.loads((HERE / 'candidate-v2.json').read_text())['files']:
        checks['candidate:' + item['path']] = sha(ROOT / item['path']) == item['sha256']
    previous = HERE.parent / 'slice1'
    for item in json.loads((previous / 'candidate.json').read_text())['files']:
        source = HERE / 'before' / item['path']
        if not source.exists(): source = ROOT / item['path']
        checks['prior:' + item['path']] = sha(source) == item['sha256']
    for log, count in [('python-final-v3.txt', 22), ('verify-entry-final.txt', 15)]:
        text = (HERE / log).read_text()
        checks[log] = f'Ran {count} tests' in text and text.rstrip().endswith('OK')
    for name, expected in [('diagnosis-regression-v3.trx', 39), ('diagnosis-managed-final.trx', 8)]:
        results = [e for e in ET.parse(HERE / name).getroot().iter() if e.tag.split('}')[-1] == 'UnitTestResult']
        checks[name] = len(results) == expected and all(e.get('outcome') == 'Passed' for e in results)
    total = 0
    replay_runs = []
    for change in ('TEST-ASSET-LAYOUT-001', 'TEST-ASSET-AUTOMATION-001'):
        index = json.loads((ROOT / 'changes' / change / 'assets.json').read_text())
        for run in index['runs']:
            result = validate_run(ROOT, change, run['runId'])
            checks[run['runId']] = result['valid']
            total += result['files']
            if run['purpose'] == 'replay': replay_runs.append(run)
    checks['retained-files94'] = total == 94
    checks['real-replay-failures-retained'] = len(replay_runs) == 2 and all(r['summary']['status'] == 'mismatch' and r['summary']['cliExitCode'] == 1 for r in replay_runs)
    entry = json.loads((HERE / 'entry.json').read_text())['sealedReplayScript']
    checks['sealed-script-unchanged'] = sha(ROOT / entry['path']) == entry['sha256']
    for name in ('default-apps', 'sim-dialog'):
        case = ROOT / 'changes/EVO-003/evidence/cases' / name / 'case.json'
        checks['original-case:' + name] = sha(case) == case.with_name('case.sha256').read_text().strip()
    case = HERE / 'case-input/case.json'
    document = json.loads(case.read_text())
    checks['current-case-inputs'] = sha(case) == case.with_name('case.sha256').read_text().strip() and all(sha(case.parent / item['path']) == item['sha256'] for item in document['inputs'])
    old = json.loads((ROOT / 'changes/ORPHAN-CLEANUP-001/cleanup.json').read_text())
    checks['old40-not-repeated'] = all(not (ROOT / item['path']).exists() for item in old['deleted'])
    checks['restored4-protected'] = all(sha(ROOT / item['path']) == item['sha256'] for item in old['restoredAfterReview'])
    old_receipt = json.loads((previous / 'support-finalization.json').read_text())
    checks['old3-absent'] = all(not (Path('/tmp') / item['path']).exists() for item in old_receipt['deleted'])
    checks['reviews'] = all((HERE / name).is_file() for name in ('review-standards-v3.md', 'review-spec-v2.md'))
    result = {'passed': all(checks.values()), 'checks': checks, 'retainedRunFiles': total,
              'scope': 'replay and explicit development diagnosis only; no full product/model/device/archive/GC claim'}
    print(json.dumps(result, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__': sys.exit(main())
