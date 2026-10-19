"""Review only this session's delta against the preserved dirty entry."""
import pathlib, json, tarfile, subprocess, difflib, hashlib
root=pathlib.Path(__file__).resolve().parents[3]
entry=json.loads((root/'evidence/runtime-plan-001/entry/snapshot.json').read_text())
snapshot=pathlib.Path(entry['snapshot']); manifest=json.loads((snapshot/'manifest.json').read_text())
originals={e['path']:e for e in manifest['files']}
status=subprocess.check_output(['git','status','--porcelain=v1','-z','--untracked-files=all'],cwd=root).split(b'\0')
paths=[]
i=0
while i<len(status):
 row=status[i];i+=1
 if not row:continue
 paths.append(row[3:].decode())
 if row[:1] in (b'R',b'C') or row[1:2] in (b'R',b'C'):i+=1
patch=[]; inventory=[]
with tarfile.open(snapshot/'dirty-files.tar.gz') as archive:
 for name in sorted(set(paths)):
  if not name.startswith(('src/','tests/','dsh/','product/','scenarios/','schemas/agt-002/','docs/architecture/','plans/runtime-plan-001','changes/RUNTIME-PLAN-001/','.dsh/product/uniagent-prod-bindings.yaml','docs/adr/0053-composable-product-plan-with-bounded-local-execution.md','docs/agents/test-emulator.md')):continue
  file=root/name
  if file.is_dir():continue
  after=file.read_bytes() if file.exists() else b''
  if name in originals:
   if hashlib.sha256(after).hexdigest()==originals[name]['sha256']:continue
   try:before=archive.extractfile(name).read()
   except KeyError:raise RuntimeError(f'missing entry snapshot: {name}')
  else:
   check=subprocess.run(['git','show',f'{manifest["head"]}:{name}'],cwd=root,capture_output=True)
   before=check.stdout if check.returncode==0 else b''
  if before==after:continue
  try:a=before.decode();b=after.decode()
  except UnicodeDecodeError:continue
  patch.extend(difflib.unified_diff(a.splitlines(keepends=True),b.splitlines(keepends=True),fromfile='entry/'+name,tofile='current/'+name))
  inventory.append({'path':name,'before':hashlib.sha256(before).hexdigest(),'after':hashlib.sha256(after).hexdigest(),'new':not before})
output=root/'evidence/runtime-plan-001/review';(output/'implementation.patch').write_text(''.join(patch));(output/'inventory.json').write_text(json.dumps(inventory,indent=2)+'\n')
print(json.dumps({'files':len(inventory),'patchBytes':(output/'implementation.patch').stat().st_size}))
