"""Development-only begin/scope/finish/replay operations over test asset runs."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

from test_assets import AssetRun, Layout, asset_path, atomic_json, checked, digest, file_digest, key, load_layout, read_index, update_index

ROOT = Path(__file__).resolve().parents[1]
STOP_WAIT_SECONDS = 5


class _ReplayCancelled(Exception):
    pass


def _json(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'))


def _local_path(value):
    path = Path(value).expanduser()
    return (ROOT / path).resolve() if not path.is_absolute() else path.resolve()


def _entry(value, logical_index):
    path = _local_path(value)
    exists = path.is_file() and not path.is_symlink()
    item = {'requested': value, 'localPath': str(path), 'exists': exists}
    if exists:
        item['sha256'] = file_digest(path)
        if path.is_relative_to(ROOT):
            item['logicalRoot'], item['path'] = 'repository', path.relative_to(ROOT).as_posix()
        else:
            item['logicalRoot'], item['path'] = f'external-{logical_index}', path.name
    return item


def _baseline(entry):
    if not entry['exists']:
        return None
    return {name: entry[name] for name in ('logicalRoot', 'path', 'sha256')}


def _paths(layout, change, run):
    root = layout.run_root(change, run)
    return {'changeId': change, 'runId': run, 'root': str(root),
            **{kind: str(checked(layout.parent, root.relative_to(layout.parent) / layout.categories[kind])) for kind in ('raw', 'logs', 'results', 'scratch')},
            'configDigest': layout.digest}


def _validate_protected(layout, roots):
    for value in roots:
        protected = _local_path(value)
        # resolve/case-fold checks are inherited from Layout for asset roots; compare
        # resolved paths here so aliases cannot make an input root look distinct.
        for asset_root in (layout.work_root, layout.archive_base):
            a, b = str(protected).casefold(), str(asset_root.resolve()).casefold()
            if a == b or a.startswith(b + os.sep) or b.startswith(a + os.sep):
                raise ValueError('protected root overlaps test asset work/archive root: ' + str(protected))


def begin(change, operation, config, retry_of, sources, inputs, context, protected_roots, source_entries=None, input_entries=None, work_release_reason=None):
    if operation not in ('diagnosis', 'replay', 'verification'):
        raise ValueError('unsupported asset operation')
    layout = load_layout(ROOT, config)
    _validate_protected(layout, protected_roots)
    source_entries = source_entries if source_entries is not None else [_entry(value, i) for i, value in enumerate(sources)]
    input_entries = input_entries if input_entries is not None else [_entry(value, i) for i, value in enumerate(inputs)]
    # Collector implementation changes also start a new verification batch.
    collector_files = [Path(__file__).resolve(), Path(__file__).with_name('test_assets.py').resolve(), ROOT / 'tools/test-assets']
    source_entries = list(source_entries)
    known_paths = {entry['localPath'] for entry in source_entries}
    for path in collector_files:
        if str(path) not in known_paths:
            source_entries.append(_entry(str(path), len(source_entries)))
            known_paths.add(str(path))
    provenance = {'schemaVersion': 'test-asset-provenance.v1', 'algorithm': 'sha256', 'operation': operation,
                  'sourceFiles': source_entries, 'inputFiles': input_entries, 'context': context,
                  'sourcePreservation': 'inputs remain owned by their original repository or external root'}
    refs = [{**ref, 'role': 'original-source'} for entry in source_entries + input_entries if (ref := _baseline(entry)) is not None]
    run = AssetRun(ROOT, layout, change, operation, digest(source_entries), digest([input_entries, context]), retry_of,
                   work_release_reason=work_release_reason)
    def protect(index):
        record = next(item for item in index['runs'] if item['runId'] == run.run_id)
        record['protectedRefs'] = refs
    update_index(ROOT, change, protect)
    atomic_json(run.path('raw', 'provenance.json'), provenance)
    return _paths(layout, change, run.run_id)


def _record(change, run):
    return next((item for item in read_index(ROOT, change)['runs'] if item['runId'] == key(run)), None)


def _layout_for(record):
    binding = record['layout']
    from test_assets import binding_home
    path = binding_home() / binding['repositoryKey'] / (binding['configDigest'] + '.json')
    layout = Layout(ROOT, json.loads(path.read_text()), 'immutable-binding')
    if layout.digest != binding['configDigest'] or layout.repository_key != binding['repositoryKey']:
        raise ValueError('binding digest mismatch')
    return layout


def scope(change, run):
    record = _record(change, run)
    if not record:
        raise ValueError('unknown asset run')
    if 'summary' in record or 'finalization' in record or 'finalizationPending' in record:
        raise ValueError('scope only permits an open run')
    return _paths(_layout_for(record), change, run)


def _source_stability(layout, root):
    path = root / layout.categories['raw'] / 'provenance.json'
    if not path.is_file():
        return {'measured': False, 'reason': 'begin provenance unavailable'}
    provenance = json.loads(path.read_text())
    entries = provenance.get('sourceFiles', []) + provenance.get('inputFiles', [])
    if not entries:
        return {'measured': False, 'reason': 'no explicit source/input paths were provided'}
    changed = []
    for entry in entries:
        target = Path(entry['localPath'])
        now_exists = target.is_file() and not target.is_symlink()
        if now_exists != entry.get('exists') or (now_exists and file_digest(target) != entry.get('sha256')):
            changed.append(entry['requested'])
    return {'measured': True, 'stable': not changed, 'changed': changed}


def _mark_pending(change, run, error, summary=None):
    def action(index):
        record = next(item for item in index['runs'] if item['runId'] == run)
        record['finalizationPending'] = str(error)
        if summary is not None: record['summary'] = dict(summary)
    update_index(ROOT, change, action)


def finish(change, run_id, supplied):
    record = _record(change, run_id)
    if not record:
        raise ValueError('unknown asset run')
    retry_pending = 'finalizationPending' in record
    if 'summary' in record and not retry_pending:
        raise ValueError('completed run cannot be finalized again')
    layout = _layout_for(record)
    root = layout.run_root(change, run_id)
    summary = dict(record['summary']) if retry_pending and 'summary' in record else dict(supplied)
    summary.setdefault('operation', record['purpose'])
    stability = _source_stability(layout, root)
    summary.setdefault('sourceStable', stability.get('stable') if stability['measured'] else None)
    summary.setdefault('sourceStability', stability)
    instance = AssetRun.__new__(AssetRun)
    instance.repo, instance.layout, instance.change, instance.run_id, instance.root, instance.record = ROOT, layout, change, run_id, root, record
    try:
        receipt = instance.finish(summary)
        if retry_pending:
            def clear(index):
                item = next(value for value in index['runs'] if value['runId'] == run_id)
                item.pop('finalizationPending', None)
            update_index(ROOT, change, clear)
        return {'changeId': change, 'runId': run_id, 'completed': receipt['completed'], 'finalization': instance.record['finalization']}, 0
    except (OSError, ValueError) as error:
        _mark_pending(change, run_id, error, summary)
        return {'changeId': change, 'runId': run_id, 'completed': False, 'finalizationPending': str(error)}, 1


def path_ref(value):
    prefix = 'test-assets:'
    if not value.startswith(prefix):
        raise ValueError('expected test-assets:CHANGE/RUN/run-relative-path')
    pieces = value[len(prefix):].split('/', 2)
    if len(pieces) != 3:
        raise ValueError('expected test-assets:CHANGE/RUN/run-relative-path')
    return asset_path(ROOT, pieces[0], pieces[1], pieces[2])


def _stop_owned(process):
    """Stop only the session created for this replay, including a surviving child."""
    def alive():
        try:
            os.killpg(process.pid, 0)
            return True
        except (ProcessLookupError, PermissionError):
            return False
    try: os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError: return
    except PermissionError:
        # A constrained host may forbid group signalling even for a child it
        # created.  Fall back to that child only; never signal an unrelated pid.
        if process.poll() is None:
            process.terminate()
            try: process.wait(timeout=STOP_WAIT_SECONDS)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait()
        return
    deadline = time.monotonic() + STOP_WAIT_SECONDS
    while alive() and time.monotonic() < deadline:
        time.sleep(.02)
    if alive():
        try: os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError: return
        deadline = time.monotonic() + STOP_WAIT_SECONDS
        while alive() and time.monotonic() < deadline:
            time.sleep(.02)
    if process.poll() is None:
        try: process.wait(timeout=1)
        except subprocess.TimeoutExpired: pass


def _declared_case_materials(case):
    case_path = _local_path(case)
    identity = _entry(str(case_path), 0)
    sources, inputs = [identity], []
    try:
        document = json.loads(case_path.read_text())
        for item in document.get('inputs', []):
            target = checked(case_path.parent, item['path'])
            inputs.append({'requested': item['path'], 'localPath': str(target), 'exists': target.is_file() and not target.is_symlink(),
                           'logicalRoot': 'sealed-case', 'path': item['path'],
                           'sha256': file_digest(target) if target.is_file() and not target.is_symlink() else None,
                           'declaredSha256': item['sha256'], 'declared': True})
        for item in document.get('realization', []):
            target = _local_path(item['path'])
            sources.append({'requested': item['path'], 'localPath': str(target), 'exists': target.is_file() and not target.is_symlink(),
                            'logicalRoot': 'repository', 'path': item['path'], 'sha256': file_digest(target) if target.is_file() and not target.is_symlink() else None,
                            'declaredSha256': item['sha256']})
        baseline = document.get('baseline', {})
        if isinstance(baseline, dict) and isinstance(baseline.get('path'), str):
            target = _local_path(baseline['path'])
            sources.append({'requested': baseline['path'], 'localPath': str(target), 'exists': target.is_file() and not target.is_symlink(),
                            'logicalRoot': 'repository', 'path': baseline['path'],
                            'sha256': file_digest(target) if target.is_file() and not target.is_symlink() else None,
                            'declaredSha256': baseline.get('sha256'), 'role': 'case-baseline-current'})
    except (OSError, json.JSONDecodeError, KeyError, TypeError) as error:
        identity['caseReadError'] = str(error)
    return identity, sources, inputs


def _read_trx(path):
    outcomes, failures = {}, []
    root = ET.parse(path).getroot()
    for element in root.iter():
        if element.tag.split('}')[-1] != 'UnitTestResult':
            continue
        outcome = element.get('outcome', 'Unknown')
        outcomes[outcome] = outcomes.get(outcome, 0) + 1
        if outcome == 'Failed':
            message = next((child.text or '' for child in element.iter() if child.tag.split('}')[-1] == 'Message'), '')
            failures.append({'name': element.get('testName', ''), 'message': message})
    return outcomes, failures


def replay(change, case, config, retry_of):
    cancel_requested = [False]
    previous_sigterm, previous_sigint = signal.getsignal(signal.SIGTERM), signal.getsignal(signal.SIGINT)
    def cancelled(_number, _frame):
        cancel_requested[0] = True
    signal.signal(signal.SIGTERM, cancelled)
    signal.signal(signal.SIGINT, cancelled)
    try:
        identity, sources, inputs = _declared_case_materials(case)
        started = begin(change, 'replay', config, retry_of, [], [], {'case': {key: identity[key] for key in ('logicalRoot', 'path', 'sha256') if key in identity}}, [], sources, inputs)
        raw, logs = Path(started['raw']), Path(started['logs'])
        out = raw / 'replay'
        command = [sys.executable, str(ROOT / 'tools/evolution-case.py'), 'replay', '--case', str(_local_path(case)), '--out-dir', str(out)]
        status, cli_exit, carrier = ('cancelled', 130, None) if cancel_requested[0] else ('unusable', 2, None)
        process = None
        try:
            if not cancel_requested[0]:
                with (logs / 'evolution-case.txt').open('w') as output:
                    process = subprocess.Popen(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
                    deadline = time.monotonic() + 125
                    while process.poll() is None and not cancel_requested[0] and time.monotonic() < deadline:
                        try: cli_exit = process.wait(timeout=min(.1, max(0, deadline - time.monotonic())))
                        except subprocess.TimeoutExpired: pass
                    if cancel_requested[0]: status, cli_exit = 'cancelled', 130
                    elif process.poll() is None: status, cli_exit = 'timeout', 124
                    else: cli_exit = process.poll()
        except OSError as error:
            (logs / 'evolution-case.txt').write_text(str(error))
        finally:
            if process is not None: _stop_owned(process)
        replay_json = out / 'replay.json'
        consumers_completed = False
        consumer_errors = []
        if replay_json.is_file() and status not in ('timeout', 'cancelled'):
            try:
                payload = json.loads(replay_json.read_text())
                if not isinstance(payload, dict) or not isinstance(payload.get('status'), str): raise ValueError('invalid replay.json shape')
                status = payload['status']; carrier = payload.get('carrierExitCode')
                consumers_completed = True
            except (OSError, ValueError, TypeError) as error:
                status = 'unusable'
                consumer_errors.append({'consumer': 'replay-json', 'error': str(error)})
        trx = out / 'case.trx'
        trx_state, trx_outcomes, failures = 'missing', {}, []
        if trx.is_file():
            try:
                trx_outcomes, failures = _read_trx(trx)
                trx_state = 'readable'
            except (OSError, ET.ParseError) as error:
                trx_state = 'invalid'
                consumer_errors.append({'consumer': 'trx', 'error': str(error)})
        consumers_completed = consumers_completed and trx_state == 'readable'
        result = {'changeId': change, 'runId': started['runId'], 'operation': 'replay', 'status': status, 'cliExitCode': cli_exit, 'carrierExitCode': carrier, 'trx': trx_state,
                  'trxOutcomes': trx_outcomes, 'failures': failures, 'consumersCompleted': consumers_completed,
                  'case': {key: identity[key] for key in ('logicalRoot', 'path', 'sha256') if key in identity}}
        if consumer_errors: result['consumerErrors'] = consumer_errors
        try:
            finalized, _ = finish(change, started['runId'], result)
            result['finalization'] = finalized.get('finalization')
            if finalized.get('finalizationPending'): result['finalizationPending'] = finalized['finalizationPending']
        except (OSError, ValueError, KeyError, TypeError) as error:
            result['finalizationPending'] = str(error)
            try: _mark_pending(change, started['runId'], error, result)
            except (OSError, ValueError, KeyError): pass
        return result, cli_exit
    finally:
        signal.signal(signal.SIGTERM, previous_sigterm)
        signal.signal(signal.SIGINT, previous_sigint)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    begin_parser = commands.add_parser('begin'); begin_parser.add_argument('change'); begin_parser.add_argument('--operation', required=True)
    begin_parser.add_argument('--asset-config'); begin_parser.add_argument('--retry-of'); begin_parser.add_argument('--source', action='append', default=[]); begin_parser.add_argument('--input', action='append', default=[]); begin_parser.add_argument('--context'); begin_parser.add_argument('--protected-root', action='append', default=[])
    begin_parser.add_argument('--work-release-reason', help='Creator declares no external physical-path consumer; future reads must use path-ref. Only for this new Run.')
    scope_parser = commands.add_parser('scope'); scope_parser.add_argument('change'); scope_parser.add_argument('run')
    finish_parser = commands.add_parser('finish'); finish_parser.add_argument('change'); finish_parser.add_argument('run'); finish_parser.add_argument('--summary', required=True)
    ref_parser = commands.add_parser('path-ref'); ref_parser.add_argument('ref')
    replay_parser = commands.add_parser('replay'); replay_parser.add_argument('change'); replay_parser.add_argument('--case', required=True); replay_parser.add_argument('--asset-config'); replay_parser.add_argument('--retry-of')
    args = parser.parse_args()
    try:
        if args.command == 'begin': result, code = begin(args.change, args.operation, args.asset_config, args.retry_of, args.source, args.input, json.loads(args.context) if args.context else None, args.protected_root, work_release_reason=args.work_release_reason), 0
        elif args.command == 'scope': result, code = scope(args.change, args.run), 0
        elif args.command == 'finish': result, code = finish(args.change, args.run, json.loads(args.summary))
        elif args.command == 'path-ref': print(path_ref(args.ref)); return 0
        else: result, code = replay(args.change, args.case, args.asset_config, args.retry_of)
        print(_json(result)); return code
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
        print('ASSET_ERROR=' + str(error), file=sys.stderr); return 2


if __name__ == '__main__': sys.exit(main())
