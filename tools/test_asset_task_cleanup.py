"""Explicit retirement of inventoried Change assets, retaining an audit shell."""
from __future__ import annotations
import copy
from pathlib import Path

from test_assets import bound_layout, checked, digest, file_digest, index_path, read_index
from test_asset_archives import _files, _work_tree, _unpack, consumer_checks
from test_asset_cleanup import _identity


def _binding(index):
    return {key: value for key, value in index.items() if key not in ('taskDeletions', 'assetRetirement', 'retiredRuns')}


def _observed(root):
    if not root.exists() and not root.is_symlink(): return []
    _checked_candidate({'path':str(root)})
    result=[]
    for path in sorted(root.rglob('*')):
        if path.is_dir() and not path.is_symlink(): continue
        result.append({'path':str(path),'bytes':path.lstat().st_size})
    return result


def _inventory(repo, change, index):
    owner=index_path(repo,change).parent
    units={};kept=[];pending=[];absent=[];seen=set();archive_roots={}
    for record in index['runs']:
        run=record['runId']
        if run.casefold() in seen: raise ValueError('duplicate Run identity')
        seen.add(run.casefold())
        unit={'runId':run,'record':record,'files':[],'roots':[],'candidates':[],'observed':[]};units[run]=unit
        try:
            if run in index.get('retiredRuns',{}) and index['retiredRuns'][run].get('status')=='retired':
                unit['retired']=True;continue
            if 'layout' in record:
                layout=bound_layout(repo,record);root=layout.run_root(change,run)
                unit['roots']=[str(root)];unit['observed']=_observed(root)
                archive_roots.setdefault(str(layout.archive_root(change)),[]).append(run)
            else:
                declared=record.get('root')
                if declared=='repository':root=Path(repo)
                elif isinstance(declared,dict) and declared.get('kind')=='repository':root=checked(Path(repo),declared['path'])
                else:raise ValueError('old Run has no explicit repository binding')
            for item in record.get('files',[]):
                path=checked(root,item['path'])
                if 'layout' not in record and (not path.is_relative_to(owner) or path.name in ('state.md','assets.json')):
                    raise ValueError('repository record is outside task asset ownership')
                unit['files'].append(str(path))
            summary=record.get('summary',{})
            if summary.get('consumersCompleted') is not True:unit['active']=True
            if (record.get('finalizationPending') or summary.get('consumersCompleted') is not True or
                not(summary.get('sourceStable') is True or summary.get('sourceFilesUnchanged') is True)):
                raise ValueError('active, pending or unverified-source Run must be retained')
            expected=_files(record)
            if any(item.get('retention') not in ('run','task') for item in expected.values()):
                raise ValueError('unknown retention/ownership declaration')
            if 'layout' in record:present=_work_tree(root,expected,False)
            else:present={name for name in expected if checked(root,name).exists()}
            for name,item in expected.items():
                path=checked(root,name)
                if name not in present:
                    absent.append({'runId':run,'path':str(path),'bytes':item['bytes'],'sha256':item['sha256']});continue
                identity=_identity(path)
                if path.stat().st_size!=item['bytes'] or file_digest(path)!=item['sha256']:
                    raise ValueError('inventoried file changed: '+name)
                unit['candidates'].append({'runId':run,'location':'work' if 'layout' in record else 'repository',
                    'path':str(path),'bytes':item['bytes'],'sha256':item['sha256'],'identity':list(identity)})
            for archive in index.get('archives',[]):
                if archive['runId']!=run:continue
                if 'layout' not in record:raise ValueError('archive without old immutable binding')
                path=checked(layout.archive_root(change),archive['path'])
                unit['roots'].append(str(path));unit['files'].append(str(path))
                if not path.exists():
                    absent.append({'runId':run,'path':str(path),'bytes':archive['bytes'],'sha256':archive['sha256']});continue
                with _unpack(repo,change,record,archive) as material:consumer_checks(repo,material,root,record)
                unit['candidates'].append({'runId':run,'location':'archive','path':str(path),
                    'bytes':archive['bytes'],'sha256':archive['sha256'],'identity':list(_identity(path))})
        except (OSError,ValueError,KeyError,TypeError) as error:
            unit['problem']=str(error);pending.append({'runId':run,'reason':str(error)})
            kept.extend({**item,'runId':run,'reason':str(error)} for item in unit['observed'])
    known={path for unit in units.values() for path in unit['files']}
    for root,runs in archive_roots.items():
        try:unknown=[item for item in _observed(Path(root)) if item['path'] not in known]
        except (OSError,ValueError) as error:unknown=[{'path':root,'reason':str(error)}]
        if unknown:
            kept.extend({**item,'reason':'unregistered archive material retained'} for item in unknown)
            for run in runs:
                units[run]['problem']='archive root contains unknown or unreadable material'
                pending.append({'runId':run,'reason':units[run]['problem']})
    audit=[str(owner/'state.md'),str(owner/'assets.json')]
    for path in sorted(owner.rglob('*')):
        if (path.is_dir() and not path.is_symlink()) or path.name.endswith('.lock') or str(path) in audit:continue
        if str(path) not in known:kept.append({'path':str(path),'reason':'not inventoried as a task-owned Run asset'})
    kept.extend({'path':path,'reason':'minimal state/index/deletion audit retained'} for path in audit)
    return units,kept,pending,absent


