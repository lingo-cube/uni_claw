#!/usr/bin/env python3
"""Read-only proof of the fixed candidate and retained real asset records."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets


def main():
    candidate = json.loads((HERE / 'candidate-v3.json').read_text())
    checks = {'source:' + item['path']: assets.file_digest(ROOT / item['path']) == item['sha256']
              for item in candidate['files']}
    regression = json.loads((HERE / 'regression-v4-result.json').read_text())
    proof = json.loads((HERE / 'actual-cli-v4-result.json').read_text())
    checks['95-contracts'] = regression['exitCode'] == 0 and regression['counts'] == {'total': 95, 'passed': True}
    checks['verification-sources-stable'] = regression['sourceStable'] and proof['sourceStable']
    checks['consumers-completed'] = regression['consumersCompleted'] and proof['consumersCompleted']
    checks['actual-cli-retirement'] = proof['status'] == 'passed' and proof['deletedFiles'] == 5 and proof['deletedBytes'] > 0
    checks['protected-seven'] = proof['protectedFilesVerified'] == 7 and proof['originalRunFactsUnchanged']
    checks['historic-binding-and-read-guards'] = all(proof[key] for key in ('oldBindingsAndNestedPathsUsed', 'retiredReadRejected', 'protectedReadSucceeded'))
    checks['no-real-history-selected'] = not proof['realHistoricalTasksDeleted']
    totals = {'runs': 0, 'registeredFiles': 0, 'archives': 0}
    for change in ('TEST-ASSET-LAYOUT-001', 'TEST-ASSET-AUTOMATION-001'):
        name = 'changes/' + change + '/assets.json'
        original = json.loads(subprocess.check_output(['git', 'show', candidate['base'] + ':' + name], cwd=ROOT))
        current = assets.read_index(ROOT, change)
        for key in ('runs', 'archives', 'baselineRefs'):
            checks[change + ':original-' + key] = current.get(key, [])[:len(original.get(key, []))] == original.get(key, [])
        for record in current['runs']:
            checks['available:' + record['runId']] = assets.validate_run(ROOT, change, record['runId'])['valid']
            totals['registeredFiles'] += len(record['files'])
        totals['runs'] += len(current['runs'])
        totals['archives'] += len(current.get('archives', []))
    for axis in ('spec', 'standards'):
        checks['review:' + axis] = (HERE / ('review-' + axis + '-v3.md')).is_file()
    result = {'passed': all(checks.values()), 'checks': checks, 'retainedRealAssets': totals,
              'controlledDeletion': {key: proof[key] for key in ('deletedFiles', 'deletedBytes', 'protectedFilesVerified')},
              'qualification': 'Local deterministic contracts and real subprocess CLI on isolated owned fixtures; no product/device/model execution or historical deletion.'}
    print(json.dumps(result, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
