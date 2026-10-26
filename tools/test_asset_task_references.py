"""Read-only projection of the consumers that prevent task asset retirement."""
from __future__ import annotations

import json
import os
import re
from pathlib import Path

from test_assets import bound_layout, checked, read_index
from test_asset_archives import _archive_entry, _file_ok, _unpack

_SKIP = {'.git', 'bin', 'obj', '__pycache__', 'node_modules'}
_HISTORICAL = ('entry-source', 'source-snapshot', 'candidate-source')


def _ref(path, kind):
    return path.as_posix() + ':' + kind


def _paths(target):
    """All concrete paths denoting a target; archives are files, never roots."""
    values = []
    for position, value in enumerate(target.get('roots', ())):
        path = Path(value).resolve()
        # Contract: roots[0] is the work root even after release. Archives are
        # concrete files and must never make their shared parent a dependency.
        values.append((path, position == 0))
    for value in target.get('files', ()):
        values.append((Path(value).resolve(), False))
    return values


def _physical_match(data, target, repo=None, document=None):
    """Raw text may name exact concrete paths, never broad parent prefixes."""
    for protected, _ in _paths(target):
        aliases = {str(protected)}
        if repo is not None and protected.is_relative_to(repo):
            relative = protected.relative_to(repo).as_posix()
            aliases.update((relative, './' + relative))
            if document is not None:
                aliases.add(os.path.relpath(protected, document.parent))
        for alias in aliases:
            if re.search(r'(?<![^\s"\'(<\[=:])' + re.escape(alias) + r'(?=$|[\s"\')>\],;:#?])', data):
                return True
    return False


def _mentions_path(value, target, repo):
    """Only explicit path-shaped declarations may establish a physical edge."""
    if isinstance(value, dict):
        for key in ('localPath', 'path'):
            raw = value.get(key)
            if not isinstance(raw, str): continue
            candidate = Path(raw)
            if not candidate.is_absolute() and value.get('logicalRoot', 'repository') in ('repository', 'work'):
                candidate = Path(repo) / candidate
            if candidate.is_absolute():
                candidate = Path(os.path.abspath(candidate)).resolve()
                for protected, is_root in _paths(target):
                    # Structured paths can deliberately name a directory that
                    # covers either a work tree or an archive file.
                    if candidate == protected or protected in candidate.parents or candidate in protected.parents:
                        return True
        return any(_mentions_path(item, target, repo) for item in value.values())
    if isinstance(value, list): return any(_mentions_path(item, target, repo) for item in value)
    return False


def _run_mention(text, run):
    return re.search(r'(?<![A-Za-z0-9_.-])'+re.escape(run)+r'(?![A-Za-z0-9_.-])',text) is not None


def _logical_mentions(data, change, run):
    prefix = 'test-assets:' + change + '/' + run + '/'
    return prefix in data


def _add(mapping, key, value):
    mapping.setdefault(key, []).append(value)


def _consumer_documents(repo, owner, record):
    """Yield registered JSON consumers using their historic binding, never restore."""
    json_files = [item['path'] for item in record.get('files', []) if str(item.get('path', '')).endswith('.json')]
    if not json_files and 'layout' not in record: return []
    entries = {item['path']: item for item in record.get('files', []) if str(item.get('path', '')).endswith('.json')}
    json_files = list(entries)
    if 'layout' not in record:
        root = record.get('root')
        if root == 'repository': base = Path(repo)
        elif isinstance(root, dict) and root.get('kind') == 'repository': base = checked(Path(repo), root['path'])
        else: raise ValueError('unbound incoming Run has no explicit repository root: ' + record.get('runId', '?'))
        result = []
        for name in json_files:
            path = checked(base, name)
            if not path.is_file() or ('sha256' in entries[name] and not _file_ok(path, entries[name])):
                raise ValueError('incoming consumer JSON missing/changed: ' + record.get('runId', '?') + '/' + name)
            result.append((name, path.read_bytes()))
        return result
    layout=bound_layout(repo,record)
    root=layout.run_root(owner,record['runId'])
    if record.get('summary',{}).get('consumersCompleted') is not True:
        provenance=(layout.categories['raw']/'provenance.json').as_posix()
        if not checked(root,provenance).is_file():
            raise ValueError('active source provenance unavailable: '+record['runId'])
        entries.setdefault(provenance,None)
        json_files=list(entries)
    if all(checked(root, name).is_file() for name in json_files):
        result = []
        for name in json_files:
            path = checked(root, name)
            if entries[name] is not None and not _file_ok(path, entries[name]): raise ValueError('incoming consumer JSON missing/changed: ' + record['runId'] + '/' + name)
            result.append((name, path.read_bytes()))
        return result
    entry = _archive_entry(repo, owner, run_id=record['runId'])
    with _unpack(repo, owner, record, entry) as unpacked:
        return [(name, checked(unpacked, name).read_bytes()) for name in json_files]