def _plan_identity(plan):
    # A saved plan/audit file is retained metadata, not a new deletion candidate.
    return digest({key: value for key,value in plan.items() if key not in ('planId', 'kept')})


def plan_task_delete(repo, change):
    """Inventory only; configuration is resolved from each original Run binding."""
    from test_asset_task_references import scan_task_references
    repo = Path(repo).resolve(); index = read_index(repo, change)
    units, kept, pending, absent = _inventory(repo, change, index)
    targets = {run: unit for run,unit in units.items() if not unit.get('retired')}
    for run, unit in targets.items():
        if run not in index.get('retiredRuns', {}): continue
        previous = next((entry for entry in reversed(index.get('taskDeletions', []))
            if entry.get('runBindings', {}).get(run) == digest(unit['record'])), None)
        if previous is None:
            unit['problem'] = 'retiring Run has no verified original reference footprint'
            pending.append({'runId': run, 'reason': unit['problem']})
        else: unit['skipOwnDocuments'] = True
    refs = scan_task_references(repo, change, targets)
    for run,unit in targets.items():
        if not unit.get('skipOwnDocuments'): continue
        previous = next(entry for entry in reversed(index['taskDeletions']) if entry.get('runBindings', {}).get(run) == digest(unit['record']))
        refs['internal'][run] = previous.get('internal', {}).get(run, [])
    reasons = {run: [unit['problem']] for run,unit in units.items() if unit.get('problem')}
    if any(unit.get('active') for unit in units.values()):
        for run in targets: reasons.setdefault(run,[]).append('unfinished task Run may still consume previous task assets')
    for kind in ('external', 'baselines'):
        for run, values in refs[kind].items(): reasons.setdefault(run, []).extend(values)
    if refs['errors']:
        for run in targets: reasons.setdefault(run, []).extend(refs['errors'])
        pending.extend({'reason': reason} for reason in refs['errors'])
    # Kept consumers keep their same-task sources too.
    def propagate():
        changed = True
        while changed:
            changed = False
            for source, dependencies in refs['internal'].items():
                if source not in reasons: continue
                for target in dependencies:
                    if target not in reasons:
                        reasons[target] = ['required by retained Run ' + source]; changed = True
    propagate()
    eligible = {run for run,unit in targets.items() if run not in reasons}
    incoming = {run: 0 for run in eligible}
    for source,dependencies in refs['internal'].items():
        if source in eligible:
            for target in dependencies:
                if target in eligible: incoming[target] += 1
    order=[]; ready=sorted(run for run,n in incoming.items() if n==0)
    while ready:
        source=ready.pop(0);order.append(source)
        for target in refs['internal'].get(source, []):
            if target in incoming:
                incoming[target]-=1
                if incoming[target]==0: ready.append(target);ready.sort()
    for run in eligible-set(order):
        reasons[run]=['cyclic or unresolved same-task consumer dependency']
        pending.append({'runId':run,'reason':reasons[run][0]})
    propagate()
    for run, values in reasons.items():
        for item in units[run]['candidates']:
            kept.append({**item,'reason':'retained dependency or unresolved ownership','references':sorted(set(values))})
    plan = {'schemaVersion':'test-asset-task-deletion-plan.v1','changeId':change,
        'inventoryDigest':digest(_binding(index)),'runBindings':{r:digest(u['record']) for r,u in units.items()},
        'candidates':[item for run in order if run not in reasons for item in units[run]['candidates']],
        'kept':kept,'pending':pending,'alreadyAbsent':absent,
        'runs':[{'runId':run,'roots':unit['roots'],'status':'retired' if unit.get('retired') else 'keep' if run in reasons else 'delete'} for run,unit in units.items()],
        'deleteOrder':[run for run in order if run not in reasons], 'internal':refs['internal'],
        'external':refs['external'],'baselines':refs['baselines']}
    plan['planId']=_plan_identity(plan)
    return plan


