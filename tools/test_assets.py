"""Development-only asset layout, immutable local bindings and run inventories.

No product storage dependency and no implicit garbage collection. JSON layout
profiles live locally; shared inventories identify a binding by SHA256.
"""
from __future__ import annotations
import datetime
import contextlib
import fcntl
import functools
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import uuid

KINDS = ('raw', 'logs', 'results', 'scratch')
KEY = re.compile(r'^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$')


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest()


def file_digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for part in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(part)
    return h.hexdigest()


def key(value):
    if not isinstance(value, str) or not KEY.fullmatch(value) or value in ('.', '..'):
        raise ValueError('invalid path key: ' + repr(value))
    return value


def relative(value):
    if (not isinstance(value, str) or not value or '\\' in value or
            any(p in ('', '.', '..') for p in value.split('/')) or PurePosixPath(value).is_absolute()):
        raise ValueError('expected a nonempty relative path without dot segments: ' + repr(value))
    return Path(value)


def overlaps(first, second):
    # The supported local filesystem may be case-insensitive (macOS).
    first, second = Path(str(first).casefold()), Path(str(second).casefold())
    return first == second or first in second.parents or second in first.parents


def checked(root, rel):
    """Reject links, including in nonexistent destinations' existing parents."""
    root = Path(root)
    if root.is_symlink():
        raise ValueError('symlink root: ' + str(root))
    current = root
    for part in relative(str(rel)).parts:
        current = current / part
        if current.is_symlink():
            raise ValueError('symlink asset path: ' + str(current))
    if not current.resolve().is_relative_to(root.resolve()):
        raise ValueError('asset path escapes root')
    return current


def atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        with temp.open('x', encoding='utf-8') as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write('\n'); stream.flush(); os.fsync(stream.fileno())
        os.replace(temp, path)
    finally:
        if temp.exists(): temp.unlink()


def profile_home():
    return Path(os.environ.get('XDG_CONFIG_HOME', str(Path.home() / '.config'))) / 'uniclaw'


def binding_home():
    # Layout discovery can change with CLI/env/profile. Historical binding
    # discovery cannot: keep this machine registry independent of all three.
    return Path.home() / '.local/state/uniclaw/test-asset-bindings'


class Layout:
    def __init__(self, repo, data, selection):
        required = {'version', 'parentRoot', 'repositoryKey', 'workPath', 'archivePath', 'categoryPaths'}
        if not isinstance(data, dict) or set(data) != required or type(data['version']) is not int or data['version'] != 1:
            raise ValueError('layout requires exactly version=1, parentRoot, repositoryKey, workPath, archivePath, categoryPaths')
        if not isinstance(data['parentRoot'], str) or not Path(data['parentRoot']).is_absolute():
            raise ValueError('parentRoot must be absolute')
        self.parent = Path(data['parentRoot']).resolve()
        self.repository_key = key(data['repositoryKey'])
        work, archive = relative(data['workPath']), relative(data['archivePath'])
        if overlaps(work, archive):
            raise ValueError('workPath and archivePath overlap')
        categories = data['categoryPaths']
        if not isinstance(categories, dict) or set(categories) != set(KINDS):
            raise ValueError('categoryPaths must declare raw, logs, results, scratch')
        self.categories = {name: relative(value) for name, value in categories.items()}
        if any(path.parts[0].casefold() == 'layout.json' for path in self.categories.values()):
            raise ValueError('category collides with Run layout metadata')
        for name, path in self.categories.items():
            if any(name != other and overlaps(path, p) for other, p in self.categories.items()):
                raise ValueError('category paths overlap')
        self.work_root = checked(self.parent, work / self.repository_key)
        self.archive_base = checked(self.parent, archive / self.repository_key)
        repo = Path(repo).resolve()
        if any(p.is_relative_to(repo) or repo.is_relative_to(p) for p in (self.work_root, self.archive_base)):
            raise ValueError('asset roots must be outside and separate from the repository')
        self.data = dict(data, parentRoot=str(self.parent))
        self.digest = digest(self.data)
        self.selection = selection

    def archive_root(self, change):
        return checked(self.parent, self.archive_base.relative_to(self.parent) / key(change))

    def run_root(self, change, run):
        return checked(self.parent, self.work_root.relative_to(self.parent) / key(change) / 'runs' / key(run))

    def resolved(self):
        return {**self.data, 'configDigest': self.digest, 'selection': self.selection,
                'workRoot': str(self.work_root), 'archiveRoot': str(self.archive_base)}

    def bind(self):
        path = binding_home() / self.repository_key / (self.digest + '.json')
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.pending')
        try:
            with temporary.open('x', encoding='utf-8') as stream:
                json.dump(self.data, stream, sort_keys=True)
                stream.flush(); os.fsync(stream.fileno())
            try:
                # Atomic create-if-absent: another Change only sees full JSON.
                os.link(temporary, path)
            except FileExistsError:
                if json.loads(path.read_text()) != self.data:
                    raise ValueError('immutable layout binding changed')
        finally:
            if temporary.exists(): temporary.unlink()