def scan_task_references(repo, change, targets):
    """Return incoming external/internal/baseline retention edges without mutation."""
    repo = Path(repo).resolve()
    result = {'external': {}, 'internal': {}, 'baselines': {}, 'errors': []}
    target_ids = set(targets)

    def retired(index, run_id):
        value = index.get('retiredRuns', [])
        if isinstance(value,dict):
            return isinstance(value.get(run_id),dict) and value[run_id].get('status')=='retired'
        return run_id in value or any(isinstance(item,dict) and item.get('runId')==run_id and item.get('status')=='retired' for item in value)

    def record_structured(document, location, source_run=None):
        text=json.dumps(document,ensure_ascii=False)
        for target_id,target in targets.items():
            physical=_mentions_path(document,target,repo)
            logical=_logical_mentions(text,change,target_id) or _run_mention(text,target_id)
            if source_run==target_id and not physical:continue  # Self identity is metadata.
            if physical or logical:
                if source_run in target_ids and source_run!=target_id:
                    _add(result['internal'],source_run,target_id)
                else:_add(result['baselines'],target_id,location)

    # Formal baselines and declared original sources are retention declarations,
    # not arbitrary prose mentioning a path.
    for index_file in repo.glob('changes/*/assets.json'):
        owner = index_file.parent.name
        try:
            index = json.loads(index_file.read_text())
            for target_id, target in targets.items():
                for declaration in index.get('baselineRefs', []):
                    if _mentions_path(declaration, target, repo): _add(result['baselines'], target_id, _ref(index_file.relative_to(repo), 'baseline'))
            for source in index.get('runs', []):
                if retired(index, source.get('runId')): continue
                for declaration in source.get('protectedRefs', []):
                    if not isinstance(declaration, dict): continue
                    for target_id, target in targets.items():
                        if _mentions_path(declaration, target, repo):
                            location = _ref(index_file.relative_to(repo), 'original-source')
                            if owner == change and source.get('runId') in target_ids and source['runId'] != target_id:
                                _add(result['internal'], source['runId'], target_id)
                            else:
                                _add(result['baselines'], target_id, location)
                # The supplied target inventory is the authority for the task
                # being retired.  Do not make its historical index evidence an
                # incoming external consumer (or require a stale work tree).
                if owner == change:
                    continue
                try: documents = _consumer_documents(repo, owner, source)
                except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
                    result['errors'].append(f'{index_file.relative_to(repo)}:{source.get("runId", "?")}:{error}')
                    continue
                for name, raw in documents:
                    try: document = json.loads(raw)
                    except json.JSONDecodeError as error:
                        result['errors'].append(f'{owner}/{source.get("runId", "?")}/{name}:{error.msg}')
                        continue
                    record_structured(document, f'test-assets:{owner}/{source.get("runId", "?")}/{name}')
        except (OSError, ValueError, json.JSONDecodeError) as error:
            result['errors'].append(f'{index_file.relative_to(repo)}:{error}')

    # Supplied current candidates are authoritative even before their index is
    # complete. Their JSON provenance creates same-task edges only.
    for source_id, source in targets.items():
        if source.get('skipOwnDocuments') is True:
            continue
        record = source['record']
        try:
            if record.get('layout', {}).get('repositoryKey'):
                documents = _consumer_documents(repo, change, record)
            else:
                documents = []
                for candidate in source.get('files', []):
                    path = Path(candidate)
                    if path.suffix != '.json': continue
                    if not path.is_file(): raise ValueError('target consumer JSON missing: ' + str(path))
                    try: name = path.resolve().relative_to(Path(source['roots'][0]).resolve()).as_posix()
                    except (IndexError, ValueError): name = path.name
                    documents.append((name, path.read_bytes()))
            for name, raw in documents:
                record_structured(json.loads(raw), f'test-assets:{change}/{source_id}/{name}', source_id)
        except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
            result['errors'].append(f'test-assets:{change}/{source_id}:{error}')

    # Plans and WorkItems, including ignored files, are external consumers.
    for current, directories, files in os.walk(repo, followlinks=False):
        directories[:] = [item for item in directories if item not in _SKIP]
        for name in directories + files:
            path = Path(current) / name
            if not path.is_symlink(): continue
            try: resolved = path.resolve()
            except OSError as error:
                result['errors'].append(f'{path.relative_to(repo)}:{error}'); continue
            for target_id, target in targets.items():
                if _mentions_path({'localPath': str(resolved)}, target, repo):
                    _add(result['external'], target_id, _ref(path.relative_to(repo), 'physical-link'))
        for name in files:
            path = Path(current) / name
            relative = path.relative_to(repo)
            if relative.parts[:2] == ('changes', change):
                if relative.name in ('state.md', 'assets.json') or relative.name.endswith('.lock'): continue
                if 'evidence' in relative.parts and not any(part.lower() in ('plans', 'workitems') or part.lower().startswith(('plan.', 'handoff.', 'progress.')) for part in relative.parts[2:]): continue
            if path.name == 'assets.json' and relative.parts[:1] == ('changes',): continue
            if 'evidence' in relative.parts and any(part in ('entry-source', 'source-snapshot') or part.startswith('candidate-source') for part in relative.parts): continue
            try: data = path.read_text(errors='replace')
            except OSError as error: result['errors'].append(f'{relative}:{error}'); continue
            try: structured = json.loads(data)
            except json.JSONDecodeError: structured = None
            if (isinstance(structured, dict) and structured.get('changeId') == change and
                    structured.get('schemaVersion') in ('test-asset-task-deletion-plan.v1', 'test-asset-task-deletion-audit.v1')):
                continue
            for target_id, target in targets.items():
                if _logical_mentions(data,change,target_id):
                    _add(result['external'],target_id,_ref(relative,'logical'))
                elif _run_mention(data,target_id):
                    _add(result['external'],target_id,_ref(relative,'run-identity'))
                # physical paths are searched literally, preserving aliases and directories.
                if _physical_match(data, target, repo, path) or (structured is not None and _mentions_path(structured, target, repo)):
                    _add(result['external'], target_id, _ref(relative, 'physical'))
    for mapping in ('external', 'baselines'):
        result[mapping] = {key: sorted(set(values)) for key, values in result[mapping].items()}
    result['internal'] = {key: sorted(set(values)) for key, values in result['internal'].items()}
    result['errors'] = sorted(set(result['errors']))
    return result
