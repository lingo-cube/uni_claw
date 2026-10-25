#!/usr/bin/env python3
"""Read-only final checks; no restore, release, build or product execution."""
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets
import test_asset_archives as archives
import test_asset_cleanup as cleanup


def main():
    checks = {}
    for item in json.loads((HERE / 'candidate.json').read_text())['files']:
        checks['candidate:' + item['path']] = assets.file_digest(ROOT / item['path']) == item['sha256']
    for item in json.loads((HERE.parent / 'slice3/candidate.json').read_text())['files']:
        path = HERE / 'before' / item['path']
        if not path.exists(): path = ROOT / item['path']
        checks['slice3-preserved:' + item['path']] = assets.file_digest(path) == item['sha256']
    proof = json.loads((HERE / 'real-release-proof.json').read_text())
    change, run_id, relative = proof['assetRef'].removeprefix('test-assets:').split('/', 2)
    index = assets.read_index(ROOT, change)
    record = next(r for r in index['runs'] if r['runId'] == run_id)
    root = assets.locate_run(ROOT, change, run_id)
    available = assets.validate_run(ROOT, change, run_id)
    checks['released-work-absent-and-readonly-archive-valid'] = not root.exists() and available['valid'] and available['storage'] == 'archive'
    checks['package-still-valid'] = archives.verify_archive(ROOT, change, proof['archiveId'])['valid']
    checks['final-reference-guards-pass-readonly'] = not cleanup.reference_blockers(ROOT, change, record, root)
    attempts = [r for r in index['workReleases'] if r['releaseId'] in proof['releaseAttempts']]
    checks['actual-receipts'] = len(attempts) == 2 and all(r['completed'] and not r['pending'] and
        len(r['deleted']) == proof['probeFiles'] and r['deletedBytes'] == proof['netWorkLogicalBytesRemoved'] for r in attempts)
    historical = 0
    for owner in ('TEST-ASSET-LAYOUT-001', change):
        for original in assets.read_index(ROOT, owner)['runs']:
            if owner == change and original['runId'] == run_id: continue
            original_root = assets.locate_run(ROOT, owner, original['runId'])
            for item in original['files']:
                path = assets.checked(original_root, item['path'])
                checks['original:' + owner + '/' + original['runId'] + '/' + item['path']] = (
                    path.is_file() and path.stat().st_size == item['bytes'] and assets.file_digest(path) == item['sha256'])
                historical += 1
    checks['original100-physical-files-kept'] = historical == proof['historicalFilesKept'] == 100
    checks['native-recovery-proof'] = all(proof[name] for name in ('passed', 'validateDidNotRestore', 'sameBoundRoot',
        'sameFailureKey', 'sameSourceSha256', 'allMtimesPreserved', 'originalRunRecordUnchanged')) and not proof['newProductExecution']
    old = json.loads((ROOT / 'changes/ORPHAN-CLEANUP-001/cleanup.json').read_text())
    checks['restored4-sources-protected'] = all(assets.file_digest(ROOT / item['path']) == item['sha256'] for item in old['restoredAfterReview'])
    for log, count in [('contracts-final-v5.txt', 50), ('verify-entry-v1.txt', 15)]:
        content = (HERE / log).read_text()
        checks[log] = f'Ran {count} tests' in content and content.rstrip().endswith('OK')
    checks['reviews'] = all((HERE / f'review-{axis}-v3.md').exists() for axis in ('standards', 'spec'))
    result = {'passed': all(checks.values()), 'checks': checks, 'historicalFilesPhysicallyKept': historical,
        'newCopyFilesAvailableOnlyInArchive': len(record['files']), 'netWorkLogicalBytesRemoved': proof['netWorkLogicalBytesRemoved'],
        'scope': 'opt-in verification work copies only; not task deletion, product storage, historical migration or physical-space measurement'}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__': sys.exit(main())
