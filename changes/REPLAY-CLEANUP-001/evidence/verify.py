#!/usr/bin/env python3
"""Check bounded deletion and retained baselines; never changes old pins."""
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}


def load(p):
    return json.loads(p.read_text())


protected = load(HERE / 'protected-sha256.json')
manifest = load(HERE / 'deletion-manifest.json')
concurrent = {r['path']: r for r in load(HERE / 'concurrent-drift.json')['files']}
for name, expected in protected.items():
    actual = hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
    if name in concurrent:
        assert name.startswith('src/'), ('baseline-drift-not-allowed', name)
        assert expected == concurrent[name]['entrySha256'], name
        assert actual == concurrent[name]['observedSha256'], ('new-concurrent-drift', name)
    else:
        assert actual == expected, name
for item in manifest['files']:
    assert not (ROOT / item['path']).exists(), item['path']
assert not set(protected).intersection(x['path'] for x in manifest['files'])


def read_trx(name):
    tree = ET.parse(HERE / name)
    rows = tree.findall('.//t:UnitTestResult', NS)
    return tree.find('.//t:Counters', NS).attrib, rows


after_counts, after = read_trx('targeted/targeted.trx')
before_counts, control = read_trx('control-restored/control-restored.trx')


def failures(rows):
    return {r.attrib['testName']: r.find('.//t:Message', NS).text
            for r in rows if r.attrib['outcome'] == 'Failed'}


def normalize(message):
    # Only the per-run temporary directory GUID differs. Keep the raw paths in
    # TRX and preserve every other character of the exception message.
    return re.sub(r'local-act-overlay-[0-9a-f]{32}', 'local-act-overlay-<run-guid>', message)


after_failures = failures(after)
control_failures = failures(control)
retired = [n for n in control_failures if 'CurrentCandidate' in n]
assert len(retired) == 6
assert {n: normalize(m) for n, m in after_failures.items()} == {
    n: normalize(m) for n, m in control_failures.items() if n not in retired}
assert len(after_failures) == 8
assert after_counts['passed'] == before_counts['passed'] == '16'
assert after_counts['total'] == '24' and before_counts['total'] == '30'
assert not any('CurrentCandidate' in r.attrib['testName'] for r in after)
formal = [r for r in after if '.RecordedPopupReplayTests.' in r.attrib['testName']
          or '.RecordedPopupRuntimeReplayTests.' in r.attrib['testName']]
assert len(formal) == 6
assert all('hash-mismatch:src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs'
           in after_failures[r.attrib['testName']] for r in formal)
directory_errors = [n for n, m in after_failures.items()
                    if 'System.IO.DirectoryNotFoundException' in m]
assert len(directory_errors) == 2

result = {
    'cleanupScopeVerified': True,
    'deletedRepositoryFiles': manifest['fileCount'],
    'deletedRepositoryBytes': manifest['bytes'],
    'protectedFilesUnchanged': len(protected) - len(concurrent),
    'protectedBaselineAndCarrierFilesUnchanged': sum(not p.startswith('src/') for p in protected),
    'concurrentProductSourceChangesPreserved': list(concurrent.values()),
    'formalPopupReplayTestsRetained': len(formal),
    'duplicateCandidateTestsRetired': len(retired),
    'afterCleanup': after_counts,
    'candidateControl': before_counts,
    'unchangedFailures': [{'name': n, 'message': m} for n, m in after_failures.items()],
    'comparison': 'same remaining failure names/messages; normalize only test-generated temporary directory GUID',
    'controlQualification': 'restore exact 3 candidate source files and composition in private clone only; deleted process exports have no runtime references; unused provenance was not restored',
    'allSuitesGreen': False,
    'productFixApplied': False,
}
(HERE / 'verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({k: v for k, v in result.items() if k != 'unchangedFailures'}, ensure_ascii=False))
