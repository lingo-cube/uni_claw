"""Release explicitly disposable, verified archive-backed work copies only.

The archive, task, original result and all source dependencies remain owned by
their original producer. This is neither task deletion nor global GC.
"""
from __future__ import annotations

import datetime
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import uuid
import xml.etree.ElementTree as ET
import zipfile

from test_assets import bound_layout, checked, file_digest, index_path, read_index, serialized_run, update_index
from test_asset_archives import _archive_entry, _file_ok, _files, _record, _unpack, _work_tree, consumer_checks

EXCLUDED_DIRECTORIES = {'.git', 'bin', 'obj', '__pycache__', 'node_modules'}


def _physical_reference(data, root, repo):
    """Structured source/baseline directories may cover the entire Run."""
    if isinstance(data, dict):
        value = data.get('localPath', data.get('path'))
        if isinstance(value, str) and (Path(value).is_absolute() or data.get('logicalRoot', 'repository') in ('repository', 'work')):
            path = Path(value)
            # Consumers resolve external aliases too; lexical abspath can hide
            # a live dependency through an alias outside the repository.
            path = (path if path.is_absolute() else Path(repo) / path).resolve()
            if path == root or path in root.parents or root in path.parents: return True
        return any(_physical_reference(value, root, repo) for value in data.values())
    if isinstance(data, list): return any(_physical_reference(value, root, repo) for value in data)
    return False