def _portable_receipt(repo, change, receipt):
    """Shared audit identities use the original binding, never a host root."""
    index=read_index(repo,change);locations=[]
    for record in index['runs']:
        if 'layout' not in record:continue
        try:
            layout=bound_layout(repo,record)
            for kind,root in (('work',layout.run_root(change,record['runId'])),('archive',layout.archive_root(change))):
                locations.append((root,kind,record['runId'],record['layout']))
        except (OSError,ValueError,KeyError):continue
    def item(value):
        if isinstance(value,str):value={'path':value,'directory':True}
        result={key:data for key,data in value.items() if key!='identity'}
        path=Path(value['path']) if isinstance(value.get('path'),str) else None
        if path is None or not path.is_absolute():return result
        if path.is_relative_to(Path(repo)):
            result.update(logicalRoot='repository',path=path.relative_to(repo).as_posix());return result
        for root,kind,run,binding in locations:
            if path.is_relative_to(root) and (not value.get('runId') or value['runId']==run):
                result.update(logicalRoot=kind,runId=run,configDigest=binding['configDigest'],repositoryKey=binding['repositoryKey'])
                relative=path.relative_to(root)
                if relative==Path('.'):
                    result.pop('path',None);result['root']=True
                else:result['path']=relative.as_posix()
                return result
        result.pop('path',None);result['unresolvedLocation']=True
        return result
    result=copy.deepcopy(receipt)
    for key in ('kept','deleted','alreadyAbsent','deletedDirectories'):
        if key in result:result[key]=[item(value) for value in result[key]]
    # Diagnostics and reference projections can embed paths inside prose too.
    # Project those with the same old binding as structured asset locations.
    from test_assets import binding_home
    aliases={str(Path(repo)):'[repository]',str(binding_home()):'[binding-registry]'}
    for root,kind,run,binding in locations:
        aliases[str(root)]=f'[{kind}:{change}/{run}@{binding["configDigest"]}]'
    replacements=sorted(aliases.items(),key=lambda pair:len(pair[0]),reverse=True)
    def project(value):
        if isinstance(value,str):
            for physical,logical in replacements:value=value.replace(physical,logical)
            return value
        if isinstance(value,list):return [project(entry) for entry in value]
        if isinstance(value,dict):return {key:project(entry) for key,entry in value.items()}
        return value
    return project(result)


