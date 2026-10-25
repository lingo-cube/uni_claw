#!/usr/bin/env python3
"""Read-only verification of the slice2 source/evidence package; no model/device calls."""
import argparse
import hashlib
import json
import tarfile
from pathlib import Path
from xml.etree import ElementTree

HERE = Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads((HERE / path).read_text())


def counters(path):
    node = ElementTree.parse(HERE / path).getroot().find('{*}ResultSummary/{*}Counters')
    return {key: int(value) for key, value in node.attrib.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write-result', action='store_true')
    args = parser.parse_args()
    binding = read('build-binding-v2.json')
    candidate = read(binding['candidate'])
    entry = read('entry.json')
    checks = {
        'entry_sources': all(sha(HERE / 'entry-source' / p) == h for p, h in entry['source_sha256'].items()),
        'candidate_sources': all(sha(HERE / 'candidate-v2' / p) == h for p, h in candidate['files'].items()),
        'base_archive': sha(HERE / binding['base_source_archive']) == binding['base_source_archive_sha256'],
    }
    readonly = read(binding['readonly_root'] + '/manifest.json')
    mismatches = []
    with tarfile.open(HERE / binding['base_source_archive']) as archive:
        for path, expected in binding['inputs_sha256'].items():
            if path in candidate['files']:
                actual = sha(HERE / 'candidate-v2' / path)
            elif path in readonly['files']:
                actual = sha(HERE / binding['readonly_root'] / path)
            elif path in binding['slice1_overlay_paths']:
                actual = sha(HERE / binding['slice1_overlay_root'] / path)
            else:
                actual = hashlib.sha256(archive.extractfile(path).read()).hexdigest()
            if actual != expected:
                mismatches.append(path)
    checks['complete_build_inputs'] = not mismatches and len(binding['inputs_sha256']) == 1007
    host = counters('pure-host-v2.trx')
    dsh = counters('pure-dsh-v2c.trx')
    checks['host23'] = host['total'] == host['passed'] == 23 and host['failed'] == 0
    checks['dsh50'] = dsh['total'] == dsh['passed'] == 50 and dsh['failed'] == 0
    checks['source_owner_red'] = counters('tool-tests/report-owner-mismatch-red-2026-10-25T183710.trx')['failed'] == 2
    checks['host_only_kill_red'] = counters('orphan-tests/orphan-red-v1.trx')['failed'] == 1
    checks['shutdown_red'] = counters('shutdown-tests/report-work-shutdown-red-20261025-01.trx')['failed'] == 2
    runtime = HERE / 'pure-runtime-evidence-v2'
    phases = list((runtime / 'crash-tests').glob('*/immutables-summary.json'))
    checks['four_kill_phases'] = len(phases) == 4 and all(json.loads(p.read_text())['PriorFilesPreserved'] for p in phases)
    checks['actual_kill_observed'] = all(json.loads(p.with_name('kill-observed.json').read_text())['UsedKill'] for p in phases)
    orphaned = list((runtime / 'orphan-tests').glob('*/observed.json'))
    checks['host_only_kill_no_overlap'] = bool(orphaned) and all(
        row['HostOnlyKill'] and row['OrphanAliveDuringRetry'] and len(row['CallsDuringOrphan']) == 1
        and not row['BlockedResult']['Ok'] and row['FinalResult']['Ok'] and row['RepeatedResult']['Ok']
        and row['BusinessResultUnchanged'] and len(row['FinalGeneratorCalls']) == 2
        for row in (json.loads(p.read_text()) for p in orphaned))
    closed = list((runtime / 'shutdown-tests').glob('*/close-summary.json'))
    checks['real_python_close'] = bool(closed) and all(
        row['GeneratorExited'] and row['CloseDrainSettled'] and not row['UnconfirmedOperations']
        and row['FailedStatus'] == 'Failed' and row['SourceFingerprint'] and row['NewRequirementRejected'] == 'report-host-closing'
        for row in (json.loads(p.read_text()) for p in closed))
    checks['input_cli2'] = 'Ran 2 tests' in (HERE / 'input-contract-final-v1.log').read_text() and '\nOK' in (HERE / 'input-contract-final-v1.log').read_text()
    checks['legacy_golden'] = 'CHECK-PASS' in (HERE / 'legacy-golden-final-v2.log').read_text()
    checks['reviews_present'] = all((HERE / ('review-' + axis + '-v2.md')).is_file() for axis in ['spec', 'standards'])
    prior = read('../slice1/final-artifacts-v6.json')
    checks['slice1_unchanged'] = all(sha(HERE.parent / 'slice1' / p) == h for p, h in prior.items())
    sealed = HERE / 'final-artifacts-v2.json'
    if sealed.exists():
        checks['sealed_artifacts_unchanged'] = all(sha(HERE / p) == h for p, h in read(sealed.name).items())
    result = {
        'level': 'DETERMINISTIC+SCENARIO', 'candidate': binding['candidate'],
        'method': 'Frozen source archive/overlays, public report/HTTP/tool seams, real child-process kill/reopen and close, independent two-axis reviews',
        'expected': 'Host23/DSH50/CLI2 pass; source identity bound; no orphan overlap; original committed files retained; slice1 seal unchanged',
        'actual': checks, 'passed': all(checks.values()), 'input_mismatches': mismatches,
        'host': host, 'dsh': dsh, 'build_input_count': len(binding['inputs_sha256']),
        'assembly_count': len(binding['assemblies_sha256']),
        'limits': 'First run-report buyer only. No full-suite-green, live DSH/model/device, power-loss, performance-gain, core inverse-history or parent completion claim.'
    }
    if args.write_result:
        with (HERE / 'verification-v2.json').open('x') as output:
            json.dump(result, output, ensure_ascii=False, indent=2)
            output.write('\n')
    print(json.dumps({'passed': result['passed'], 'checks': checks}, ensure_ascii=False))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
