#!/usr/bin/env python3
"""Recheck retained first-slice evidence; does not rerun product or modify sealed history."""
import hashlib
import json
import sys
from pathlib import Path
REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import validate_run, atomic_json
checks = {}
entry = json.loads((HERE / 'entry.json').read_text())
for path, sha in entry['files'].items():
    checks['entry:' + path] = hashlib.sha256((HERE / 'entry' / path).read_bytes()).hexdigest() == sha
candidate = json.loads((HERE / 'candidate.json').read_text())
for path, sha in candidate['files'].items():
    checks['candidate:' + path] = hashlib.sha256((REPO / path).read_bytes()).hexdigest() == sha
labels = ['control-versions-red', 'versions-build', 'versions-first-regression', 'versions-regression-v2',
          'versions-cost-control', 'versions-cost-plan-distinct', 'versions-cost-plan-versioned', 'versions-storage-final']
for label in labels:
    run = json.loads((HERE / (label + '.json')).read_text())
    checks['assets:' + label] = validate_run(REPO, 'RUNTIME-STORAGE-002', run['runId'])['valid']
    checks['owned-stable:' + label] = run['ownedSourcesUnchangedDuringRun'] and run['inputsUnchangedDuringRun']
    if label == 'control-versions-red':
        checks['red-budget'] = run['exitCode'] != 0 and run['counts']['failed'] == '1'
    elif label == 'versions-first-regression':
        checks['intermediate-failure-kept'] = run['exitCode'] != 0 and run['counts']['failed'] == '43'
        checks['parallel-drift-disclosed'] = set(run['changedSourcePathsDuringRun']) == {'tools/test_asset_archives.py', 'tools/test_asset_operations.py', 'tools/test_assets.py'}
    else:
        checks['success:' + label] = run['exitCode'] == 0
    if label in ('versions-regression-v2', 'versions-storage-final'):
        provenance = json.loads((Path(run['runRoot']) / 'results/provenance.json').read_text())
        actual = {item['path']: item['sha256'] for item in provenance['sourceFiles']}
        checks['candidate-bound:' + label] = all(actual.get(path) == sha for path, sha in candidate['files'].items())
    if label.startswith('versions-cost-'):
        cost = json.loads((Path(run['runRoot']) / 'raw/source/growth.json').read_text())
        checks['same-records:' + label] = cost['EquivalentLegacy']['IdenticalOriginalRowsAndRecordingIds'] and cost['EquivalentLegacy']['LegacyReaderRecordsMatch']
        checks['query-roundtrips:' + label] = all(cost['VersionQueries'][family]['RecordsMatch'] for family in ['Control', 'Plan'])
        checks['original-readonly:' + label] = all(cost['SourceSha256Preserved'].values()) and not cost['FullReadOnlyDomainSourceChecks']['ContinuationSupported']
        if label.endswith('control'):
            checks['control-net-storage-budget'] = cost['EquivalentLegacy']['Control']['CurrentSourceHeadAndBaselinesBytes'] < 9 * 1024 * 1024
        if label.endswith('plan-distinct'):
            c = cost['EquivalentLegacy']['Plan']; checks['distinct-plan-storage-reduced'] = c['CurrentSourceHeadAndBaselinesBytes'] < c['LegacySourceAndHeadBytes']
compat = json.loads((HERE / 'resource-v4-compat.json').read_text())
checks['sealed-resource-compat'] = compat['passed'] and compat['counts']['bidirectionalMatch'] and compat['sealedSourceUnchanged']
checks['compat-assets'] = validate_run(REPO, 'RUNTIME-STORAGE-002', compat['runId'])['valid']
checks['compat-temporary-build-removed'] = compat['temporaryBuildRemoved']
for axis in ['spec', 'standards']:
    report = (HERE / ('review-' + axis + '.md')).read_text()
    checks['review:' + axis] = bool(report.strip())
result = {'passed': all(checks.values()), 'parentAccepted': False, 'scope': 'Control/Plan core version slice only; shared source candidate, no live/device/model/full-suite claim',
          'checks': checks, 'candidateSha256': candidate['files'], 'runs': labels + [compat['runId']]}
atomic_json(HERE / 'verification.json', result)
print(json.dumps({'passed': result['passed'], 'checks': len(checks), 'failed': [k for k, v in checks.items() if not v]}, indent=2))
sys.exit(0 if result['passed'] else 1)
