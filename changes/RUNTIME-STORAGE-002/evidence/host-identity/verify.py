#!/usr/bin/env python3
"""Validate this Host identity slice's retained evidence, without rerunning product tests."""
import hashlib
import json
from pathlib import Path
import sys
REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import validate_run, atomic_json
checks = {}
entry = json.loads((HERE / 'entry.json').read_text())
candidate = json.loads((HERE / 'candidate.json').read_text())
for path, sha in entry['files'].items():
    checks['entry:' + path] = hashlib.sha256((HERE / 'entry' / path).read_bytes()).hexdigest() == sha
for path, sha in candidate['files'].items():
    checks['candidate:' + path] = hashlib.sha256((REPO / path).read_bytes()).hexdigest() == sha
for label in ['identity-red', 'identity-red-observed', 'identity-green']:
    run = json.loads((HERE / (label + '.json')).read_text())
    checks['assets:' + label] = validate_run(REPO, 'RUNTIME-STORAGE-002', run['runId'])['valid']
    checks['stable:' + label] = run['sourceUnchangedDuringRun'] and run['inputsUnchangedDuringRun']
    checks['counts:' + label] = ((run['exitCode'] != 0 and run['counts']['failed'] == '4') if label != 'identity-green'
                               else run['exitCode'] == 0 and run['counts']['passed'] == '12' and run['counts']['failed'] == '0')
    if label == 'identity-green':
        provenance = json.loads((Path(run['runRoot']) / 'results/provenance.json').read_text())
        actual = {item['path']: item['sha256'] for item in provenance['sourceFiles']}
        checks['compiled-source-binding'] = all(actual.get(path) == sha for path, sha in candidate['files'].items())
observed = json.loads((HERE / 'red-observations.json').read_text())
checks['original-memory-reopen-gap'] = len(observed) == 4 and all(item['outcome'] == 'Failed' and 'in-memory=' in item['observation'] and 'reopened=' in item['observation'] for item in observed)
packet = json.loads((HERE / 'diagnosis.packet.json').read_text())
import jsonschema
jsonschema.validate(packet, json.loads((REPO / 'schemas/diagnosis-packet.schema.json').read_text()))
checks['diagnosis-schema'] = True
checks['diagnosis-entry-binding'] = all(entry['files'].get(item['path']) == item['sha256'] for item in packet['subject']['inputs'])
for axis in ['spec', 'standards']:
    checks['review:' + axis] = bool((HERE / ('review-' + axis + '.md')).read_text().strip())
result = {'passed': all(checks.values()), 'parentAccepted': False, 'scope': 'Host TaskInstance/ProductSession acknowledgement and close only; no full-suite/OS/crash/RuntimeRunStore historical reconstruction claim',
          'checks': checks, 'candidateSha256': candidate['files'], 'diagnosisSha256': hashlib.sha256((HERE / 'diagnosis.packet.json').read_bytes()).hexdigest()}
atomic_json(HERE / 'verification.json', result)
print(json.dumps({'passed': result['passed'], 'checks': len(checks), 'failed': [k for k, v in checks.items() if not v]}, indent=2))
sys.exit(0 if result['passed'] else 1)
