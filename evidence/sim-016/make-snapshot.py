import hashlib, json, pathlib, shutil, subprocess, tempfile, sys
root=pathlib.Path(__file__).resolve().parents[2]
snapshot=pathlib.Path(tempfile.mkdtemp(prefix='uniclaw-sim016-',dir='/private/tmp'))
tracked=subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
extra=subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z','src','tests','scenarios','tools/tests'],cwd=root).decode().split('\0')
legacy=json.loads((root/'evidence/sim-009/copied-legacy-fixtures.json').read_text())
corpus=json.loads((root/'tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json').read_text())
for case in corpus['cases']:
    legacy.extend(case[key]['path'] for key in ['before','after','sequence','settingsTrace','facts','recording'])
popup=json.loads((root/'tests/UniClaw.Host.Tests/Fixtures/PopupReplay/sources.json').read_text())
for episode in popup['episodes']:
    legacy.extend(episode[key]['path'] for key in ['trace','steps'])
    legacy.extend(frame[key]['path'] for frame in episode['frames'] for key in ['xml','png'])
legacy=sorted(set(legacy))
paths=set(tracked+extra+legacy+['changes/SIM-014/state.md','changes/SIM-014/component-contracts.md','changes/SIM-015/state.md','docs/architecture/simulation-baseline-v0.2-c7-component-delta.md','changes/SIM-016/state.md','changes/SIM-017/state.md','changes/SIM-017/design.md'])
manifest={}
for name in sorted(paths):
    if not name or name.startswith(('.git/','evidence/')) and name not in legacy: continue
    src=root/name
    if not src.is_file(): continue
    dst=snapshot/name; dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(src,dst)
    manifest[name]=hashlib.sha256(dst.read_bytes()).hexdigest()
subprocess.run(['git','init','-q',str(snapshot)],check=True)
(pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else root/'evidence/sim-016/final-snapshot.json').write_text(json.dumps({'path':str(snapshot),'originalHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'sha256':manifest,'legacyFixtures':legacy},indent=2)+'\n')
print(snapshot)
