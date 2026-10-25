#!/usr/bin/env python3
"""Validate retained Host projection evidence; never rerun product tests."""
import hashlib
import json
from pathlib import Path
import sys

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import atomic_json, validate_run


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


candidate = json.loads((HERE / 'candidate-v2.json').read_text())
baseline = json.loads((HERE / 'host-baseline-red.json').read_text())
final = json.loads((HERE / 'host-pure-final.json').read_text())
checks = {}

for path, expected in candidate['files'].items():
    checks['candidate-sha:' + path] = sha(REPO / path) == expected

checks['baseline-asset-valid'] = validate_run(REPO, 'RUNTIME-STORAGE-002', baseline['runId'])['valid']
checks['baseline-red-5-of-5'] = (baseline['exitCode'] != 0 and baseline['counts']['total'] == '5'
                                and baseline['counts']['executed'] == '5'
                                and baseline['counts']['failed'] == '5')
checks['baseline-source-unchanged'] = (baseline['sourceUnchangedDuringRun']
                                       and baseline['inputsUnchangedDuringRun']
                                       and baseline['ownedSourcesUnchangedDuringRun'])

checks['final-asset-valid'] = validate_run(REPO, 'RUNTIME-STORAGE-002', final['runId'])['valid']
checks['final-32-of-32'] = (final['exitCode'] == 0 and final['counts']['total'] == '32'
                           and final['counts']['executed'] == '32'
                           and final['counts']['passed'] == '32'
                           and final['counts']['failed'] == '0')
checks['final-source-files-unchanged'] = final['sourceFilesUnchanged'] is True
checks['final-consumers-completed'] = final['consumersCompleted'] is True

for axis in ('spec', 'standards'):
    report = (HERE / f'review-{axis}-delta.md').read_text()
    checks[f'review-{axis}-no-findings'] = '0 项' in report

# This is an explicitly retained integration-build blocker, not Host behavior evidence.
review_final = json.loads((HERE / 'host-review-final.json').read_text())
checks['shared-host-review-final-blocked-by-coverage-wip'] = (
    review_final['exitCode'] != 0 and review_final['consumersCompleted'] is False
    and review_final['scope'] == 'shared working sources; selected checks only')

result = {
    'passed': all(checks.values()),
    'parentAccepted': False,
    'scope': 'Host projection evidence only; joint build remains a separate acceptance gate',
    'checks': checks,
    'checkCount': len(checks),
    'candidateSha256': candidate['files'],
    'retainedBlocker': 'shared host-review-final compile blocked by Coverage WIP; not Host behavior evidence',
}
atomic_json(HERE / 'verification.json', result)
print(json.dumps({'passed': result['passed'], 'checks': result['checkCount'],
                  'failed': [k for k, v in checks.items() if not v]}, indent=2))
sys.exit(0 if result['passed'] else 1)
