"""Verified Run packages and additive restore. No source deletion or task GC."""
from __future__ import annotations

import contextlib
import hashlib
import json
import os
from pathlib import Path
import runpy
import shutil
import stat
import tempfile
import uuid
import xml.etree.ElementTree as ET
import zipfile

from test_assets import bound_layout, checked, file_digest, key, read_index, relative, serialized_run, update_index

CODE_ROOT = Path(__file__).resolve().parents[1]
HEADER = '__test_assets_archive_v1__.json'
BINDING_KEYS = ('changeId', 'runId', 'purpose', 'sourceDigest', 'inputDigest', 'configDigest', 'batchId', 'layout', 'files', 'summary')


def _record(repo, change, run):
    record = next((r for r in read_index(repo, change)['runs'] if r['runId'] == key(run)), None)
    if record is None: raise ValueError('unknown asset Run')
    return record


def _run_binding(change, record):
    value = {name: change if name == 'changeId' else record[name] for name in BINDING_KEYS}
    for name in ('retryOf', 'startedAt', 'protectedRefs', 'referenceRoleCorrectionRef', 'workReleasePolicy'):
        if name in record: value[name] = record[name]
    return value


def _files(record):
    entries = {}
    folded = set()
    for item in record['files']:
        name = relative(item['path']).as_posix()
        if name == HEADER or name.casefold() in folded: raise ValueError('duplicate/reserved archive path')
        if type(item['bytes']) is not int or item['bytes'] < 0: raise ValueError('invalid asset size')
        entries[name] = item
        folded.add(name.casefold())
    if not entries: raise ValueError('empty/unfinalized Run cannot be archived')
    return entries


def _file_ok(path, item):
    return path.is_file() and path.stat().st_size == item['bytes'] and file_digest(path) == item['sha256']


def _work_tree(root, entries, require_complete):
    present = set()
    if root.exists():
        for path in root.rglob('*'):
            checked(root, path.relative_to(root))
            if path.is_file(): present.add(path.relative_to(root).as_posix())
            elif not path.is_dir(): raise ValueError('nonregular Run entry; preserve the tree: ' + str(path))
    if present - entries.keys(): raise ValueError('Run contains uninventoried files; preserve them')
    for name in present:
        if not _file_ok(checked(root, name), entries[name]): raise ValueError('work asset changed: ' + name)
    if require_complete and present != entries.keys(): raise ValueError('inventoried work asset missing')
    return present


def _archive_entry(repo, change, archive_id=None, run_id=None):
    candidates = [a for a in read_index(repo, change)['archives']
                  if (archive_id is None or a['archiveId'] == key(archive_id)) and (run_id is None or a['runId'] == run_id)]
    if not candidates: raise ValueError('no registered archive for Run')
    return candidates[-1]


def _package(repo, change, record, entry):
    if entry['runId'] != record['runId'] or entry['configDigest'] != record['configDigest']:
        raise ValueError('archive Run/layout mismatch')
    layout = bound_layout(repo, record)
    path = checked(layout.archive_root(change), entry['path'])
    if not _file_ok(path, entry): raise ValueError('archive size/SHA256 mismatch')
    return path


