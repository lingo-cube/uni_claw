#!/usr/bin/env python3
"""Reconstruct and test the immutable slice2 candidate in a fresh temporary directory."""
import hashlib
import json
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
HOST_FILTER = 'FullyQualifiedName~RequiredReportDeliveryTests|FullyQualifiedName~RequiredReportCrashTests|FullyQualifiedName~ReportArtifactProjectionTests|FullyQualifiedName~RuntimeRunStoreTests|FullyQualifiedName~AuxiliaryArtifactIsolationTests'
DSH_FILTER = 'FullyQualifiedName~RequiredReportToolTests|FullyQualifiedName~ReportDeliveryEndpointTests|FullyQualifiedName~ReportGeneratorOrphanTests|FullyQualifiedName~ReportWorkShutdownTests|FullyQualifiedName~RuntimeToolHostTests|FullyQualifiedName~RuntimeToolConfigTests|FullyQualifiedName~RuntimeToolHostProcedureTests'


def main():
    subprocess.run([sys.executable, str(HERE / 'verify.py')], check=True)
    binding = json.loads((HERE / 'build-binding-v2.json').read_text())
    root = Path(tempfile.mkdtemp(prefix='uniclaw-storage002-reproduction-')).resolve()
    with tarfile.open(HERE / binding['base_source_archive']) as archive:
        for member in archive:
            target = (root / member.name).resolve()
            if not member.isfile() or not str(target).startswith(str(root) + '/'):
                raise ValueError('invalid-archive-member')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.extractfile(member).read())
    candidate = json.loads((HERE / binding['candidate']).read_text())
    readonly = json.loads((HERE / binding['readonly_root'] / 'manifest.json').read_text())
    overlays = [(binding['slice1_overlay_root'], binding['slice1_overlay_paths']),
                ('candidate-v2', candidate['files']), (binding['readonly_root'], readonly['files'])]
    for source, paths in overlays:
        for path in paths:
            target = root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((HERE / source / path).read_bytes())
    for path, digest in binding['inputs_sha256'].items():
        if hashlib.sha256((root / path).read_bytes()).hexdigest() != digest:
            raise ValueError('input-mismatch:' + path)
    output = root / 'reproduction-results'
    output.mkdir()
    projects = ['tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj', 'tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj']
    for index, project in enumerate(projects):
        with (output / ('build-' + str(index) + '.log')).open('w') as log:
            subprocess.run(['dotnet', 'build', project, '-m:1', '-nr:false'], cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
    for label, project, selection in [('host', projects[0], HOST_FILTER), ('dsh', projects[1], DSH_FILTER)]:
        with (output / (label + '.log')).open('w') as log:
            subprocess.run(['dotnet', 'test', project, '--no-build', '--no-restore', '--filter', selection,
                            '--logger', 'trx;LogFileName=' + label + '.trx', '--results-directory', str(output)],
                           cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
    with (output / 'input-cli.log').open('w') as log:
        subprocess.run([sys.executable, '-m', 'unittest', 'tools.tests.test_run_report_input_contract'],
                       cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
    print(json.dumps({'passed': True, 'source_root': str(root), 'results': str(output)}))


if __name__ == '__main__':
    main()
