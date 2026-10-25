#!/usr/bin/env python3
"""Create a private candidate index/build view; never stages the user's index."""
import json
import os
import subprocess
import sys
from pathlib import Path
REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import AssetRun, load_layout, fingerprints, atomic_json, file_digest
if (HERE / 'draft-run.json').exists(): raise FileExistsError('Do not overwrite an existing draft run')
source, inputs, provenance = fingerprints(REPO, [], 'joint-publication-draft', 'build-only scope/dependency check', False)
run = AssetRun(REPO, load_layout(REPO), 'RUNTIME-STORAGE-002', 'joint publication private index and clean build view', source, inputs)
index = run.directory('scratch') / 'candidate.index'
view = run.directory('scratch') / 'view'
env = os.environ.copy(); env['GIT_INDEX_FILE'] = str(index)
subprocess.run(['git', 'read-tree', 'HEAD'], cwd=REPO, env=env, check=True)
scope = json.loads((HERE / 'candidate-code-scope.json').read_text())['files']
paths = [path for path in scope if (REPO / path).is_file()]
subprocess.run(['git', 'add', '--', *paths], cwd=REPO, env=env, check=True)
# Export only the candidate source/test/tool/build surfaces, without unrelated raw history assets.
all_paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=REPO, env=env).decode().split('\0')
exports = [p for p in all_paths if p and (p.startswith(('src/', 'tests/', 'tools/', 'schemas/', 'product/', 'scenarios/'))
           or '/' not in p and p.endswith(('.props', '.targets', '.json', '.sln', '.slnx', '.config', '.md', '.yaml')))]
view.mkdir(parents=True)
subprocess.run(['git', 'checkout-index', '--prefix=' + str(view) + '/', '--stdin', '-z'], input=('\0'.join(exports) + '\0').encode(), cwd=REPO, env=env, check=True)
tree = subprocess.check_output(['git', 'write-tree'], cwd=REPO, env=env, text=True).strip()
atomic_json(run.path('results', 'provenance.json'), provenance)
atomic_json(HERE / 'draft-run.json', {'runId': run.run_id, 'runRoot': str(run.root), 'index': str(index), 'view': str(view), 'tree': tree,
    'exportedFiles': len(exports), 'candidatePaths': paths, 'status': 'draft, not accepted or committed'})
print(json.dumps({'runId': run.run_id, 'index': str(index), 'view': str(view), 'tree': tree, 'exportedFiles': len(exports)}, indent=2))
