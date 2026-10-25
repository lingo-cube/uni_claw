using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Host.Runtime;

public sealed partial class CoverageReportDelivery
{
    // Single canonical implementation. Each accepted request pins a self-contained copy.
    public const string GeneratorSource = """
        import argparse, hashlib, json, pathlib, stat, zipfile
        p = argparse.ArgumentParser()
        p.add_argument('--run-dir', required=True); p.add_argument('--out-dir', required=True)
        p.add_argument('--source-label', required=True); p.add_argument('--strict', action='store_true')
        a = p.parse_args()
        root = pathlib.Path(a.run_dir); output = pathlib.Path(a.out_dir)
        raw = (root / 'coverage-source.json').read_bytes()
        source = json.loads(raw)
        if source.get('schemaVersion') != 'uniclaw.coverage-report.source.v1' or source.get('ownerRef') != 'coverage-run:' + a.source_label:
            raise ValueError('coverage-source-owner-mismatch')
        if source.get('discoveryProblem') is not None: raise ValueError('coverage-source-incomplete')
        material = source['material']; identity = material['identity']
        run_id = identity.get('runId')
        if not isinstance(run_id, str) or not run_id or run_id != run_id.strip() or len(run_id) > 1024:
            raise ValueError('coverage-source-run-id-invalid')
        for field in ('runRecordingId', 'evidenceRecordingId', 'worldRecordingId'):
            if not isinstance(identity.get(field), str) or len(identity[field]) != 32 or any(c not in '0123456789abcdefABCDEF' for c in identity[field]):
                raise ValueError('coverage-source-identity-invalid')
        report = json.loads(material['reportJson']); steps = json.loads(material['stepsJson'])
        if not isinstance(report, dict) or not isinstance(steps, list): raise ValueError('coverage-source-shape')
        for key in ('status', 'items', 'coverageRate', 'stepSuccessRate', 'uncoveredItems', 'stepsExecuted', 'stepsVerified', 'consultRounds'):
            if key not in report: raise ValueError('coverage-source-field-missing:' + key)
        planned = {row['relativePath']: row for row in source['attachments']}
        if len(planned) != len(source['attachments']): raise ValueError('coverage-source-duplicate')
        with zipfile.ZipFile(root / 'coverage-attachments.zip') as z:
            names = z.namelist()
            if len(set(n.casefold() for n in names)) != len(names): raise ValueError('coverage-attachment-duplicate')
            manifest = json.loads(z.read('manifest.json'))
            if manifest.get('schemaVersion') != 'uniclaw.coverage-report.attachments.v1' or manifest.get('ownerRef') != source['ownerRef'] or manifest.get('sourceSha256') != hashlib.sha256(raw).hexdigest():
                raise ValueError('coverage-attachment-binding')
            rows = {row['relativePath']: row for row in manifest['files']}
            if len(rows) != len(manifest['files']) or set(rows) != set(planned): raise ValueError('coverage-attachment-membership')
            expected = {'manifest.json'}
            for name, row in rows.items():
                if name.startswith('/') or '\\' in name or any(x in ('', '.', '..') for x in name.split('/')):
                    raise ValueError('coverage-attachment-path')
                spec = planned[name]
                if row['purpose'] != spec['purpose'] or row['requiredForDelivery'] != spec['requiredForDelivery']:
                    raise ValueError('coverage-attachment-purpose')
                if not row['present']:
                    if spec['requiredForDelivery'] or spec['wasPresent']: raise ValueError('coverage-attachment-missing')
                    continue
                member = 'files/' + name; expected.add(member); info = z.getinfo(member)
                if info.is_dir() or stat.S_ISLNK(info.external_attr >> 16): raise ValueError('coverage-attachment-link')
                if info.file_size != row['bytes'] or info.file_size > 128 * 1024 * 1024: raise ValueError('coverage-attachment-size')
                data = z.read(member); sha = hashlib.sha256(data).hexdigest()
                if sha != row['sha256'] or (spec.get('expectedSha256') is not None and sha != spec['expectedSha256']):
                    raise ValueError('coverage-attachment-hash')
            if set(names) != expected: raise ValueError('coverage-attachment-membership')
        output.mkdir(parents=True, exist_ok=True)
        (output / 'coverage-report.json').write_bytes(material['reportJson'].encode('utf-8'))
        (output / 'coverage-steps.json').write_bytes(material['stepsJson'].encode('utf-8'))
        print(json.dumps({'coverageReport': 'coverage-report.json', 'coverageSteps': 'coverage-steps.json'}))
        """;

    private async Task<string> InstallGeneratorAsync(CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(GeneratorSource);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var path = RequiredReportDeliveryPaths.ResolveSourcePath(_storeRoot, ".generators/coverage-" + hash + ".py");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { await WriteOriginalAsync(path, bytes, token).ConfigureAwait(false); }
        catch (IOException) when (File.Exists(path))
        { if (await HashFileAsync(path, token).ConfigureAwait(false) != hash) throw new InvalidDataException("coverage-generator-install-conflict"); }
        return path;
    }
}
