#!/usr/bin/env python3
"""One fresh disposable copy, real CLIs, no product test execution or old deletion."""
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
CHANGE = 'TEST-ASSET-AUTOMATION-001'
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets


def cli(*args):
    result = subprocess.run([sys.executable, str(ROOT / 'tools/test-assets'), *args], text=True, capture_output=True)
    assert result.returncode == 0, result.stderr + result.stdout
    return result.stdout.strip()


def main():
    proof_file = HERE / 'real-release-proof.json'
    if proof_file.exists():
        proof = json.loads(proof_file.read_text())
        assert proof['passed']
        print('Existing proof retained; no repeated deletion.')
        return
    originals = {}
    for owner in ('TEST-ASSET-LAYOUT-001', CHANGE):
        for record in assets.read_index(ROOT, owner)['runs']:
            root = assets.locate_run(ROOT, owner, record['runId'])
            for item in record['files']:
                path = assets.checked(root, item['path'])
                assert path.is_file() and assets.file_digest(path) == item['sha256']
                originals[str(path)] = item['sha256']
    assert len(originals) == 100
    source_run = next(r for r in reversed(assets.read_index(ROOT, CHANGE)['runs']) if r['purpose'] == 'replay')
    source_item = next(f for f in source_run['files'] if f['path'].endswith('/case.trx'))
    source = assets.asset_path(ROOT, CHANGE, source_run['runId'], source_item['path'])
    started = json.loads(cli('begin', CHANGE, '--operation', 'verification', '--source', str(Path(__file__)),
        '--source', str(ROOT / 'tools/test_asset_cleanup.py'), '--input', str(source),
        '--context', '{"purpose":"disposable work release copy","newProductExecution":false}',
        '--work-release-reason', '本证明独占副本，所有读取已结束；后续仅通过path-ref读取，无仓库外或构建目录物理消费者'))
    run_id = started['runId']; root = Path(started['root'])
    copied = Path(started['raw']) / 'consumer-input.trx'; shutil.copy2(source, copied)
    adapter = runpy.run_path(str(ROOT / 'tools/evolution-signal.py'))
    signals = adapter['scan_dev']([copied], ROOT, intent='expected-red', context={'change': CHANGE, 'scope': 'release-copy'})
    expected_signal = signals[0]
    signal_path = Path(adapter['persist'](signals, Path(started['results']) / 'evolution', ROOT)[0])
    signal_relative = signal_path.relative_to(root).as_posix()
    cli('finish', CHANGE, run_id, '--summary', json.dumps({'status': 'COPY_ONLY', 'consumersCompleted': True,
        'newProductExecution': False, 'testExecution': 'none; existing TRX copied for archive/release proof'}))
    record = next(r for r in assets.read_index(ROOT, CHANGE)['runs'] if r['runId'] == run_id)
    timestamps = {f['path']: (root / f['path']).stat().st_mtime_ns for f in record['files']}
    package = json.loads(cli('archive', CHANGE, run_id))
    first = json.loads(cli('release-work', CHANGE, run_id))
    assert first['completed'] and len(first['deleted']) == len(record['files']) and not root.exists()
    available = json.loads(cli('validate', CHANGE, run_id))
    assert available['valid'] and available['storage'] == 'archive' and not root.exists()
    reference = f'test-assets:{CHANGE}/{run_id}/{signal_relative}'
    restored_signal = Path(cli('path-ref', reference))
    restored = json.loads(restored_signal.read_text())
    assert restored_signal == signal_path and Path(restored['source']['path']) == copied
    assert assets.file_digest(copied) == expected_signal['source']['sha256']
    assert all(assets.file_digest(root / f['path']) == f['sha256'] for f in record['files'])
    assert all((root / name).stat().st_mtime_ns == timestamp for name, timestamp in timestamps.items())
    with tempfile.TemporaryDirectory(prefix='uniclaw-release-native-') as output:
        consumer = subprocess.run([sys.executable, str(ROOT / 'tools/evolution-signal.py'), 'dev', '--trx', str(copied),
            '--intent', 'expected-red', '--repo-root', str(ROOT), '--out-dir', output],
            env=dict(os.environ, GIT_OPTIONAL_LOCKS='0'), text=True, capture_output=True)
        assert consumer.returncode == 0, consumer.stderr
        emitted = json.loads(Path(json.loads(consumer.stdout)['signals'][0]).read_text())
        assert emitted['failureKey'] == expected_signal['failureKey']
        assert emitted['source']['sha256'] == expected_signal['source']['sha256']
    # Every consumer has completed. Leave this new disposable work tree absent.
    final = json.loads(cli('release-work', CHANGE, run_id))
    assert final['completed'] and not root.exists()
    assert next(r for r in assets.read_index(ROOT, CHANGE)['runs'] if r['runId'] == run_id) == record
    assert all(Path(path).is_file() and assets.file_digest(path) == checksum for path, checksum in originals.items())
    assert json.loads(cli('verify-archive', CHANGE, package['archiveId']))['valid']
    proof = {'passed': True, 'assetRef': reference, 'archiveId': package['archiveId'],
        'historicalFilesKept': 100, 'probeFiles': len(record['files']), 'workFilesAbsent': True,
        'netWorkLogicalBytesRemoved': final['deletedBytes'], 'archiveBytesAdded': package['bytes'],
        'releaseAttempts': [first['releaseId'], final['releaseId']], 'validateDidNotRestore': True,
        'sameBoundRoot': True, 'sameFailureKey': True, 'sameSourceSha256': True, 'allMtimesPreserved': True,
        'originalRunRecordUnchanged': True, 'newProductExecution': False,
        'note': 'Two releases with one recovery; net bytes count the final absent work copy once, not physical disk space.'}
    proof_file.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(proof, ensure_ascii=False, indent=2))


if __name__ == '__main__': main()