def reference_blockers(repo, change, record, root):
    """Read all repository reference surfaces, including ignored plans/WorkItems.

    Exact logical file references are safe through the existing restoration
    seam. Other mentions are deliberately unresolved, not guessed to be stale.
    Creator opt-in separately asserts no external physical-path consumers.
    """
    run_id = record['runId']
    blockers = []
    # rg deliberately does not follow links. A link itself is a live physical
    # dependency, including a directory link covering this Run.
    def scan_error(error): raise error
    for current, directories, files in os.walk(repo, followlinks=False, onerror=scan_error):
        directories[:] = [name for name in directories if name not in EXCLUDED_DIRECTORIES]
        for name in directories + files:
            path = Path(current) / name
            if path.is_symlink():
                target = path.resolve()
                if target == root or target in root.parents or root in target.parents:
                    blockers.append(path.relative_to(repo).as_posix() + ':physical-link')
    if blockers: return sorted(blockers)
    command = ['rg', '--files-with-matches', '--null', '--text', '--fixed-strings', '--hidden', '--no-ignore',
               '--glob', '!.git/**', '--glob', '!**/bin/**', '--glob', '!**/obj/**',
               '--glob', '!**/__pycache__/**', '--glob', '!**/node_modules/**',
               '-e', run_id, '-e', str(root), '--', '.']
    result = subprocess.run(command, cwd=repo, capture_output=True)
    if result.returncode not in (0, 1): raise ValueError('reference scan failed: ' + result.stderr.decode(errors='replace'))
    logical = [f'test-assets:{change}/{run_id}/{item["path"]}'.encode() for item in record['files']]
    patterns = [re.compile(re.escape(value) + rb'''(?=$|[\s"'<>)\],;}])''') for value in sorted(logical, key=len, reverse=True)]
    own_index = index_path(repo, change)
    for name in result.stdout.split(b'\0'):
        if not name: continue
        path = checked(Path(repo), os.fsdecode(name).removeprefix('./'))
        if path == own_index: continue  # Analyze incoming fields separately below.
        if path.name.startswith('assets.') and path.name.endswith('.lock'): continue
        data = path.read_bytes()
        for pattern in patterns: data = pattern.sub(b'<archive-aware-reference>', data)
        if run_id.encode() in data or str(root).encode() in data:
            blockers.append(path.relative_to(repo).as_posix())
    index = read_index(repo, change)
    incoming = {**index, 'runs': [r for r in index['runs'] if r['runId'] != run_id],
                'archives': [a for a in index['archives'] if a['runId'] != run_id], 'workReleases': []}
    incoming['targetProtectedRefs'] = record.get('protectedRefs', [])
    data = json.dumps(incoming, ensure_ascii=False).encode()
    for pattern in patterns: data = pattern.sub(b'<archive-aware-reference>', data)
    if run_id.encode() in data or str(root).encode() in data or _physical_reference(incoming, root, repo):
        blockers.append('changes/' + change + '/assets.json:incoming-source-or-baseline')
    # Directory baseline/protected references may not contain the Run ID.
    incoming_runs = [(change, r) for r in index['runs'] if r['runId'] != run_id]
    for path in Path(repo).glob('changes/*/assets.json'):
        if path == own_index: continue
        checked(Path(repo), path.relative_to(repo))
        other = json.loads(path.read_text())
        refs = [other.get('baselineRefs', []), *[r.get('protectedRefs', []) for r in other.get('runs', [])]]
        if _physical_reference(refs, root, repo): blockers.append(path.relative_to(repo).as_posix() + ':source-or-baseline')
        incoming_runs.extend((path.parent.name, r) for r in other.get('runs', []))
    # External source identities in protectedRefs intentionally contain no host
    # path. Consult the producer's actual provenance/packet JSON, not SHA alone.
    for owner, source_record in incoming_runs:
        bound = 'layout' in source_record
        if bound:
            source_layout = bound_layout(repo, source_record)
            source_root = source_layout.run_root(owner, source_record['runId'])
            documents = {item['path']: item for item in source_record['files'] if item['path'].endswith('.json')}
            provenance = (source_layout.categories['raw'] / 'provenance.json').as_posix()
            if 'summary' not in source_record:
                if not checked(source_root, provenance).is_file():
                    raise ValueError('active Run source provenance not yet available: ' + source_record['runId'])
                documents.setdefault(provenance, None)
        else:
            # Pre-binding, repository-local manifests already exist. Honor
            # their explicit root, inspect current incoming refs, never migrate
            # them or guess an external root from today's local configuration.
            declared = source_record.get('root')
            if declared == 'repository': source_root = Path(repo)
            elif isinstance(declared, dict) and declared.get('kind') == 'repository':
                source_root = checked(Path(repo), declared['path'])
            else: raise ValueError('unbound incoming Run has no explicit repository root: ' + source_record['runId'])
            documents = {item['path']: None for item in source_record['files'] if item['path'].endswith('.json')}
        def inspect(directory):
            for name, item in documents.items():
                path = checked(directory, name)
                if item is not None and not _file_ok(path, item):
                    raise ValueError('incoming consumer JSON missing/changed: ' + source_record['runId'] + '/' + name)
                content = path.read_bytes()
                for pattern in patterns: content = pattern.sub(b'<archive-aware-reference>', content)
                document = json.loads(content)
                if run_id.encode() in content or str(root).encode() in content or _physical_reference(document, root, repo):
                    blockers.append('test-assets:' + owner + '/' + source_record['runId'] + '/' + name + ':source-dependency')
        if all(checked(source_root, name).is_file() for name in documents): inspect(source_root)
        else:
            if not bound: raise ValueError('repository-local incoming JSON missing: ' + source_record['runId'])
            package = _archive_entry(repo, owner, run_id=source_record['runId'])
            with _unpack(repo, owner, source_record, package) as unpacked: inspect(unpacked)
    return sorted(set(blockers))


def _identity(path):
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
        raise ValueError('only exclusive regular work files may be released: ' + str(path))
    return (info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns)


def _save(repo, change, receipt):
    def save(index):
        attempts = index.setdefault('workReleases', [])
        for position, attempt in enumerate(attempts):
            if attempt['releaseId'] == receipt['releaseId']:
                attempts[position] = receipt.copy(); break
        else: attempts.append(receipt.copy())
    update_index(repo, change, save)