@contextlib.contextmanager
def _unpack(repo, change, record, entry):
    """All membership/type checks precede writes; extraction stays in own temp."""
    package = _package(repo, change, record, entry)
    expected = _files(record)
    with zipfile.ZipFile(package) as archive:
        members = archive.infolist()
        names = [m.filename for m in members]
        if len(set(n.casefold() for n in names)) != len(names): raise ValueError('duplicate ZIP member')
        if set(names) != {*expected, HEADER}: raise ValueError('ZIP membership differs from inventory')
        for member in members:
            relative(member.filename)
            if member.is_dir() or stat.S_ISLNK(member.external_attr >> 16): raise ValueError('ZIP link/directory member refused')
            if member.filename != HEADER and member.file_size != expected[member.filename]['bytes']:
                raise ValueError('ZIP declared size differs from inventory')
        header = archive.getinfo(HEADER)
        if header.file_size != entry['manifestBytes']: raise ValueError('archive manifest size mismatch')
        data = archive.read(HEADER)
        if hashlib.sha256(data).hexdigest() != entry['manifestSha256']: raise ValueError('archive manifest digest mismatch')
        manifest = json.loads(data)
        version = manifest.get('schemaVersion')
        if (version not in ('test-asset-archive.v1', 'test-asset-archive.v2') or manifest.get('archiveId') != entry['archiveId'] or
                manifest.get('run') != _run_binding(change, record)):
            raise ValueError('archive manifest identity/content mismatch')
        expected_version = 2 if version == 'test-asset-archive.v2' else 1
        if entry.get('formatVersion', 1) != expected_version: raise ValueError('archive format version mismatch')
        timestamps = manifest.get('mtimeNs', {}) if expected_version == 2 else {name: 0 for name in expected}
        if set(timestamps) != set(expected) or any(type(value) is not int or not -(2**63) <= value < 2**63 for value in timestamps.values()):
            raise ValueError('invalid archived modification times')
        with tempfile.TemporaryDirectory(prefix='uniclaw-archive-read-') as directory:
            target = Path(directory).resolve()
            for name, item in expected.items():
                path = checked(target, name); path.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(name) as source, path.open('xb') as sink: shutil.copyfileobj(source, sink, 1024 * 1024)
                if not _file_ok(path, item): raise ValueError('unpacked asset digest mismatch: ' + name)
                os.utime(path, ns=(path.stat().st_atime_ns, timestamps[name]))
            yield target


def _reference_path(repo, original, unpacked, value):
    path = Path(value)
    path = path if path.is_absolute() else Path(repo) / path
    # Do not resolve the missing original directory through current config.
    absolute = Path(os.path.abspath(path)).resolve()
    if absolute.is_relative_to(original): return checked(unpacked, absolute.relative_to(original))
    return absolute


def _check_reference(repo, original, unpacked, reference):
    path = _reference_path(repo, original, unpacked, reference['path'])
    if not path.is_file() or path.is_symlink() or file_digest(path) != reference['sha256']:
        raise ValueError('consumer source missing/changed: ' + reference['path'])
    return path


def consumer_checks(repo, root, original, record):
    """Read actual formats with existing consumers; never reexecute a Run."""
    checks = []
    coverage = signal_adapter = None
    for name in _files(record):
        path = checked(root, name)
        if path.suffix.lower() == '.trx':
            if coverage is None:
                coverage = runpy.run_path(str(CODE_ROOT / 'tools/scenario-coverage.py'))
                signal_adapter = runpy.run_path(str(CODE_ROOT / 'tools/evolution-signal.py'))
            outcomes = coverage['parse_trx'](path)
            signals = signal_adapter['scan_dev']([path], Path(repo), context={})
            counts = {}
            for outcome in outcomes.values(): counts[outcome] = counts.get(outcome, 0) + 1
            checks.append({'path': name, 'reader': 'scenario-coverage.parse_trx', 'outcomes': counts})
            checks.append({'path': name, 'reader': 'evolution-signal.scan_dev', 'signals': len(signals)})
        elif path.suffix.lower() == '.json':
            document = json.loads(path.read_text(encoding='utf-8'))
            schema = document.get('schema') if isinstance(document, dict) else None
            if schema in ('uniclaw.diagnosis.packet.v0', 'uniclaw.evolution.signal.v0'):
                import jsonschema
                schema_file = 'diagnosis-packet.schema.json' if schema == 'uniclaw.diagnosis.packet.v0' else 'evolution-signal.schema.json'
                try: jsonschema.validate(document, json.loads((CODE_ROOT / 'schemas' / schema_file).read_text()))
                except jsonschema.ValidationError as error:
                    raise ValueError('consumer schema failed: ' + schema_file + ': ' + error.message) from error
                refs = document['subject']['inputs'] if schema == 'uniclaw.diagnosis.packet.v0' else [document['source']]
                if schema == 'uniclaw.evolution.signal.v0' and document.get('diagnosis'): refs.append(document['diagnosis'])
                for reference in refs:
                    if reference.get('sha256'): _check_reference(repo, original, root, reference)
                checks.append({'path': name, 'reader': schema + ':schema+source-digests', 'references': len(refs)})
            elif schema == 'uniclaw.evolution.replay.v0':
                trx = _reference_path(repo, original, root, document['trx'])
                if not trx.is_file(): raise ValueError('replay TRX reference unavailable')
                checks.append({'path': name, 'reader': 'replay-json+trx-reference', 'status': document['status']})
    if not checks: checks.append({'reader': 'bytes-only', 'limitation': 'no known original consumer in this inventory'})
    return checks