def load_layout(repo, explicit=None):
    env = os.environ.get('UNICLAW_TEST_ASSET_CONFIG')
    selected = explicit if explicit is not None else env if env is not None else profile_home() / 'test-assets.json'
    if not str(selected): raise ValueError('asset config path is empty')
    path = Path(selected).expanduser()
    try:
        data = json.loads(path.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise ValueError(f'asset config unavailable/invalid ({path}): {error}') from error
    return Layout(repo, data, 'cli' if explicit is not None else 'environment' if env is not None else 'local-profile')


def index_path(repo, change):
    path = checked(Path(repo).resolve(), Path('changes') / key(change))
    if not (path / 'state.md').is_file():
        raise ValueError('asset owner requires an existing Change state: ' + change)
    return checked(path, 'assets.json')


def read_index(repo, change):
    path = index_path(repo, change)
    if not path.exists():
        return {'schemaVersion': 'test-assets.v1', 'changeId': change, 'runs': [], 'baselineRefs': [], 'archives': []}
    value = json.loads(path.read_text())
    if value.get('schemaVersion') != 'test-assets.v1' or value.get('changeId') != change:
        raise ValueError('asset index schema/owner mismatch')
    return value


@contextlib.contextmanager
def task_access(repo, change, exclusive=False, blocking=True):
    """Outer asset-collection gate; index and Run locks are always acquired inside."""
    path = checked(index_path(repo, change).parent, 'assets.task.lock')
    with path.open('a') as lock:
        flags = fcntl.LOCK_EX if exclusive else fcntl.LOCK_SH
        if not blocking: flags |= fcntl.LOCK_NB
        fcntl.flock(lock, flags)
        yield


def update_index(repo, change, action):
    path = index_path(repo, change)
    with task_access(repo, change):
        with checked(path.parent, 'assets.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            value = read_index(repo, change)
            action(value)
            atomic_json(path, value)


def serialized_run(action):
    """Task gate precedes the existing Run lock, then short index writes."""
    @functools.wraps(action)
    def locked(repo, change, run_id, *args, **kwargs):
        with task_access(repo, change):
            run_record(repo, change, run_id)
            path = checked(index_path(repo, change).parent, 'assets.' + key(run_id) + '.lock')
            with path.open('a') as lock:
                fcntl.flock(lock, fcntl.LOCK_EX)
                return action(repo, change, run_id, *args, **kwargs)
    return locked


def run_record(repo, change, run_id):
    index = read_index(repo, change)
    if key(run_id) in index.get('retiredRuns', {}):
        raise ValueError('asset Run is retiring/retired; original inventory is audit only: ' + run_id)
    record = next((r for r in index['runs'] if r['runId'] == run_id), None)
    if record is None: raise ValueError('unknown asset Run')
    return record


def locate_run(repo, change, run_id):
    record = run_record(repo, change, run_id)
    return bound_layout(repo, record).run_root(change, run_id)


def bound_layout(repo, record):
    binding = record['layout']
    namespace, config_digest = key(binding['repositoryKey']), binding['configDigest']
    if not re.fullmatch(r'[a-f0-9]{64}', config_digest): raise ValueError('invalid config digest')
    path = binding_home() / namespace / (config_digest + '.json')
    data = json.loads(path.read_text())
    layout = Layout(repo, data, 'immutable-binding')
    if layout.digest != config_digest or layout.repository_key != namespace: raise ValueError('binding digest mismatch')
    return layout


def asset_path(repo, change, run_id, relative_path):
    record = run_record(repo, change, run_id)
    item = next((f for f in record['files'] if f['path'] == relative_path), None)
    if item is None: raise ValueError('path is not inventoried')
    path = checked(locate_run(repo, change, run_id), relative_path)
    if any(a['runId'] == run_id for a in read_index(repo, change)['archives']):
        from test_asset_archives import restore_run, validate_available
        root = locate_run(repo, change, run_id)
        if any(not checked(root, item['path']).exists() for item in record['files']):
            restore_run(repo, change, run_id)
        available = validate_available(repo, change, record)
        if not available['valid']:
            raise ValueError('archived bytes/consumer validation pending: ' + '; '.join(available['problems']))
    if not path.is_file() or path.stat().st_size != item['bytes'] or file_digest(path) != item['sha256']:
        raise ValueError('asset missing or digest mismatch; original binding retained')
    return path


def validate_run(repo, change, run_id):
    if key(run_id) in read_index(repo, change).get('retiredRuns', {}):
        return {'valid': False, 'retired': True, 'problems': ['explicit task asset retirement; original inventory is audit only'], 'files': 0}
    root = locate_run(repo, change, run_id)
    record = next(r for r in read_index(repo, change)['runs'] if r['runId'] == run_id)
    problems = []
    for item in record['files']:
        try:
            path = checked(root, item['path'])
            if not path.is_file() or path.stat().st_size != item['bytes'] or file_digest(path) != item['sha256']:
                problems.append(item['path'])
        except (OSError, ValueError): problems.append(item['path'])
    if any(a['runId'] == run_id for a in read_index(repo, change)['archives']):
        from test_asset_archives import validate_available
        return validate_available(repo, change, record)
    return {'valid': not problems, 'problems': problems, 'files': len(record['files'])}


class AssetRun:
    def __init__(self, repo, layout, change, purpose, source_digest, input_digest, retry_of=None, baseline_refs=(), work_release_reason=None):
        self.repo, self.layout, self.change = Path(repo), layout, key(change)
        self.run_id = 'run-' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S') + '-' + uuid.uuid4().hex
        self.root = layout.run_root(change, self.run_id)
        self.batch = digest([purpose, source_digest, input_digest, layout.digest])
        self.record = {'runId': self.run_id, 'purpose': purpose, 'sourceDigest': source_digest,
                       'inputDigest': input_digest, 'configDigest': layout.digest, 'batchId': self.batch,
                       'layout': {'logicalRoot': 'work', 'repositoryKey': layout.repository_key, 'configDigest': layout.digest},
                       'files': [], 'startedAt': datetime.datetime.now(datetime.timezone.utc).isoformat()}
        if work_release_reason is not None:
            if purpose != 'verification':
                raise ValueError('work release is only supported for new verification Runs')
            if not isinstance(work_release_reason, str) or not work_release_reason.strip():
                raise ValueError('work release requires an explicit creator reason')
            self.record['workReleasePolicy'] = {'schemaVersion': 'test-asset-work-release.v1', 'reason': work_release_reason,
                'readThrough': 'test-assets:path-ref', 'externalPhysicalConsumers': 'none'}
        def register(index):
            if index.get('assetRetirement'):
                raise ValueError('task asset collection is retiring/retired; new Runs are disabled')
            for reference in baseline_refs:
                if reference not in index['baselineRefs']:
                    index['baselineRefs'].append(reference)
            if retry_of:
                previous = next((r for r in index['runs'] if r['runId'] == key(retry_of)), None)
                if previous is None or previous['batchId'] != self.batch: raise ValueError('retryOf must reference an existing run in the same batch')
                self.record['retryOf'] = retry_of
            layout.bind()
            self.root.mkdir(parents=True, exist_ok=False)
            for kind in KINDS: self.directory(kind).mkdir(parents=True, exist_ok=False)
            atomic_json(self.root / 'layout.json', layout.data)
            index['runs'].append(self.record)
        update_index(repo, change, register)

    def directory(self, kind):
        return checked(self.layout.parent, self.root.relative_to(self.layout.parent) / self.layout.categories[kind])

    def path(self, kind, name):
        path = checked(self.directory(kind), name)
        path.parent.mkdir(parents=True, exist_ok=True)
        return path

    def finish(self, summary):
        with task_access(self.repo, self.change):
            run_record(self.repo, self.change, self.run_id)
            return self._finish(summary)

    def _finish(self, summary):
        """Call after real consumers; retired data is never recreated."""
        checked(self.layout.parent, self.root.relative_to(self.layout.parent))
        receipt = {'schemaVersion': 'test-asset-finalization.v1', 'changeId': self.change,
                   'runId': self.run_id, 'operation': summary.get('operation', 'verification'), 'completed': bool(summary.get('consumersCompleted')),
                   'kept': [], 'archived': [], 'deleted': [], 'pending': [], 'deletedBytes': 0}
        files = []
        metadata_paths = {self.path('results', 'summary.json'), self.path('results', 'finalization.json')}
        for path in sorted(self.root.rglob('*')):
            if path.is_symlink(): raise ValueError('symlink in run inventory: ' + str(path))
            if not path.is_file() or path in metadata_paths: continue
            relative_path = path.relative_to(self.root)
            kind = next((k for k, p in self.layout.categories.items() if relative_path.is_relative_to(p)), 'results')
            item = {'path': relative_path.as_posix(), 'kind': kind, 'bytes': path.stat().st_size,
                    'sha256': file_digest(path), 'retention': 'task'}
            files.append(item)
            receipt['kept'].append({**item, 'reason': 'verification consumers and acceptance evidence'})
        if not summary.get('consumersCompleted'):
            receipt['pending'].append({'reason': 'not all verification consumers completed; all run files retained'})
        atomic_json(self.path('results', 'summary.json'), summary)
        atomic_json(self.path('results', 'finalization.json'), receipt)
        for name in ('summary.json', 'finalization.json'):
            path = self.path('results', name)
            files.append({'path': path.relative_to(self.root).as_posix(), 'kind': 'results', 'bytes': path.stat().st_size,
                          'sha256': file_digest(path), 'retention': 'task'})
        self.record.update(files=files, summary=summary, finalization=self.path('results', 'finalization.json').relative_to(self.root).as_posix())
        def finish(index):
            for i, record in enumerate(index['runs']):
                if record['runId'] == self.run_id:
                    index['runs'][i] = self.record
                    return
            raise ValueError('registered asset run disappeared')
        update_index(self.repo, self.change, finish)
        return receipt


def fingerprints(repo, scenario_ids, scope, intent, live):
    """Bind actual local code bytes, not just HEAD; never copy build trees."""
    source_files = []
    for directory in ('src', 'tests', 'tools'):
        base = repo / directory
        if not base.exists(): continue
        for current, dirs, names in os.walk(base):
            dirs[:] = sorted(d for d in dirs if d.lower() not in ('bin', 'obj', 'testresults', '__pycache__', '.git', 'node_modules', 'runs', 'evidence') and not d.startswith('runs-'))
            for name in sorted(names):
                path = Path(current) / name
                if path.suffix in ('.cs', '.csproj', '.py', '.props', '.targets') or (directory == 'tools' and not path.suffix):
                    # Large recorded inputs are bound separately by their real consumer.
                    if 'Fixtures' in path.parts: continue
                    if path.is_file() and not path.is_symlink():
                        source_files.append({'path': path.relative_to(repo).as_posix(), 'sha256': file_digest(path)})
    for name in ('UniClaw.Kernel.slnx', 'Directory.Build.props', 'Directory.Packages.props', 'global.json'):
        path = repo / name
        if path.is_file(): source_files.append({'path': name, 'sha256': file_digest(path)})
    inputs = []
    for path in [repo / 'testsets/simulation-baseline/manifest.json', *[repo / 'scenarios' / (s + '.json') for s in scenario_ids]]:
        if path.is_file(): inputs.append({'path': path.relative_to(repo).as_posix(), 'sha256': file_digest(path)})
    # Fixture contents can affect test results even when the selected scenario is unchanged.
    for base in sorted((repo / 'tests').glob('*/Fixtures')):
        for current, dirs, names in os.walk(base):
            dirs[:] = sorted(d for d in dirs if d not in ('bin', 'obj', 'TestResults', '__pycache__'))
            for name in sorted(names):
                path = Path(current) / name
                if path.is_file() and not path.is_symlink(): inputs.append({'path': path.relative_to(repo).as_posix(), 'sha256': file_digest(path)})
    options = {'scope': scope, 'intent': intent, 'live': live, 'scenarioIds': scenario_ids}
    provenance = {'schemaVersion': 'test-asset-provenance.v1', 'algorithm': 'sha256',
                  'sourceFiles': sorted(source_files, key=lambda i: i['path']), 'inputFiles': sorted(inputs, key=lambda i: i['path']),
                  'options': options, 'sourcePreservation': 'live repository retained; not an immutable full-source archive'}
    return digest(provenance['sourceFiles']), digest([provenance['inputFiles'], options]), provenance
