#!/usr/bin/env python3
"""Bounded public World tests, native TRX consumer, immutable asset Run per attempt."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]


def tool(*args):
    result = subprocess.run([sys.executable, str(ROOT / 'tools/test-assets'), *args], cwd=ROOT, text=True, capture_output=True)
    if result.returncode: raise RuntimeError(result.stderr + result.stdout)
    return json.loads(result.stdout)


def finalize(run, phase, code, expected_red=False):
    trx = list(Path(run['raw']).glob('*.trx')); counts = {}; failures = []
    for path in trx:
        for row in ET.parse(path).getroot().iter():
            if row.tag.rsplit('}', 1)[-1] == 'UnitTestResult':
                outcome = row.get('outcome', 'Unknown'); counts[outcome] = counts.get(outcome, 0) + 1
                if outcome == 'Failed': failures.append({'test': row.get('testName'), 'detail': ''.join(row.itertext()).strip()})
    if trx:
        signal = subprocess.run([sys.executable, str(ROOT / 'tools/evolution-signal.py'), 'dev',
            *sum((['--trx', str(p)] for p in trx), []), '--intent', 'expected-red' if expected_red else 'verify',
            '--repo-root', str(ROOT), '--out-dir', str(Path(run['results']) / 'evolution')], cwd=ROOT, text=True, capture_output=True)
        (Path(run['logs']) / 'signal.txt').write_text(signal.stdout + signal.stderr)
        consumed = signal.returncode == 0
    else: consumed = False
    summary = {'operation': 'verification', 'scope': phase, 'status': 'PASS' if code == 0 else 'FAILED',
        'consumerStatus': 'completed' if consumed else 'pending',
        'cliExitCode': code, 'trxOutcomes': counts, 'failures': failures, 'expectedRed': expected_red,
        'consumersCompleted': consumed and bool(counts), 'liveProductTaskExecuted': False, 'deterministicProductTests': True,
        'evidenceKind': 'public deterministic product tests; no live agent/device/model/service'}
    receipt = tool('finish', 'RUNTIME-STORAGE-002', run['runId'], '--summary', json.dumps(summary))
    log_name = 'tests.txt' if (Path(run['logs']) / 'tests.txt').exists() else 'red.txt'
    result = {'runId': run['runId'], 'counts': counts, 'exitCode': code, 'finalization': receipt,
        'logRef': f'test-assets:RUNTIME-STORAGE-002/{run["runId"]}/logs/{log_name}'}
    (HERE / (phase + '-result.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False))
    return code


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('phase'); parser.add_argument('--filter', required=True)
    parser.add_argument('--no-build', action='store_true'); parser.add_argument('--expected-red', action='store_true')
    args = parser.parse_args()
    sources = [*[str(p.relative_to(ROOT)) for p in (ROOT / 'src/UniClaw.Host/Runtime').glob('FileWorldJournal*.cs')], 'src/UniClaw.Host/Runtime/WorldRevisionDeltaCodec.cs',
        *[str(p.relative_to(ROOT)) for p in (ROOT / 'src/UniClaw.Host/Runtime').glob('WorldHistoryVersion*.cs')],
        'tests/UniClaw.Host.Tests/WorldHistoryBaselineTests.cs', 'tests/UniClaw.Host.Tests/WorldHistoryDeltaTests.cs',
        'tests/UniClaw.Host.Tests/WorldHistoryRecoveryTests.cs', 'tests/UniClaw.Host.Tests/WorldOperationReplayTests.cs', str(Path(__file__))]
    sources += ['tests/UniClaw.Host.Tests/' + name + '.cs' for name in ('WorldAsyncStorageTests', 'FileWorldJournalStreamingTests',
        'RunWorldRecoveryConsistencyTests', 'RunRecoveryStorageAbstractionTests', 'StorageClosureFileTests')]
    run = tool('begin', 'RUNTIME-STORAGE-002', '--operation', 'verification',
        *sum((['--source', p] for p in sources), []), '--context', json.dumps({'scope': args.filter, 'phase': args.phase}))
    (HERE / (args.phase + '-run.json')).write_text(json.dumps(run, indent=2) + '\n')
    command = ['dotnet', 'test', 'tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj', '--no-restore',
        '--filter', args.filter, '--results-directory', run['raw'], '--logger', 'trx;LogFileName=world.trx', '--verbosity', 'minimal']
    if args.no_build: command.append('--no-build')
    with (Path(run['logs']) / 'tests.txt').open('w') as log:
        code = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT).returncode
    return finalize(run, args.phase, code, args.expected_red)


if __name__ == '__main__': sys.exit(main())