def _remove_created(path, identity):
    if identity is None: return
    try: current = path.lstat()
    except FileNotFoundError: return
    if stat.S_ISREG(current.st_mode) and (current.st_dev, current.st_ino) == identity:
        path.unlink()


@serialized_run
def archive_run(repo, change, run_id):
    record = _record(repo, change, run_id)
    if 'summary' not in record or record.get('finalizationPending'): raise ValueError('Run must be finalized before archive')
    layout = bound_layout(repo, record)
    root = layout.run_root(change, run_id)
    expected = _files(record)
    _work_tree(root, expected, True)
    archive_id = 'archive-' + uuid.uuid4().hex
    if any(a['archiveId'] == archive_id for a in read_index(repo, change)['archives']):
        raise FileExistsError('archive ID already registered')
    directory = layout.archive_root(change); directory.mkdir(parents=True, exist_ok=True)
    package = checked(directory, archive_id + '.zip')
    timestamps = {name: checked(root, name).stat().st_mtime_ns for name in expected}
    manifest = {'schemaVersion': 'test-asset-archive.v2', 'archiveId': archive_id, 'run': _run_binding(change, record),
                'baselineRefs': read_index(repo, change)['baselineRefs'], 'mtimeNs': timestamps}
    data = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
    entry = {'formatVersion': 2, 'archiveId': archive_id, 'runId': run_id, 'logicalRoot': 'archive', 'path': package.name,
             'configDigest': record['configDigest'], 'manifestBytes': len(data), 'manifestSha256': hashlib.sha256(data).hexdigest()}
    published = False
    created_identity = None
    try:
        with package.open('xb') as stream:
            info = os.fstat(stream.fileno()); created_identity = (info.st_dev, info.st_ino)
            with zipfile.ZipFile(stream, 'w', compression=zipfile.ZIP_DEFLATED, strict_timestamps=False) as archive:
                archive.writestr(HEADER, data)
                for name in expected: archive.write(checked(root, name), name)
            stream.flush(); os.fsync(stream.fileno())
        entry.update(bytes=package.stat().st_size, sha256=file_digest(package))
        with _unpack(repo, change, record, entry) as extracted:
            readers = consumer_checks(repo, extracted, root, record)
        _work_tree(root, expected, True)
        if any(checked(root, name).stat().st_mtime_ns != value for name, value in timestamps.items()):
            raise ValueError('source modification times changed during archive')
        entry.update(verified=True, consumerChecks=readers, sourceDeletedByArchive=False)
        def register(index):
            current = next(r for r in index['runs'] if r['runId'] == run_id)
            if _run_binding(change, current) != _run_binding(change, record): raise ValueError('Run inventory drifted during archive')
            if any(a['archiveId'] == archive_id for a in index['archives']): raise FileExistsError('archive ID already registered')
            index['archives'].append(entry)
        update_index(repo, change, register)
        published = True
        return entry
    finally:
        # Only the unique package made by this invocation, never source data.
        if not published:
            try:
                registered = any(all(a.get(k) == entry.get(k) for k in ('archiveId', 'runId', 'configDigest', 'path', 'sha256'))
                                 for a in read_index(repo, change)['archives'])
            except (OSError, ValueError): registered = True
            if not registered: _remove_created(package, created_identity)


