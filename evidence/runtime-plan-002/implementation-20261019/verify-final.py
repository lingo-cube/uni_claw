"""Final deterministic closure and preservation audit; test execution is separate."""
import pathlib, json, hashlib, subprocess, re, importlib.util
root=pathlib.Path(__file__).resolve().parents[3]; out=pathlib.Path(__file__).parent
entry=json.loads((out/'entry.json').read_text()); result={'checks':[], 'failures':[]}
def check(name,ok,actual):
 result['checks'].append({'name':name,'passed':bool(ok),'actual':actual})
 if not ok:result['failures'].append(name)
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
check('head-preserved',subprocess.check_output(['git','rev-parse','HEAD'],cwd=root).decode().strip()==entry['head'],entry['head'])
missing=[item['path'] for item in entry['files'] if not (root/item['path']).is_file()]
check('all-entry-files-preserved',not missing,missing)
protected=[item for item in entry['files'] if item['path'].startswith(('changes/RUNTIME-PLAN-001/','evidence/runtime-plan-001/')) or item['path']=='docs/adr/0053-composable-product-plan-with-bounded-local-execution.md']
changed=[item['path'] for item in protected if not (root/item['path']).exists() or digest(root/item['path'])!=item['sha256']]
check('001-history-and-ADR0053-unchanged',not changed,{'checked':len(protected),'changed':changed})
oldscenes=json.loads((out/'scenario-entry.json').read_text());scene_changes=[]; carriers={}
for old in oldscenes:
 current=json.loads((root/old['path']).read_text())
 if current.get('id')!=old['id'] or current.get('expectations')!=old['expectations'] or current.get('execution')!=old['execution']:scene_changes.append(old['path'])
 if old['execution'].get('kind')=='none' and digest(root/old['path'])!=old['sha256']:scene_changes.append(old['path']+':none-bytes-changed')
 kind=current.get('execution',{}).get('kind');carriers[kind]=carriers.get(kind,0)+1
check('historical-scenario-identities-expectations-carriers-and-none-bytes-unchanged',not scene_changes,{'changed':scene_changes,'carriers':carriers})
canonical=root/'schemas/agt-002/product-protocol.schema.json';mirror=root/'dsh/uniclaw-decision-channel/schema/product-protocol.schema.json'
schema_hash=hashlib.sha256(canonical.read_bytes().rstrip(b'\n')).hexdigest()
recorded=(mirror.parent/'schema-hash.txt').read_text().strip()
check('schema-mirror-and-hash',canonical.read_bytes()==mirror.read_bytes() and schema_hash==recorded,schema_hash)
spec=importlib.util.spec_from_file_location('prompt_hash',root/'tools/prompt-manifest-hash.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
p=root/'product/prompt/uniagent-prod';d=root/'dsh/uniclaw-decision-channel/prompt';ph=mod.compute(p);manifest=json.loads((p/'manifest.json').read_text())
check('prompt-mirror-manifest-hash',ph==(p/'prompt-hash.txt').read_text().strip()==mod.compute(d)==(d/'prompt-hash.txt').read_text().strip() and all((p/name).read_bytes()==(d/name).read_bytes() for name in ['manifest.json',*manifest['segments']]),{'revision':manifest['promptRevision'],'hash':ph})
check('manifest-schema-hash',manifest['protocol']['schemaHash']==schema_hash,manifest['protocol'])
profile=(root/'product/profiles/uniagent-prod.yaml').read_text();revision=int(re.search(r'^profileRevision:\s*(\d+)',profile,re.M)[1])
check('profile-pin',ph in profile and revision==19,{'revision':revision,'promptHash':ph})
for name in ['model-routing.yaml','.agents/skills/uniflow/SKILL.md','schemas/work-item.schema.json']:
 old=next((item for item in entry['files'] if item['path']==name),None)
 prior=old['sha256'] if old else hashlib.sha256(subprocess.check_output(['git','show','HEAD:'+name],cwd=root)).hexdigest()
 check('shared-routing-contract-unchanged:'+name,digest(root/name)==prior,prior)
review_files=json.loads((out/'review-3/files.json').read_text())
drift=[item['path'] for item in review_files if digest(root/item['path'])!=item['currentSha256']]
check('review-3-source-snapshot-matches-current',not drift,{'files':len(review_files),'drift':drift})
current_source=subprocess.check_output(['python3','tools/scenario_certify.py','--print-source-hash'],cwd=root).decode().strip()
check('certified-final-source-hash-matches-current',current_source==(out/'runtime-source-hash.txt').read_text().strip(),current_source)
whitespace=subprocess.run(['git','diff','--check'],cwd=root,capture_output=True,text=True)
check('diff-whitespace',whitespace.returncode==0,whitespace.stdout+whitespace.stderr)
allowed_changes={item['path'] for item in review_files}|{'changes/RUNTIME-PLAN-002/state.md','changes/INDEX.md'}|{item['path'] for item in oldscenes if item['execution']['kind']=='golden-bundle'}
entry_changes=[item['path'] for item in entry['files'] if (root/item['path']).is_file() and digest(root/item['path'])!=item['sha256']]
unrelated=[path for path in entry_changes if path not in allowed_changes]
check('unrelated-entry-bytes-preserved',not unrelated,{'changedOwnedFiles':len(entry_changes),'unrelatedChanges':unrelated})
prior_paths={item['path'] for item in entry['files']}
tracked_changes=subprocess.check_output(['git','diff','--name-only','HEAD'],cwd=root).decode().splitlines()
new_unexpected=[path for path in tracked_changes if path not in prior_paths and path not in allowed_changes]
check('no-unexpected-new-tracked-edits',not new_unexpected,new_unexpected)
result['passed']=not result['failures'];(out/'closure-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'passed':result['passed'],'checks':len(result['checks']),'failures':result['failures']},ensure_ascii=False))
raise SystemExit(0 if result['passed'] else 1)
