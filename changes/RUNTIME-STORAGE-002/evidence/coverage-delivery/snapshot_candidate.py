#!/usr/bin/env python3
"""Freeze only the Coverage WorkItem file list against its exact entry."""
import difflib
import hashlib
import json
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
old=json.loads((HERE/'candidate-v1.json').read_text())
paths=[row['path'] for row in old['files']]
paths += ['tests/UniClaw.Host.Tests/RequiredReportCoverageReviewTests.cs', 'tests/UniClaw.Host.Tests/RequiredReportCoverageIdentityTests.cs']
rows=[];patch=[]
for path in paths:
    data=(ROOT/path).read_bytes()
    rows.append({'path':path,'sha256':hashlib.sha256(data).hexdigest()})
    before=HERE/'before'/path
    patch.extend(difflib.unified_diff(before.read_text().splitlines(True) if before.exists() else [],
        data.decode().splitlines(True),fromfile='a/'+path if before.exists() else '/dev/null',tofile='b/'+path))
(HERE/'candidate.json').write_text(json.dumps({'files':rows,'comparison':old['comparison'],
    'existingTestCounterpart': {'path':'tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs',
    'integration':'HEAD public sealed class -> public sealed partial class only; no parallel fixture edits'}},indent=2)+'\n')
(HERE/'candidate.patch').write_text(''.join(patch))
print(len(rows))
