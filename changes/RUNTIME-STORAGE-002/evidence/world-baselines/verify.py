#!/usr/bin/env python3
"""Read-only evidence validation. No builds, replay, restore or deletion."""
import hashlib
import json
from pathlib import Path
import sys
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets


def read(name): return json.loads((HERE / name).read_text())


def main():
    checks = {}
    for item in read('candidate.json')['files']:
        checks['candidate:' + item['path']] = assets.file_digest(ROOT / item['path']) == item['sha256']
    for item in read('entry.json')['files']:
        checks['before:' + item['path']] = assets.file_digest(HERE / 'before' / item['path']) == item['sha256']
    index = assets.read_index(ROOT, 'RUNTIME-STORAGE-002')
    phase_files = sorted(HERE.glob('*-result.json'))
    for result_file in phase_files:
        result = json.loads(result_file.read_text())
        if 'runId' not in result: continue
        record = next(r for r in index['runs'] if r['runId'] == result['runId'])
        checks['run:' + result['runId']] = assets.validate_run(ROOT, 'RUNTIME-STORAGE-002', result['runId'])['valid']
        if result_file.name == 'world-final-v2-result.json':
            checks['final154'] = result['counts'] == {'Passed': 154} and result['exitCode'] == 0
            checks['final-consumers-and-source-stable'] = record['summary']['consumersCompleted'] and record['summary']['sourceStable']
    official_red = read('red-result.json')
    checks['behavior-red'] = official_red['exitCode'] != 0 and official_red['counts'] == {'Failed': 1}
    cost = read('cost-result.json')
    checks['cost-and-tradeoff'] = (cost['sameRawJsonAndQueryHashesVerified'] and cost['logical']['revisions'] == 128 and
        cost['v3Bytes'] == 1236533 and cost['v2Bytes'] == 1059133 and cost['v1Bytes'] == 8184254 and
        cost['decodeCounts'] == {'v3Latest': 32, 'v2Latest': 128})
    old_code = read('cost-probe-source.json')
    checks['exact-measurement-source'] = assets.file_digest(HERE / old_code['path']) == old_code['sha256']
    legacy_run = read('legacy-run.json')
    for version in ('v1', 'v2'):
        report = json.loads((Path(legacy_run['raw']) / f'existing-{version}.json').read_text())
        checks['legacy:' + version] = report['source']['sourceHashesUnchanged'] and report['latest']['matchesOriginalFullReader']
        for value in report['source']['hashesAfter']:
            filename, checksum = value.rsplit(':', 1)
            checks['legacy-preserved:' + filename] = assets.file_digest(Path(filename)).upper() == checksum
    async_path = 'tests/UniClaw.Host.Tests/WorldAsyncStorageTests.cs'
    before = (HERE / 'before' / async_path).read_text()
    checks['only-authorized-async-line'] = (ROOT / async_path).read_text() == before.replace(
        'Assert.Contains("uniclaw.world.history.v2", File.ReadAllText(wp));', 'Assert.Contains("uniclaw.world.history.v3", File.ReadAllText(wp));')
    checks['reviews'] = all((HERE / f'review-{axis}-v1.md').is_file() for axis in ('standards', 'spec'))
    result = {'passed': all(checks.values()), 'checks': checks,
        'scope': 'World v3 baseline/data reader, not parent acceptance, live execution, history pruning or Coverage delivery'}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__': sys.exit(main())
