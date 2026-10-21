using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>Bounded real UniAgent → formal codec → Kernel Settings traversal acceptance.</summary>
[Collection("PlanLiveDevice")]
public sealed class SettingsTraversalPlanLiveTests(ITestOutputHelper output)
{
    // This is the exact ExecutionContract objective formed by SettingsCoverageRunner
    // for the frozen two-root scope below. The registration freezes this value before
    // any real consultation; the model is never allowed to rewrite it.
    public const string Objective = "Traverse Android Settings within the current device, account, permissions and system state. "
        + "Enter every configured first-level target: Network & internet; Connected devices. "
        + "Required evidence: rootPage=True, scrollDiscoveredEntries=0, secondLevelPages=0, "
        + "backNavigation=True, repeatedEntries=0. End on the observed Settings root. "
        + "Select every action from the current semantic hierarchy. "
        + "A configured scope result never proves all possible UI states exhaustive.";

    public const string TaskInstruction = "本次仅遍历 Settings 当前根页中配置的 Network & internet 与 Connected devices 两个入口，必须使用 settings.menu Traverse，maxDepth=1，局部操作预算最多128。首个真实响应必须是包含该 Traverse 的 Plan。按 Source 当前投影规则处理候选：导航仅使用 condition=navigation、Navigate，descriptor绑定$candidate、role绑定$candidate或本Source已声明的ui.element、effectClass=tap；开关仅使用 condition=readOnly 的 ReadOnly 操作且不得附带动作；未知候选暂停，不猜测、不放行。返回契约为 ui.element / Navigate up / tap，且只能在当前页真实可见时绑定。不要预列未来子页名称；由 Kernel 根据新观察决定进入、返回与进度。当planExecution.traversal为Completed、两个指定入口各有已Verified的进入和返回且当前仍在根页时，立即调用submit_decision提交NoAction；本任务已有World证据满足目标，无需completion折抵，结束载荷优先只保留kind、当前decisionId、proposal.decisionId和80字以内的简短justification，无需completion/checklist或重复计划；如有completion仍只能引用本次上下文Verified且有receipt的plan-attempt:ID。若尚未满足这些事实，继续按原未知/恢复规则处理。不得改变原Goal、设备/权限范围或把局部结果表述为Settings全部发现。";

    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };
    private static readonly string[] RootTargets = ["Network & internet", "Connected devices"];

    [Fact]
    public async Task FrozenBoundedScope_UsesRealAgentAndRetainsTraversalEvidence()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_PLAN005_LIVE") != "1")
        {
            output.WriteLine("真实 Settings Traversal 验收未启用。");
            return;
        }

        var root = FindRoot();
        var registrationPath = Environment.GetEnvironmentVariable("UNICLAW_PLAN005_REGISTRATION");
        Assert.False(string.IsNullOrWhiteSpace(registrationPath),
            "REGISTRATION_REQUIRED: set UNICLAW_PLAN005_REGISTRATION to the frozen preregistration JSON.");
        Assert.True(File.Exists(registrationPath), "REGISTRATION_REQUIRED: preregistration file does not exist.");
        using var registration = ReadFrozenRegistration(root, registrationPath!);
        var registrationRoot = registration.RootElement;
        var epoch = RequiredString(registrationRoot, "epoch");
        Assert.Equal(Path.GetFileName(epoch), epoch);
        var repetitions = RequiredInt(registrationRoot, "repetitions");
        Assert.True(repetitions > 0);
        var repeat = ParseRepeat(repetitions);
        AssertRegisteredObjective(registrationRoot);
        Assert.Equal(TaskInstruction, RequiredString(registrationRoot, "taskInstruction"));

        var serial = Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE");
        Assert.False(string.IsNullOrWhiteSpace(serial),
            "ENVIRONMENT_UNAVAILABLE: UNICLAW_ANDROID_DEVICE must identify the registered device lease serial.");
        var leaseDirectory = Environment.GetEnvironmentVariable("UNICLAW_ANDROID_RUN_DIR");
        Assert.False(string.IsNullOrWhiteSpace(leaseDirectory), "registered device lease directory is required");
        Assert.True(File.Exists(Path.Combine(leaseDirectory!, ".state")), "registered device lease state is required");
        Assert.Contains($"SERIAL={serial}", File.ReadAllLines(Path.Combine(leaseDirectory!, ".state")));
        AssertRegisteredDevice(serial!);

        var bindings = UniagentDshBindingsYaml.LoadDefault();
        Assert.Equal(3081, bindings.Service.BaseUri.Port);
        var profile = UniAgentProfileYaml.LoadDefault();
        Assert.Equal(26, profile.Revision);
        Assert.Single(profile.AllowedSkillRefs!, skill => skill.Name == "android-automotive-ui-testing" && skill.Revision == 4);
        using (var prompt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "product/prompt/uniagent-prod/manifest.json"))))
        {
            Assert.Equal(25, prompt.RootElement.GetProperty("promptRevision").GetInt32());
        }
        var model = DshModelManagement.ResolveDshModel(
            DshModelManagement.FromBindings(bindings, profile), LogicalProfileId.AgentDecision);
        AssertRegisteredModel(registrationRoot, model);

        var original = SettingsCoverageConfig.LoadDefault();
        var config = original with
        {
            Session = original.Session with { TaskTitle = "RUNTIME-PLAN-005 registered bounded Settings traversal" },
            Bounds = original.Bounds with
            {
                MaxSteps = 12,
                MaxConsultRounds = 12,
                MaxTotalSteps = 12,
                MaxTotalConsultRounds = 12,
                RenewalThresholdSteps = 0,
                RenewalThresholdRounds = 0,
                GoalDirectedContinuationEnabled = false,
                AuthorizedTotalSteps = 12,
                AuthorizedTotalConsultRounds = 12,
            },
            Coverage = new CoverageRequirements(true, "all-visible", 0, 0, true, 0),
            TargetPages = RootTargets,
            DynamicRootEntries = new DynamicRootEntriesConfig(),
            DynamicNestedEntries = new DynamicNestedEntriesConfig(),
            DeepTraversal = new DeepTraversalConfig(),
            Slow = new SlowTriggerConfig(),
            LanguageInspection = null,
        };
        Assert.Equal(Objective, ComposeObjective(config));
        Assert.Equal(RootTargets, config.TargetPages);
        Assert.False(config.DynamicRootEntriesSettings.AllVisible);
        Assert.False(config.DynamicNestedEntriesSettings.IsActive);
        AssertFrozenJson(registrationRoot, "input", new
        {
            deviceSerial = serial, leaseDirectory,
            rootTargets = config.TargetPages, maxDepth = 1, maxLocalOperations = 128,
            maxSteps = config.Bounds.MaxSteps, maxConsultations = config.Bounds.MaxConsultRounds,
            maxTotalSteps = config.Bounds.MaxTotalSteps, maxTotalConsultations = config.Bounds.MaxTotalConsultRounds,
            authorizedTotalSteps = config.Bounds.AuthorizedTotalSteps, authorizedTotalConsultations = config.Bounds.AuthorizedTotalConsultRounds,
            viewportWidth = 1080, viewportHeight = 1920,
            dynamicRoot = false, dynamicNested = false, languageInspection = false,
            rootRoute = config.RootRoute, backDescriptor = config.BackDescriptor,
        });
        AssertFrozenJson(registrationRoot, "expected", new
        {
            verifiedTaps = 4, consultations = 2, minimumChildReadOnlyOperations = 1,
            maximumPreAdoptionRepairsPerConsultation = 1,
            discoveryComplete = false, minimumDiscoveryGaps = 1, finalRoute = config.RootRoute,
            endReason = "partial", completionAnchorPolicy = "omit-or-current-verified-plan-attempts",
            switchEffects = 0,
        });
        Assert.Equal(SettingsActionPolicy.RejectUnknown, config.ActionPolicy?.UnknownTargetDisposition);

        var liveRoot = Path.Combine(root, "evidence", "runtime-plan-005", "live", epoch);
        var repeatRoot = Path.Combine(liveRoot, repeat.ToString("D2"));
        Assert.False(Directory.Exists(repeatRoot),
            "EVIDENCE_EXISTS: refuse to overwrite this registered repeat; freeze a new epoch/repeat.");
        Directory.CreateDirectory(repeatRoot);
        File.Copy(registrationPath!, Path.Combine(repeatRoot, "preregistration.json"));

        var productSessionId = $"plan005-{epoch}-{repeat:D2}-{Guid.NewGuid():N}";
        string? productRunId = null;
        var sourceHashes = registrationRoot.GetProperty("sourceHashes").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
        var assemblyHashes = new[]
        {
            typeof(SettingsTraversalPlanLiveTests).Assembly,
            typeof(DshAgentAdapter).Assembly,
            typeof(UniAgentProfile).Assembly,
            typeof(KernelRunDriver).Assembly,
            typeof(SettingsCoverageRunner).Assembly,
        }.ToDictionary(assembly => assembly.GetName().Name!, assembly => Hash(assembly.Location), StringComparer.Ordinal);
        WriteJson(Path.Combine(repeatRoot, "artifact-identities.json"), new
        {
            registrationSha256 = Hash(registrationPath!), sourceHashes,
            profileRevision = profile.Revision,
            promptRevision = 25,
            skill = Assert.Single(profile.AllowedSkillRefs!),
            schemaHash = ProductProtocolSchema.Current.SchemaHash,
            model, productSessionId, assemblyHashes,
            settingsCoverageConfigSha256 = Hash(SettingsConfigPath(root)),
            actionPolicyDigest = config.ActionPolicy?.Digest,
            objective = Objective,
            taskInstruction = TaskInstruction,
            independentExpectedOutcome = new
            {
                verifiedTaps = 4,
                rootTargets = RootTargets,
                finalRoute = config.RootRoute,
                switchEffects = 0,
                discoveryComplete = false,
                minimumDiscoveryGaps = 1,
            },
        });

        using var traffic = new RecordingMessageHandler();
        await using var peer = new DshOpenedHttpPeer(bindings.Service, handler: traffic,
            model: model, requestTimeout: TimeSpan.FromSeconds(210), expectedProfile: profile);
        DshAgentAdapter? agent = null;
        var consultMetadata = new List<object>();
        var actualDecisions = new List<AgentDecision>();
        var actualContexts = new List<AgentDecisionContext>();
        var decisionRoundTrips = new List<string>();
        var objectives = new List<string>();
        var trajectorySnapshots = new List<PlanGenerationTrajectorySnapshot>();
        var trajectoryCount = 0;
        string? ownedDshSessionId = null;
        SettingsCoverageRunner.RunResult? result = null;
        Exception? failure = null;
        string? cleanupError = null;
        var timer = Stopwatch.StartNew();
        try
        {
            result = SettingsCoverageRunner.Run(repeatRoot, new SettingsCoverageRunner.Options(
                serial!, new LivePerception.LiveAssets(serial!, "wifi-settings",
                    Path.Combine(root, "platforms", "perception"),
                    Path.Combine(root, ".perception", "venv", "bin", "python"),
                    Path.Combine(root, ".perception", "cache")), config,
                context =>
                {
                    productRunId ??= context.RunId;
                    Assert.Equal(productRunId, context.RunId);
                    agent ??= new DshAgentAdapter(new DshOpenedDecisionChannel(peer), productSessionId, context.RunId,
                        turnTimeout: TimeSpan.FromSeconds(200));
                    WriteJson(Path.Combine(repeatRoot, "runtime-binding.json"), new { productSessionId, productRunId });
                    var index = objectives.Count + 1;
                    var consultRoot = Path.Combine(repeatRoot, "consults", index.ToString("D2"));
                    Directory.CreateDirectory(consultRoot);
                    Assert.Equal(Objective, context.Objective);
                    File.WriteAllText(Path.Combine(consultRoot, "base-goal.txt"), context.Objective);
                    File.WriteAllText(Path.Combine(consultRoot, "task-instruction.txt"), TaskInstruction);
                    var sentContext = context with { Objective = Objective + "\n" + TaskInstruction };
                    objectives.Add(sentContext.Objective);
                    File.WriteAllText(Path.Combine(consultRoot, "input-context.json"),
                        JsonSerializer.Serialize(sentContext, ProductProtocolJson.CreateOptions()));
                    AgentDecision? decision = null;
                    Exception? consultError = null;
                    var lineStart = traffic.Lines.Count;
                    var requestStart = traffic.ConsultRequestIds.Count;
                    var consultTimer = Stopwatch.StartNew();
                    try
                    {
                        // Keep the actual request objective intact except for the separately
                        // frozen task instruction. The trajectory capture below is awaited
                        // before the callback returns, so no next Kernel consultation starts
                        // until the collector has retained this request's completed turn.
                        decision = agent.Consult(sentContext);
                        ownedDshSessionId ??= agent?.DshSessionId;
                        var wire = decision is null ? null : PlanGenerationSemanticAssertions.EncodeRoundTrip(decision);
                        File.WriteAllText(Path.Combine(consultRoot, "decision-codec-roundtrip.json"), wire ?? "null");
                        if (decision is not null)
                        {
                            actualDecisions.Add(decision);
                            actualContexts.Add(sentContext);
                            decisionRoundTrips.Add(wire!);
                        }
                        consultMetadata.Add(new { index, sentContext.DecisionId, baseGoal = context.Objective,
                            sentObjective = sentContext.Objective, decisionIndex = decision is null ? (int?)null : actualDecisions.Count,
                            elapsedMilliseconds = consultTimer.ElapsedMilliseconds });
                    }
                    catch (Exception error)
                    {
                        consultError = error;
                        consultMetadata.Add(new { index, sentContext.DecisionId, baseGoal = context.Objective,
                            sentObjective = sentContext.Objective, error = error.ToString(),
                            elapsedMilliseconds = consultTimer.ElapsedMilliseconds });
                    }
                    finally
                    {
                        consultTimer.Stop();
                        File.WriteAllText(Path.Combine(consultRoot, "transport-raw.jsonl"),
                            string.Join(Environment.NewLine, traffic.Lines.Skip(lineStart)) + Environment.NewLine);
                        var requestId = traffic.ConsultRequestIds.Skip(requestStart).LastOrDefault();
                        if (agent.DshSessionId is { Length: > 0 } sessionId && requestId is { Length: > 0 })
                        {
                            var snapshot = PlanGenerationTrajectoryCollector.CaptureAsync(bindings.Service,
                                sessionId, requestId, Path.Combine(consultRoot, "trajectory"))
                                .GetAwaiter().GetResult();
                            WriteJson(Path.Combine(consultRoot, "trajectory-summary.json"), snapshot with
                            {
                                ConsultationRawJsonl = "see trajectory/dsh-consultation.raw.jsonl",
                            });
                            AssertMountedSources(root, snapshot);
                            Assert.True(snapshot.NoSubmitNudgeCount + Math.Max(0, snapshot.SubmitCalls.Count - 1) <= 1,
                                "at most one pre-adoption repair is allowed per consultation");
                            trajectorySnapshots.Add(snapshot);
                            trajectoryCount++;
                        }
                        else if (requestId is not null || agent.DshSessionId is not null)
                        {
                            throw new InvalidOperationException("TRAJECTORY_CORRELATION_MISSING: successful DSH request must have its session id and requestId.");
                        }
                        File.WriteAllText(Path.Combine(repeatRoot, "dsh-consult-request-response.raw.jsonl"),
                            string.Join(Environment.NewLine, traffic.Lines) + Environment.NewLine);
                    }
                    if (consultError is not null) throw new InvalidOperationException("DSH consultation failed; raw evidence retained.", consultError);
                    return decision;
                },
                ViewportWidth: 1080, ViewportHeight: 1920,
                DshSessionIdAccessor: () => agent?.DshSessionId,
                UseCoverageDirector: false));
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            timer.Stop();
            ownedDshSessionId ??= agent?.DshSessionId;
            if (ownedDshSessionId is not null)
            {
                try { await peer.DetachAsync(CancellationToken.None); }
                catch (Exception error) { cleanupError = "owned-session-detach: " + error; }
            }
            try { if (agent is not null) await agent.DisposeAsync(); }
            catch (Exception error) { cleanupError = (cleanupError ?? "") + " agent-dispose: " + error; }
            File.WriteAllText(Path.Combine(repeatRoot, "dsh-consult-request-response.raw.jsonl"),
                string.Join(Environment.NewLine, traffic.Lines) + Environment.NewLine);
            WriteJson(Path.Combine(repeatRoot, "trial.json"), new
            {
                repeat, epoch, ownedDshSessionId,
                elapsedMilliseconds = timer.ElapsedMilliseconds,
                consultations = agent?.SemanticConsultationsStarted ?? 0,
                capturedTrajectories = trajectoryCount,
                driveStatus = result?.Status.ToString(), result?.Reason,
                result?.RunDir, coverageStatus = result?.Report.Status,
                verifiedSteps = result?.Steps.Count(step => step.Verified),
                error = failure?.ToString(), cleanupError,
            });
            WriteJson(Path.Combine(repeatRoot, "actual-consults.json"), consultMetadata);
            WriteJson(Path.Combine(repeatRoot, "decision-codec-roundtrips.json"), decisionRoundTrips);
            WriteJson(Path.Combine(repeatRoot, "request-objectives.json"), objectives);
        }

        Assert.Null(cleanupError);
        Assert.NotNull(result);
        Assert.NotNull(agent);
        Assert.Equal(2, agent.SemanticConsultationsStarted);
        Assert.Equal(agent.SemanticConsultationsStarted, trajectoryCount);
        Assert.NotEmpty(actualDecisions);
        Assert.Equal(objectives.Count, actualDecisions.Count);
        Assert.All(objectives, objective => Assert.Equal(Objective + "\n" + TaskInstruction, objective));
        Assert.NotEmpty(trajectorySnapshots);
        var first = Assert.IsType<AgentDecision.Plan>(actualDecisions[0]);
        Assert.InRange(first.Proposal.MaxLocalOperations, 1, 128);
        var traversal = Assert.Single(Flatten(first.Proposal.Items).OfType<PlanItem.TraverseItem>());
        Assert.Equal(SettingsTraversalProjection.Source, traversal.Source);
        Assert.Equal(1, traversal.MaxDepth);
        Assert.Equal(2, traversal.Rules.Count);
        var navigationRule = Assert.Single(traversal.Rules.Where(rule => rule.Operation.Kind == TraversalOperationKind.Navigate));
        Assert.Equal("navigation", navigationRule.Condition);
        Assert.Contains(navigationRule.Operation.Step!.TargetRole, new[] { "$candidate", "ui.element" });
        Assert.Equal("$candidate", navigationRule.Operation.Step.TargetDescriptor);
        Assert.Equal("tap", navigationRule.Operation.Step.EffectClass);
        var readOnlyRule = Assert.Single(traversal.Rules.Where(rule => rule.Operation.Kind == TraversalOperationKind.ReadOnly));
        Assert.Equal("readOnly", readOnlyRule.Condition);
        Assert.Null(readOnlyRule.Operation.Step);
        Assert.Empty(readOnlyRule.Operation.Before);
        Assert.Empty(readOnlyRule.Operation.AfterVerified);
        Assert.Equal("ui.element", traversal.ReturnStep.TargetRole);
        Assert.Equal(config.BackDescriptor, traversal.ReturnStep.TargetDescriptor);
        Assert.Equal("tap", traversal.ReturnStep.EffectClass);
        Assert.IsType<AgentDecision.NoAction>(actualDecisions[^1]);
        for (var i = 0; i < actualDecisions.Count; i++)
        {
            if (actualDecisions[i] is not AgentDecision.NoAction { Proposal.Completion: { } completion }) continue;
            var verifiedAnchors = (actualContexts[i].PlanExecution?.Attempts ?? Array.Empty<PlanAttempt>())
                .Where(attempt => attempt.Status == PlanAttemptStatus.Verified && attempt.ReceiptId is not null)
                .Select(attempt => "plan-attempt:" + attempt.AttemptId).ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(completion.Checklist);
            Assert.All(completion.Checklist, anchor => Assert.Contains(anchor, verifiedAnchors));
        }

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(4, result.Steps.Count);
        Assert.All(result.Steps, step =>
        {
            Assert.True(step.Verified);
            Assert.Equal("tap", step.EffectClass);
            Assert.Contains(step.TargetDescriptor, RootTargets.Append(config.BackDescriptor));
        });
        Assert.Equal(config.RootRoute, result.Steps[^1].RouteAfter);
        var enteredTargets = result.Steps.Where(step => step.RouteBefore == config.RootRoute
            && step.RouteAfter != config.RootRoute).Select(step => step.TargetDescriptor).ToArray();
        Assert.Equal(RootTargets.Order(StringComparer.Ordinal), enteredTargets.Order(StringComparer.Ordinal));
        Assert.Equal(2, result.Steps.Count(step => step.Verified && step.RouteBefore != config.RootRoute
            && step.RouteAfter == config.RootRoute && step.TargetDescriptor == config.BackDescriptor));
        Assert.DoesNotContain(result.Steps, step => step.TargetDescriptor is { } descriptor
            && config.ActionPolicy?.DeclaredTargetSwitchLabels.Contains(descriptor, StringComparer.OrdinalIgnoreCase) == true);

        var traversalPath = Path.Combine(result.RunDir, "traversal-execution.json");
        Assert.True(File.Exists(traversalPath), "Kernel traversal-execution evidence is required.");
        using var traversalDocument = JsonDocument.Parse(File.ReadAllText(traversalPath));
        var traversalJson = traversalDocument.RootElement;
        Assert.Equal((int)PlanExecutionStatus.Completed, traversalJson.GetProperty("status").GetInt32());
        Assert.False(traversalJson.GetProperty("discoveryComplete").GetBoolean());
        Assert.Equal("partial", traversalJson.GetProperty("endReason").GetString());
        Assert.InRange(traversalJson.GetProperty("workUsed").GetInt32(), 0, 128);
        Assert.NotEmpty(traversalJson.GetProperty("gaps").EnumerateArray());
        var operationRecords = traversalJson.GetProperty("operations").EnumerateArray().ToArray();
        var rootEntryRecord = operationRecords.FirstOrDefault(operation =>
            operation.GetProperty("completed").GetBoolean()
            && RootTargets.Any(target => operation.GetProperty("targetKey").GetString()?.Contains(target, StringComparison.Ordinal) == true));
        Assert.NotEqual(JsonValueKind.Undefined, rootEntryRecord.ValueKind);
        var readOnlyOperations = operationRecords.Where(operation =>
            operation.GetProperty("completed").GetBoolean()
            && operation.GetProperty("templateId").GetString() == readOnlyRule.Operation.OperationId
            // Unknown/null state is legitimate; this checks the recorded World
            // association, not the independent semantic quality of the acquirer.
            && operation.GetProperty("containerId").GetString() != rootEntryRecord.GetProperty("containerId").GetString()
            && operation.GetProperty("sourceRevisionId").GetString() != rootEntryRecord.GetProperty("sourceRevisionId").GetString()).ToArray();
        Assert.NotEmpty(readOnlyOperations);
        Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-steps.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-report.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDir, "settings-trace.json")));
        var feedEvidence = Path.Combine(result.RunDir, "evidence");
        Assert.True(Directory.Exists(feedEvidence));
        Assert.NotEmpty(Directory.EnumerateFiles(feedEvidence, "*.png", SearchOption.AllDirectories));
        Assert.NotEmpty(Directory.EnumerateFiles(feedEvidence, "*.xml", SearchOption.AllDirectories));
        Assert.DoesNotContain("all Settings complete", File.ReadAllText(Path.Combine(result.RunDir, "coverage-report.json")), StringComparison.OrdinalIgnoreCase);
        output.WriteLine($"epoch={epoch}; repeat={repeat}; run={result.RunDir}; traversal=Completed/discovery-incomplete; verifiedTaps=4");
    }

    private static void AssertFrozenJson(JsonElement registration, string name, object expected)
    {
        var canonical = JsonSerializer.SerializeToElement(expected);
        Assert.True(JsonElement.DeepEquals(canonical, registration.GetProperty(name)),
            $"REGISTRATION_DRIFT: frozen {name} differs from the fixed input/independent expectation.");
    }

    private static JsonDocument ReadFrozenRegistration(string root, string path)
    {
        var document = JsonDocument.Parse(File.ReadAllText(path));
        try
        {
            var value = document.RootElement;
            Assert.Equal("frozen-ready", RequiredString(value, "status"));
            var hashes = value.GetProperty("sourceHashes");
            Assert.Equal(JsonValueKind.Object, hashes.ValueKind);
            Assert.NotEmpty(hashes.EnumerateObject());
            foreach (var source in hashes.EnumerateObject())
            {
                Assert.False(Path.IsPathRooted(source.Name), $"sourceHashes path must be repository-relative: {source.Name}");
                var fullPath = Path.GetFullPath(Path.Combine(root, source.Name));
                Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, fullPath, StringComparison.Ordinal);
                Assert.True(File.Exists(fullPath), $"registered source is missing: {source.Name}");
                Assert.Equal(source.Value.GetString(), Hash(fullPath));
            }
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void AssertRegisteredObjective(JsonElement registration)
    {
        var goal = registration.TryGetProperty("objective", out var objective)
            ? objective.GetString()
            : registration.TryGetProperty("goal", out var goalValue) ? goalValue.GetString() : null;
        Assert.Equal(Objective, goal);
    }

    private static void AssertRegisteredModel(JsonElement registration, ModelConfiguration selected)
    {
        if (!registration.TryGetProperty("model", out var model)) return;
        Assert.Equal(selected.Provider, model.GetProperty("provider").GetString());
        var nameProperty = model.TryGetProperty("name", out var name) ? name : model.GetProperty("model");
        Assert.Equal(selected.Name, nameProperty.GetString());
    }

    private static int ParseRepeat(int repetitions)
    {
        var raw = Environment.GetEnvironmentVariable("UNICLAW_PLAN005_REPEAT") ?? "1";
        Assert.True(int.TryParse(raw, out var repeat) && repeat >= 1 && repeat <= repetitions,
            $"REPEAT_INVALID: UNICLAW_PLAN005_REPEAT must be a 1-based value in 1..{repetitions}.");
        return repeat;
    }

    private static void AssertRegisteredDevice(string serial)
    {
        using var process = Process.Start(new ProcessStartInfo("adb")
        {
            ArgumentList = { "devices" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: adb could not start.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "ENVIRONMENT_UNAVAILABLE: adb devices failed: " + stderr);
        Assert.Contains(stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries), line =>
        {
            var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return columns.Length >= 2 && columns[0] == serial && columns[1] == "device";
        });
    }

    private static string ComposeObjective(SettingsCoverageConfig config) =>
        "Traverse Android Settings within the current device, account, permissions and system state. "
        + "Enter every configured first-level target: " + string.Join("; ", config.TargetPages) + ". "
        + $"Required evidence: rootPage={config.Coverage.RootPage}, "
        + $"scrollDiscoveredEntries={config.Coverage.ScrollDiscoveredEntries}, "
        + $"secondLevelPages={config.Coverage.SecondLevelPages}, "
        + $"backNavigation={config.Coverage.BackNavigation}, repeatedEntries={config.Coverage.RepeatedEntries}. "
        + "End on the observed Settings root. Select every action from the current semantic hierarchy. "
        + (config.DynamicRootEntriesSettings.AllVisible
            ? "Discover and inspect the safe dynamic root frontier; keep unobserved branches unknown. " : "")
        + (config.DynamicNestedEntriesSettings.IsActive
            ? "Inspect the configured dynamic nested frontier, keeping depth and scope limits explicit. " : "")
        + "A configured scope result never proves all possible UI states exhaustive.";

    private static void AssertMountedSources(string root, PlanGenerationTrajectorySnapshot snapshot)
    {
        var identity = File.ReadAllText(Path.Combine(root, "product/prompt/uniagent-prod/01-identity.txt")).TrimEnd();
        var guidance = File.ReadAllText(Path.Combine(root, "product/skills/android-automotive-ui-testing/guidance.md")).TrimEnd();
        Assert.NotNull(snapshot.SystemPrompt);
        Assert.Contains(identity, snapshot.SystemPrompt!, StringComparison.Ordinal);
        Assert.Contains(guidance, snapshot.SystemPrompt!, StringComparison.Ordinal);
        var expectedSections = new[]
        {
            "uniagent-prod:product-prompt",
            "uniagent-prod:skill:android-automotive-ui-testing",
        };
        Assert.Equal(expectedSections.Order(StringComparer.Ordinal), snapshot.Sections.Order(StringComparer.Ordinal));
    }

    private static IEnumerable<PlanItem> Flatten(IEnumerable<PlanItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            var children = item switch
            {
                PlanItem.ForEachItem loop => loop.Body,
                PlanItem.RepeatItem repeat => repeat.Body,
                PlanItem.IfItem branch => branch.Then.Concat(branch.Else).ToArray(),
                _ => Array.Empty<PlanItem>(),
            };
            foreach (var child in Flatten(children)) yield return child;
        }
    }

    private static string SettingsConfigPath(string root) => Path.Combine(root, "product/tasks/settings-coverage.yaml");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string RequiredString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : throw new InvalidDataException($"Registration field '{name}' is required.");
    private static int RequiredInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException($"Registration field '{name}' must be an integer.");
    private static void WriteJson(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, EvidenceJson));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("repository root unavailable");
    }

    private sealed class RecordingMessageHandler : HttpMessageHandler
    {
        private readonly HttpClientHandler _inner = new();
        public List<string> Lines { get; } = [];
        public List<string> ConsultRequestIds { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Lines.Add(JsonSerializer.Serialize(new
            {
                direction = "request", uri = request.RequestUri?.ToString(), body = requestBody,
            }));
            if (request.RequestUri?.AbsolutePath == "/api/uniclaw-agent/consult")
            {
                using var json = JsonDocument.Parse(requestBody);
                ConsultRequestIds.Add(json.RootElement.GetProperty("requestId").GetString()!);
            }
            using var client = new HttpMessageInvoker(_inner, disposeHandler: false);
            using var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
            };
            foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await client.SendAsync(clone, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Lines.Add(JsonSerializer.Serialize(new
            {
                direction = "response", status = (int)response.StatusCode, body = responseBody,
            }));
            response.Content = new StringContent(responseBody, Encoding.UTF8,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