def _save_delete(repo, change, receipt, *, start=False, retiring=(), retired=(), release=()):
    """Caller holds the exclusive task gate; use only the inner index lock."""
    import fcntl
    from test_assets import atomic_json
    path = index_path(repo, change)
    with checked(path.parent, 'assets.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        value = read_index(repo, change)
        if start:
            value.setdefault('assetRetirement', {'operationId': receipt['operationId'], 'creationClosed': True})
        markers = value.setdefault('retiredRuns', {})
        for run in retiring: markers[run] = {'status': 'retiring', 'operationId': receipt['operationId']}
        for run in retired: markers[run] = {'status': 'retired', 'operationId': receipt['operationId']}
        for run in release: markers.pop(run, None)
        attempts = value.setdefault('taskDeletions', [])
        for i, entry in enumerate(attempts):
            if entry['operationId'] == receipt['operationId']:
                attempts[i] = _portable_receipt(repo,change,receipt); break
        else: attempts.append(_portable_receipt(repo,change,receipt))
        atomic_json(path, value)


def _checked_candidate(item):
    # Check the full absolute ancestry, including a moved/symlinked Run parent.
    path = Path(item['path'])
    return checked(Path(path.anchor), path.relative_to(path.anchor))


def _delete_locked(repo, change, expected, receipt):
    from test_asset_task_references import scan_task_references
    current = plan_task_delete(repo, change)
    if current['planId'] != expected.get('planId'):
        receipt['pending'] = [{'reason':'plan is stale; inventory, references or file identities changed'}]
        _save_delete(repo, change, receipt)
        return receipt
    receipt.update(kept=current['kept'], alreadyAbsent=current['alreadyAbsent'],
        runBindings=current['runBindings'], internal=current['internal'],
        external=current['external'], baselines=current['baselines'], recordDigestAlgorithm='sha256')
    original_index=read_index(repo,change)
    previously_retiring=set(original_index.get('retiredRuns',{}))
    units,_,_,_=_inventory(repo,change,original_index)
    targets={run:unit for run,unit in units.items() if not unit.get('retired')}
    for run in current['deleteOrder']: targets[run]['skipOwnDocuments']=True
    receipt['pending']=[{'reason':'task asset retirement in progress; only confirmed unlinks are counted'}]
    _save_delete(repo,change,receipt,start=True,retiring=current['deleteOrder'])
    errors=list(current['pending']); failed=set(); completed=[]
    for run in current['deleteOrder']:
        items=[item for item in current['candidates'] if item['runId']==run]
        deleted_before=len(receipt['deleted'])
        try:
            if any(run in current['internal'].get(source,[]) for source in failed):
                raise ValueError('required by a consumer whose retirement is pending')
            refs=scan_task_references(repo,change,targets)
            blockers=refs['errors']+refs['external'].get(run,[])+refs['baselines'].get(run,[])
            if blockers: raise ValueError('new or unresolved references: '+'; '.join(blockers))
            if 'layout' in units[run]['record']:
                _work_tree(Path(units[run]['roots'][0]),_files(units[run]['record']),False)
            # A complete preflight precedes the first unlink in this Run.
            for item in items:
                path=_checked_candidate(item)
                if tuple(item['identity'])!=_identity(path) or file_digest(path)!=item['sha256']:
                    raise ValueError('asset drift before deletion: '+item['path'])
            for item in sorted(items,key=lambda value:(value['location']=='archive',value['path'])):
                path=_checked_candidate(item)
                if tuple(item['identity'])!=_identity(path) or file_digest(path)!=item['sha256']:
                    raise ValueError('asset drift during deletion: '+item['path'])
                path.unlink()
                receipt['deleted'].append({key:value for key,value in item.items() if key!='identity'})
                receipt['deletedBytes']+=item['bytes']
                _save_delete(repo,change,receipt)
            if 'layout' in units[run]['record']:
                _work_tree(Path(units[run]['roots'][0]),_files(units[run]['record']),False)
            completed.append(run)
            _save_delete(repo,change,receipt,retired=[run])
            # Only empty directories beneath the bound Run or the owned Change.
            boundary=index_path(repo,change).parent
            if 'layout' in units[run]['record']:
                boundary=bound_layout(repo,units[run]['record']).run_root(change,run)
            for item in items:
                directory=Path(item['path']).parent
                while directory.is_relative_to(boundary) and directory!=index_path(repo,change).parent:
                    try: directory.rmdir()
                    except OSError: break
                    if directory==boundary: break
                    directory=directory.parent
            if 'layout' in units[run]['record']:
                layout=bound_layout(repo,units[run]['record'])
                for tree in (layout.run_root(change,run),layout.archive_root(change)):
                    if not tree.exists(): continue
                    _checked_candidate({'path':str(tree)})
                    directories=sorted((p for p in tree.rglob('*') if p.is_dir() and not p.is_symlink()),key=lambda p:len(p.parts),reverse=True)+[tree]
                    for directory in directories:
                        _checked_candidate({'path':str(directory)})
                        try: directory.rmdir()
                        except OSError:
                            if any(directory.iterdir()): continue
                            raise
                        receipt.setdefault('deletedDirectories',[]).append(str(directory))
        except (OSError,ValueError,KeyError,TypeError) as error:
            failed.add(run);errors.append({'runId':run,'reason':str(error)})
            remaining=items[len(receipt['deleted'])-deleted_before:] if len(receipt['deleted'])==deleted_before else [item for item in items if Path(item['path']).exists()]
            receipt['kept'].extend({**item,'reason':'retirement pending: '+str(error)} for item in remaining)
            if 'layout' in units[run]['record']:
                known=set(units[run]['files'])
                receipt['kept'].extend({**item,'runId':run,'reason':'unregistered material retained; retirement pending'}
                    for item in _observed(Path(units[run]['roots'][0])) if item['path'] not in known)
            # A late reference before any removal must leave the intact source readable.
            if len(receipt['deleted'])==deleted_before and run not in previously_retiring:
                _save_delete(repo,change,receipt,release=[run])
    receipt['pending']=errors
    receipt['completed']=not errors
    receipt['retiredRuns']=completed
    _save_delete(repo,change,receipt)
    return receipt


def delete_task_assets(repo, change, plan):
    """Execute this explicit plan, preserving blocked material and the audit shell."""
    import datetime
    import uuid
    from test_assets import task_access
    repo=Path(repo).resolve();index_path(repo,change)
    if (not isinstance(plan,dict) or plan.get('schemaVersion')!='test-asset-task-deletion-plan.v1'
        or plan.get('changeId')!=change or plan.get('planId')!=_plan_identity(plan)):
        raise ValueError('invalid task deletion plan or owner')
    receipt={'schemaVersion':'test-asset-task-deletion-audit.v1','operation':'delete-task-assets',
        'operationId':'delete-'+uuid.uuid4().hex,'changeId':change,'planId':plan['planId'],
        'startedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'completed':False,
        'kept':[],'deleted':[],'alreadyAbsent':[],'pending':[],'deletedBytes':0}
    try:
        with task_access(repo,change,exclusive=True,blocking=False):
            result=_delete_locked(repo,change,plan,receipt)
            return _portable_receipt(repo,change,result)
    except BlockingIOError:
        receipt['pending']=[{'reason':'task assets busy; no mutation or deletion attempted'}]
        receipt['persisted']=False
        return receipt
