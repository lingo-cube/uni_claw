#!/usr/bin/env python3
"""Repeat recovery on the SAME disposable fixture, preserving TRX freshness."""
import json
import os
from pathlib import Path
import runpy
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets
import test_asset_archives as archives

CHANGE = 'TEST-ASSET-AUTOMATION-001'


def main():
    if (HERE / 'mtime-restore-proof.json').exists():
        proof = json.loads((HERE / 'mtime-restore-proof.json').read_text())
        assert assets.validate_run(ROOT, CHANGE, proof['probeRunId'])['valid']
        print(json.dumps({'reusedProof': True, 'probeRunId': proof['probeRunId']})); return
    first = json.loads((HERE / 'real-restore-proof.json').read_text())
    index = assets.read_index(ROOT, CHANGE)
    probe = next(r for r in index['runs'] if r['runId'] == first['probeRunId'])
    source = next(r for r in index['runs'] if r['runId'] == first['sourceRunId'])
    root = assets.locate_run(ROOT, CHANGE, probe['runId'])
    native = next(f['path'] for f in source['files'] if f['path'].endswith('/case.trx'))
    source_trx = assets.asset_path(ROOT, CHANGE, source['runId'], native)
    relative_trx = next(f['path'] for f in probe['files'] if f['path'].endswith('/consumer-input.trx'))
    copy = assets.checked(root, relative_trx)
    old_copy_time = copy.stat().st_mtime_ns
    # This is the expressly disposable input copy, never the historical source.
    # It should carry the input's age instead of the time it was copied.
    os.utime(copy, ns=(copy.stat().st_atime_ns, source_trx.stat().st_mtime_ns))
    before = {f['path']: {**f, 'mtimeNs': assets.checked(root, f['path']).stat().st_mtime_ns} for f in probe['files']}
    freshness = runpy.run_path(str(ROOT / 'tools/scenario-coverage.py'))['freshness_violation']
    stale_before = freshness(copy) is not None
    entry = archives.archive_run(ROOT, CHANGE, probe['runId'])
    assert entry['formatVersion'] == 2 and entry['verified']
    assert archives.verify_archive(ROOT, CHANGE, entry['archiveId'])['valid']
    removal = {'runId': probe['runId'], 'scope': 'same disposable fixture as first proof; no historical source removal',
        'archiveId': entry['archiveId'], 'archiveSha256': entry['sha256'], 'files': list(before.values())}
    assets.atomic_json(HERE / 'mtime-removal-plan.json', removal)
    assert {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()} == set(before)
    for name, item in before.items():
        path = assets.checked(root, name)
        assert path.stat().st_nlink == 1 and assets.file_digest(path) == item['sha256']
        path.unlink(); assert not path.exists()
    for path in sorted((p for p in root.rglob('*') if p.is_dir()), key=lambda p: len(p.parts), reverse=True): path.rmdir()
    root.rmdir()
    result = archives.restore_run(ROOT, CHANGE, probe['runId'], entry['archiveId'])
    assert result['consumerStatus'] == 'verified' and result['mtimeRestoration'] == 'preserved'
    for name, item in before.items():
        path = assets.checked(root, name)
        assert assets.file_digest(path) == item['sha256'] and path.stat().st_mtime_ns == item['mtimeNs']
    stale_after = freshness(copy) is not None
    assert stale_before == stale_after
    command = [sys.executable, str(ROOT / 'tools/test-assets'), 'verify-archive', CHANGE, first['probeArchiveId']]
    legacy = subprocess.run(command, capture_output=True, text=True)
    assert legacy.returncode == 0, legacy.stderr + legacy.stdout
    historical = 0
    for owner in ('TEST-ASSET-LAYOUT-001', CHANGE):
        for record in assets.read_index(ROOT, owner)['runs']:
            if record['runId'] != probe['runId']:
                assert assets.validate_run(ROOT, owner, record['runId'])['valid']
                historical += len(record['files'])
    assets.atomic_json(HERE / 'mtime-restore-proof.json', {'passed': True, 'probeRunId': probe['runId'],
        'archiveId': entry['archiveId'], 'archiveFormatVersion': 2, 'restoredFiles': len(before),
        'allDigestsAndMtimesPreserved': True, 'sourceFreshnessDecisionUnchanged': True,
        'staleBefore': stale_before, 'staleAfter': stale_after,
        'fixtureCopyMtimeBeforeCorrectionNs': old_copy_time, 'copiedInputMtimeNs': source_trx.stat().st_mtime_ns,
        'legacyArchiveStillReadable': True, 'historicalFilesKept': historical, 'historicalSourcesDeleted': False,
        'netSourceBytesRemoved': 0, 'newProductExecution': False})
    print((HERE / 'mtime-restore-proof.json').read_text())


if __name__ == '__main__': main()