def verify_archive(repo, change, archive_id):
    try:
        entry = _archive_entry(repo, change, archive_id)
        record = _record(repo, change, entry['runId'])
        original = bound_layout(repo, record).run_root(change, record['runId'])
        with _unpack(repo, change, record, entry) as extracted:
            try: readers = consumer_checks(repo, extracted, original, record)
            except (OSError, ValueError, KeyError, ET.ParseError) as error:
                return {'valid': False, 'bytesValid': True, 'archiveId': archive_id, 'consumerStatus': 'pending', 'error': str(error)}
        return {'valid': True, 'bytesValid': True, 'archiveId': archive_id, 'files': len(record['files']), 'consumerChecks': readers}
    except (OSError, ValueError, KeyError, ET.ParseError, zipfile.BadZipFile) as error:
        return {'valid': False, 'bytesValid': False, 'archiveId': archive_id, 'error': str(error)}


def _publish_file(source, destination, expected):
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.name + '.restore-' + uuid.uuid4().hex + '.pending')
    created_identity = None
    try:
        with source.open('rb') as incoming, temporary.open('xb') as outgoing:
            info = os.fstat(outgoing.fileno()); created_identity = (info.st_dev, info.st_ino)
            shutil.copyfileobj(incoming, outgoing, 1024 * 1024); outgoing.flush()
            os.utime(temporary, ns=(temporary.stat().st_atime_ns, source.stat().st_mtime_ns))
            os.fsync(outgoing.fileno())
        try: os.link(temporary, destination)
        except FileExistsError:
            if not _file_ok(destination, expected): raise ValueError('concurrent restore destination changed')
    finally:
        _remove_created(temporary, created_identity)


@serialized_run
def restore_run(repo, change, run_id, archive_id=None):
    record = _record(repo, change, run_id)
    entry = _archive_entry(repo, change, archive_id, run_id)
    root = bound_layout(repo, record).run_root(change, run_id)
    expected = _files(record)
    present = _work_tree(root, expected, False)
    restored = []
    reader_error = None
    with _unpack(repo, change, record, entry) as extracted:
        for name, item in expected.items():
            destination = checked(root, name)
            if name not in present:
                _publish_file(checked(extracted, name), destination, item)
                restored.append(name)
        _work_tree(root, expected, True)
        try: readers = consumer_checks(repo, root, root, record)
        except (OSError, ValueError, KeyError, ET.ParseError) as error:
            readers, reader_error = [], str(error)
    return {'bytesValid': True, 'mtimeRestoration': 'preserved' if entry.get('formatVersion', 1) == 2 else 'conservative-epoch-v1',
            'consumerStatus': 'pending' if reader_error else 'verified', 'consumerError': reader_error, 'changeId': change, 'runId': run_id, 'archiveId': entry['archiveId'], 'root': str(root),
            'restored': restored, 'consumerChecks': readers, 'sourceDeleted': False}


def validate_available(repo, change, record):
    """Read-only bytes AND known consumers; work corruption is never masked."""
    expected = _files(record)
    bytes_valid, storage, complete = False, 'unavailable', False
    try:
        root = bound_layout(repo, record).run_root(change, record['runId'])
        present = _work_tree(root, expected, False)
        complete = len(present) == len(expected)
        if complete:
            bytes_valid, storage = True, 'work'
            readers = consumer_checks(repo, root, root, record)
        else:
            entry = _archive_entry(repo, change, run_id=record['runId'])
            with _unpack(repo, change, record, entry) as extracted:
                bytes_valid, storage = True, 'archive'
                readers = consumer_checks(repo, extracted, root, record)
        return {'valid': True, 'bytesValid': True, 'consumerStatus': 'verified', 'consumerChecks': readers,
                'problems': [], 'files': len(expected), 'storage': storage, 'workTreeComplete': complete}
    except (OSError, ValueError, KeyError, ET.ParseError, zipfile.BadZipFile) as error:
        return {'valid': False, 'bytesValid': bytes_valid, 'consumerStatus': 'pending', 'problems': [str(error)],
                'files': len(expected), 'storage': storage, 'workTreeComplete': complete}
