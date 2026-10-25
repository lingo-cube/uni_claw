#!/usr/bin/env python3
"""Read-only verification; temporary archive extraction, no restore/build/GC."""
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0, str(ROOT / 'tools'))
import test_assets as assets
import test_asset_archives as archives


def checksum(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    checks = {}
    for item in json.loads((HERE / 'candidate.json').read_text())['files']:
        checks['candidate:' + item['path']] = checksum(ROOT / item['path']) == item['sha256']
    prior = HERE.parent / 'slice2'
    for item in json.loads((prior / 'candidate-v2.json').read_text())['files']:
        source = HERE / 'before' / item['path']
        if not source.exists(): source = ROOT / item['path']
        checks['slice2-preserved:' + item['path']] = checksum(source) == item['sha256']
    for log, count in [('contracts-final-v3.txt', 34), ('verify-entry-final-v3.txt', 15)]:
        text = (HERE / log).read_text()
        checks[log] = f'Ran {count} tests' in text and text.rstrip().endswith('OK')
    total, archived = 0, 0
    for change in ('TEST-ASSET-LAYOUT-001', 'TEST-ASSET-AUTOMATION-001'):
        index = assets.read_index(ROOT, change)
        for record in index['runs']:
            checks[record['runId']] = assets.validate_run(ROOT, change, record['runId'])['valid']
            total += len(record['files'])
        for entry in index['archives']:
            checks[entry['archiveId']] = archives.verify_archive(ROOT, change, entry['archiveId'])['valid']
            archived += 1
    proof = json.loads((HERE / 'real-restore-proof.json').read_text())
    timed = json.loads((HERE / 'mtime-restore-proof.json').read_text())
    checks['native-reader-recovery'] = proof['passed'] and proof['sameFailureKey'] and proof['sameSourceSha256'] and proof['validateDidNotRestore']
    checks['historical-sources94-kept'] = proof['historicalFilesKept'] == timed['historicalFilesKept'] == 94 and not proof['historicalSourcesDeleted']
    checks['mtime-and-staleness-preserved'] = timed['passed'] and timed['allDigestsAndMtimesPreserved'] and timed['staleBefore'] and timed['staleAfter'] and timed['legacyArchiveStillReadable']
    checks['net-source-deletion0'] = proof['netSourceBytesRemoved'] == timed['netSourceBytesRemoved'] == 0
    checks['file-and-archive-count'] = total == 100 and archived == 3
    old_cleanup = json.loads((ROOT / 'changes/ORPHAN-CLEANUP-001/cleanup.json').read_text())
    checks['restored4-source-protection'] = all(checksum(ROOT / item['path']) == item['sha256'] for item in old_cleanup['restoredAfterReview'])
    checks['reviews'] = all((HERE / f'review-{axis}-v3.md').is_file() for axis in ('standards', 'spec'))
    result = {'passed': all(checks.values()), 'checks': checks, 'availableRunFiles': total, 'archives': archived,
              'scope': 'Run archive/recovery, not task GC/source-tree sealing/product execution or new model/device evidence'}
    print(json.dumps(result, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__': sys.exit(main())