@serialized_run
def release_work(repo, change, run_id):
    """No deletion until every guard passes; partial failure has an actual receipt.

    A persisted pending receipt precedes the first unlink. If interrupted, it
    does not claim unconfirmed deletes; retry inventories remaining files.
    """
    record = _record(repo, change, run_id)
    receipt = {'schemaVersion': 'test-asset-finalization.v1', 'operation': 'release-work',
        'releaseId': 'release-' + uuid.uuid4().hex, 'changeId': change, 'runId': run_id,
        'startedAt': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'completed': False,
        'kept': [], 'archived': [], 'deleted': [], 'alreadyAbsent': [], 'pending': [], 'deletedBytes': 0}
    try:
        policy = record.get('workReleasePolicy', {})
        if (policy.get('schemaVersion') != 'test-asset-work-release.v1' or not policy.get('reason') or
                policy.get('readThrough') != 'test-assets:path-ref' or policy.get('externalPhysicalConsumers') != 'none'):
            raise ValueError('Run was not explicitly created for archive-backed work release; historical authorization is not inferred')
        summary = record.get('summary', {})
        if record.get('finalizationPending') or summary.get('consumersCompleted') is not True or summary.get('sourceStable') is not True:
            raise ValueError('finalization, completed consumers and stable sources are required')
        layout = bound_layout(repo, record); root = layout.run_root(change, run_id)
        expected = _files(record)
        present = _work_tree(root, expected, False)
        entry = _archive_entry(repo, change, run_id=run_id)
        if entry.get('formatVersion') != 2: raise ValueError('release requires a timestamp-preserving v2 archive')
        with _unpack(repo, change, record, entry) as extracted:
            receipt['consumerChecks'] = consumer_checks(repo, extracted, root, record)
            for name in present:
                if checked(root, name).stat().st_mtime_ns != checked(extracted, name).stat().st_mtime_ns:
                    raise ValueError('work modification time differs from archive: ' + name)
        blockers = reference_blockers(repo, change, record, root)
        if blockers: raise ValueError('physical/unknown consumer references remain: ' + ', '.join(blockers))
        identities = {name: _identity(checked(root, name)) for name in present}
        receipt['archived'] = [{**entry, 'reason': 'verified original facts; future reads use path-ref'}]
        receipt['alreadyAbsent'] = [expected[name] for name in expected if name not in present]
        receipt['pending'] = [{'reason': 'work release in progress; unconfirmed unlinks are not counted'}]
        _save(repo, change, receipt)
        # Check again after native readers/reference scanning and before unlink.
        if _record(repo, change, run_id) != record: raise ValueError('Run metadata drifted before release')
        if _archive_entry(repo, change, run_id=run_id) != entry: raise ValueError('archive registration drifted before release')
        blockers = reference_blockers(repo, change, record, root)
        if blockers: raise ValueError('new consumer references appeared: ' + ', '.join(blockers))
        _work_tree(root, expected, False)
        # Reopen/check the retained package after all preflight work.
        with _unpack(repo, change, record, entry): pass
        for name in sorted(present):
            blockers = reference_blockers(repo, change, record, root)
            if blockers: raise ValueError('consumer references appeared during release: ' + ', '.join(blockers))
            path = checked(root, name)
            if _identity(path) != identities[name]: raise ValueError('work file identity changed before unlink: ' + name)
            # Hash recheck immediately precedes each unlink; never rehash history.
            if file_digest(path) != expected[name]['sha256'] or _identity(path) != identities[name]:
                raise ValueError('work file content changed before unlink: ' + name)
            path.unlink()
            if path.exists() or path.is_symlink(): raise ValueError('released path reappeared: ' + name)
            receipt['deleted'].append({**expected[name], 'reason': 'verified archive-backed disposable work copy'})
            receipt['deletedBytes'] += expected[name]['bytes']
            _save(repo, change, receipt)
        # Only empty directories of this Run; never recursive deletion.
        if root.exists():
            for directory in sorted((p for p in root.rglob('*') if p.is_dir()), key=lambda p: len(p.parts), reverse=True):
                checked(root, directory.relative_to(root)); directory.rmdir()
            root.rmdir()
        receipt['pending'] = []
        receipt['completed'] = True
    except (OSError, ValueError, KeyError, RuntimeError, ET.ParseError, zipfile.BadZipFile) as error:
        receipt['pending'] = [{'reason': str(error)}]
    _save(repo, change, receipt)
    return receipt
