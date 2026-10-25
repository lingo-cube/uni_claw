using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class ReportGeneratorOrphanTests
{
    [Fact]
    public async Task OrphanedReportGeneratorIsNotStartedTwiceBeforeRelease()
    {
        var repo = RuntimeToolHostTests.RepoRoot();
        var root = Path.Combine(repo, "changes/RUNTIME-STORAGE-002/evidence/slice2/orphan-tests",
            "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var processes = new List<Process>();
        try
        {
            var source = Path.Combine(root, "runs", "source"); Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "facts.json"), "{\"runId\":\"source\",\"status\":\"completed\"}");
            var sentinel = Path.Combine(root, "business-result.txt"); File.WriteAllText(sentinel, "completed:source");
            var before = SHA256.HashData(File.ReadAllBytes(sentinel));
            File.WriteAllText(Path.Combine(root, "report.py"), $$"""
                import json,os,sys,time
                if '--describe-inputs' in sys.argv:
                    print(json.dumps({'schemaVersion':'uniclaw.run-report.inputs.v1','generatorName':'run-report','generatorVersion':1,'files':[{'name':'facts','fileName':'facts.json','strictRequired':False,'requiredForDelivery':True}],'outputFiles':['report.json','report.md']}))
                    sys.exit(0)
                c={{JsonSerializer.Serialize(root)}}
                with open(os.path.join(c,'calls'),'a') as f: f.write(str(os.getpid())+'\n')
                with open(os.path.join(c,'child.pid'),'w') as f: f.write(str(os.getpid()))
                with open(os.path.join(c,'started'),'w') as f: f.write('1')
                while not os.path.exists(os.path.join(c,'release')): time.sleep(.02)
                out=sys.argv[sys.argv.index('--out-dir')+1]
                os.makedirs(out,exist_ok=True)
                with open(os.path.join(out,'report.json'),'w') as f: f.write('{"result":"completed:source"}')
                with open(os.path.join(out,'report.md'),'w') as f: f.write('# completed:source')
                """);
            File.WriteAllText(Path.Combine(root, "registry.yaml"), "version: 1\ntools:\n  run-report:\n    invocation: deterministic-script\n    entry: report.py\n    posture: local-write\n    surfaces: [workbench]\n    status: implemented\n");
            var probe = Path.Combine(repo, "tools/RunRecoveryProbe/bin/Debug/net10.0/UniClaw.RunRecoveryProbe.dll");
            Process Start(string mode)
            {
                var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { probe, mode, root }) info.ArgumentList.Add(arg);
                var p = Process.Start(info)!; processes.Add(p); return p;
            }
            async Task<JsonDocument> Result(Process p)
            {
                await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(0, p.ExitCode);
                return JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "result-" + p.Id + ".json")));
            }
            var writer = Start("write-report-tool");
            await Until(() => File.Exists(Path.Combine(root, "started")) || writer.HasExited);
            if (writer.HasExited) throw new InvalidOperationException(await writer.StandardError.ReadToEndAsync());
            var oldPid = int.Parse(File.ReadAllText(Path.Combine(root, "child.pid")));
            using var orphan = Process.GetProcessById(oldPid);
            writer.Kill(entireProcessTree: false);
            await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(orphan.HasExited);
            var reader = Start("read-report-tool");
            await Until(() => reader.HasExited || File.ReadAllLines(Path.Combine(root, "calls")).Length > 1);
            var callsWhileOldAlive = File.ReadAllLines(Path.Combine(root, "calls"));
            var oldStillAlive = !orphan.HasExited;
            File.WriteAllText(Path.Combine(root, "release"), "1");
            using var blocked = await Result(reader);
            await Until(() => orphan.HasExited);
            var final = Start("read-report-tool");
            using var finalResult = await Result(final);
            var repeat = Start("read-report-tool");
            using var repeated = await Result(repeat);
            var finalCalls = File.ReadAllLines(Path.Combine(root, "calls"));
            File.WriteAllText(Path.Combine(root, "observed.json"), JsonSerializer.Serialize(new
            {
                WriterPid = writer.Id, WriterExit = writer.ExitCode, OrphanPid = oldPid,
                HostOnlyKill = true, OrphanAliveDuringRetry = oldStillAlive, CallsDuringOrphan = callsWhileOldAlive,
                BlockedResult = blocked.RootElement, FinalResult = finalResult.RootElement,
                RepeatedResult = repeated.RootElement, FinalGeneratorCalls = finalCalls,
                BusinessResultUnchanged = before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(sentinel)))
            }, new JsonSerializerOptions { WriteIndented = true }));
            Assert.True(oldStillAlive);
            Assert.False(blocked.RootElement.GetProperty("Ok").GetBoolean());
            Assert.Single(callsWhileOldAlive);
            Assert.True(finalResult.RootElement.GetProperty("Ok").GetBoolean());
            Assert.True(repeated.RootElement.GetProperty("Ok").GetBoolean());
            Assert.Equal(blocked.RootElement.GetProperty("DeliveryId").GetString(), finalResult.RootElement.GetProperty("DeliveryId").GetString());
            Assert.Equal(finalResult.RootElement.GetProperty("DeliveryId").GetString(), repeated.RootElement.GetProperty("DeliveryId").GetString());
            Assert.Equal(2, finalCalls.Length);
            Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(sentinel)));
        }
        finally
        {
            var calls = Path.Combine(root, "calls");
            if (File.Exists(calls)) foreach (var pid in File.ReadAllLines(calls).Distinct())
            {
                try { using var p = Process.GetProcessById(int.Parse(pid)); if (!p.HasExited) p.Kill(true); }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
            }
            foreach (var p in processes)
            {
                try { if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); } }
                catch (InvalidOperationException) { }
                try { File.WriteAllText(Path.Combine(root, "host-" + p.Id + ".log"),
                    await p.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(2)) + "\n" +
                    await p.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(2))); }
                catch (TimeoutException) { }
                p.Dispose();
            }
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.True(condition(), "Report process did not reach the expected state within 10 seconds");
    }
}
