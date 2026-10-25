#!/usr/bin/env python3
"""A disposable archival input, never removal of the historical source Run."""
import hashlib
import json
import os
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets
import test_asset_operations as operations
import test_asset_archives as archives

CHANGE = 'TEST-ASSET-AUTOMATION-001'


def write(name, value):
    assets.atomic_json(HERE / name, value)


def cli(*arguments):
    result = subprocess.run([sys.executable, str(ROOT / 'tools/test-assets'), *arguments], text=True, capture_output=True)
    if result.returncode: raise RuntimeError(result.stderr or result.stdout)
    return result.stdout.strip()


def main():
    if (HERE / 'real-restore-proof.json').exists():
        proof = json.loads((HERE / 'real-restore-proof.json').read_text())
        assert assets.validate_run(ROOT, CHANGE, proof['probeRunId'])['valid']
        print(json.dumps({'reusedProof': True, 'probeRunId': proof['probeRunId']}))
        return
    baseline = {}
    for owner in ('TEST-ASSET-LAYOUT-001', CHANGE):
        for record in assets.read_index(ROOT, owner)['runs']:
            assert assets.validate_run(ROOT, owner, record['runId'])['valid']
            baseline[(owner, record['runId'])] = record['files']
    source_run = next(r for r in reversed(assets.read_index(ROOT, CHANGE)['runs']) if r['purpose'] == 'replay')
    source_trx = next(f['path'] for f in source_run['files'] if f['path'].endswith('/case.trx'))
    source_path = assets.asset_path(ROOT, CHANGE, source_run['runId'], source_trx)
    existing_archives = [a for a in assets.read_index(ROOT, CHANGE)['archives'] if a['runId'] == source_run['runId']]
    existing = existing_archives[-1] if existing_archives else json.loads(cli('archive', CHANGE, source_run['runId']))
    assert archives.verify_archive(ROOT, CHANGE, existing['archiveId'])['valid']
    write('real-existing-run-archive.json', existing)
    started = operations.begin(CHANGE, 'verification', None, None,
        [str(Path(__file__)), str(ROOT / 'tools/test_asset_archives.py')], [str(source_path)],
        {'purpose': 'disposable archive recovery input', 'sourceRunId': source_run['runId'], 'newProductExecution': False}, [])
    run_id = started['runId']; root = Path(started['root'])
    copied = Path(started['raw']) / 'consumer-input.trx'; shutil.copyfile(source_path, copied)
    adapter = runpy.run_path(str(ROOT / 'tools/evolution-signal.py'))
    signals = adapter['scan_dev']([copied], ROOT, intent='expected-red', context={'change': CHANGE, 'scope': 'archive-readback-proof'})
    expected_signal = signals[0]
    signal_path = Path(adapter['persist'](signals, Path(started['results']) / 'evolution', ROOT)[0])
    signal_relative = signal_path.relative_to(root).as_posix()
    result, code = operations.finish(CHANGE, run_id, {'status': 'PREPARED', 'operation': 'archive-readback-fixture',
        'consumersCompleted': True, 'testExecution': 'none; archival input reuses an existing TRX', 'newProductExecution': False})
    assert code == 0
    archived = json.loads(cli('archive', CHANGE, run_id))
    assert json.loads(cli('verify-archive', CHANGE, archived['archiveId']))['valid']
    record = next(r for r in assets.read_index(ROOT, CHANGE)['runs'] if r['runId'] == run_id)
    assert record['summary']['sourceStable']
    expected = {f['path']: f for f in record['files']}
    actual = {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    assert actual == set(expected)
    assert all(not p.is_symlink() for p in root.rglob('*'))
    bound = assets.bound_layout(ROOT, record)
    assert root == bound.run_root(CHANGE, run_id)
    removal = {'changeId': CHANGE, 'runId': run_id, 'operation': 'controlled recovery test only',
        'archiveId': archived['archiveId'], 'archiveSha256': archived['sha256'],
        'files': list(expected.values()), 'originalHistoricalRunUntouched': source_run['runId'],
        'reason': 'fresh disposable copy created exclusively by this proof; verified package and native readers completed first'}
    write('controlled-source-removal-plan.json', removal)
    removed = []
    for name, item in expected.items():
        path = assets.checked(root, name)
        assert path.stat().st_nlink == 1 and path.stat().st_size == item['bytes'] and assets.file_digest(path) == item['sha256']
        path.unlink(); assert not path.exists(); removed.append(item)
    for directory in sorted((p for p in root.rglob('*') if p.is_dir()), key=lambda p: len(p.parts), reverse=True):
        assets.checked(root, directory.relative_to(root)).rmdir()
    root.rmdir(); assert not root.exists()
    availability = json.loads(cli('validate', CHANGE, run_id))
    assert availability['valid'] and availability['bytesValid'] and not availability['workTreeComplete']
    assert not root.exists()  # validate is read-only.
    reference = f'test-assets:{CHANGE}/{run_id}/{signal_relative}'
    restored_signal = Path(cli('path-ref', reference))
    restored = json.loads(restored_signal.read_text())
    assert restored_signal == signal_path and Path(restored['source']['path']) == copied
    assert assets.file_digest(copied) == restored['source']['sha256']
    assert all(assets.file_digest(assets.checked(root, name)) == item['sha256'] for name, item in expected.items())
    with tempfile.TemporaryDirectory(prefix='uniclaw-native-consumer-') as output:
        environment = dict(os.environ, GIT_OPTIONAL_LOCKS='0')
        checked = subprocess.run([sys.executable, str(ROOT / 'tools/evolution-signal.py'), 'dev', '--trx', str(copied),
            '--intent', 'expected-red', '--repo-root', str(ROOT), '--out-dir', output], env=environment, text=True, capture_output=True)
        assert checked.returncode == 0, checked.stderr
        emitted_path = json.loads(checked.stdout)['signals'][0]
        emitted = json.loads(Path(emitted_path).read_text())
        assert emitted['failureKey'] == expected_signal['failureKey']
        assert emitted['source']['sha256'] == expected_signal['source']['sha256']
    for (owner, identity), files in baseline.items():
        assert next(r for r in assets.read_index(ROOT, owner)['runs'] if r['runId'] == identity)['files'] == files
        assert assets.validate_run(ROOT, owner, identity)['valid']
    write('controlled-source-removal-receipt.json', {**removal, 'deletedDuringProof': removed,
        'deletedBytes': sum(f['bytes'] for f in removed), 'restoredAll': True, 'netSourceBytesRemoved': 0})
    write('real-restore-proof.json', {'passed': True, 'probeRunId': run_id, 'sourceRunId': source_run['runId'],
        'existingArchiveId': existing['archiveId'], 'probeArchiveId': archived['archiveId'],
        'historicalFilesKept': sum(len(v) for v in baseline.values()), 'probeFiles': len(expected),
        'consumer': 'original evolution-signal.py dev CLI', 'sameFailureKey': True, 'sameSourceSha256': True,
        'restoredSameBoundRoot': True, 'validateDidNotRestore': True, 'historicalSourcesDeleted': False,
        'netSourceBytesRemoved': 0, 'newProductExecution': False})
    print((HERE / 'real-restore-proof.json').read_text())


if __name__ == '__main__': main()
