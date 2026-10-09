using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
using UniClaw.Host.Dsh;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>
/// PRF-005（Q13）— 初始化审计 envelope：一次 Product Run 能完整重建当时
/// 装配（profileRevision / protocolSchemaHash / productPromptRevision /
/// safetyPolicyRevision / modelRoute）；每会话首个 run 落盘一次。
/// </summary>
public sealed class InitializationEnvelopeTests
{
    private static UniAgentProfile Profile() =>
        UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault();

    private static UniagentDshBindings Bindings() => new(
        new ModelConfiguration("deepseek-official", "deepseek-flash"),
        new DshServiceEndpoint(new Uri("http://127.0.0.1:3081/")),
        "deepseekFlash");

    [Fact]
    public void First_Run_Writes_Envelope_With_Full_Assembly_Fingerprint()
    {
        var runsRoot = Directory.CreateTempSubdirectory("prf005-envelope-").FullName;
        try
        {
            var written = InitializationEnvelope.Write(
                runsRoot, "session-A", "run-1", Profile(), Bindings(),
                new ModelConfiguration("deepseek-official", "deepseek-flash"));

            Assert.NotNull(written);
            using var document = JsonDocument.Parse(File.ReadAllText(written!));
            var root = document.RootElement;
            Assert.Equal("session-A", root.GetProperty("productSessionId").GetString());
            Assert.Equal("run-1", root.GetProperty("firstRunId").GetString());
            // 五要素齐备：profile/协议/工件修订 + Skill 快照 + 模型路由。
            Assert.Equal(2, root.GetProperty("runtimeProfileRevision").GetInt32());
            Assert.Equal(64, root.GetProperty("protocolSchemaHash").GetString()!.Length);
            Assert.Equal(2, root.GetProperty("productPromptRevision").GetInt32());
            Assert.Equal(1, root.GetProperty("safetyPolicyRevision").GetInt32());
            Assert.Equal("android-automotive-ui-testing", root.GetProperty("skill").GetProperty("name").GetString());
            Assert.Equal(1, root.GetProperty("skill").GetProperty("revision").GetInt32());
            Assert.Equal(64, root.GetProperty("skill").GetProperty("sha256").GetString()!.Length);
            Assert.Equal("deepseek-official", root.GetProperty("modelRoute").GetProperty("provider").GetString());
            Assert.Equal("deepseek-flash", root.GetProperty("modelRoute").GetProperty("model").GetString());
        }
        finally
        {
            Directory.Delete(runsRoot, recursive: true);
        }
    }

    [Fact]
    public void Later_Runs_Of_Same_Session_Keep_The_First_Envelope()
    {
        var runsRoot = Directory.CreateTempSubdirectory("prf005-envelope-2-").FullName;
        try
        {
            var profile = Profile();
            var bindings = Bindings();
            var model = new ModelConfiguration("deepseek-official", "deepseek-flash");

            Assert.NotNull(InitializationEnvelope.Write(runsRoot, "session-B", "run-1", profile, bindings, model));
            // 同会话第二个 run（即便换路由）不覆盖首 envelope——会话内装配钉扎。
            Assert.Null(InitializationEnvelope.Write(runsRoot, "session-B", "run-2", profile, bindings,
                new ModelConfiguration("zai-coding-cn", "glm-5.3-flash")));

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(runsRoot, "session-B", "initialization.json")));
            Assert.Equal("run-1", document.RootElement.GetProperty("firstRunId").GetString());
            Assert.Equal("android-automotive-ui-testing", document.RootElement.GetProperty("skill").GetProperty("name").GetString());
        }
        finally
        {
            Directory.Delete(runsRoot, recursive: true);
        }
    }

    [Fact]
    public void Multiple_Allowed_Skills_Fail_Closed_Without_A_Primary_Selection()
    {
        var runsRoot = Directory.CreateTempSubdirectory("prf009-envelope-ambiguous-").FullName;
        try
        {
            var profile = Profile();
            var skill = Assert.Single(profile.AllowedSkillRefs!);
            var ambiguous = profile with
            {
                AllowedSkillRefs = new[]
                {
                    skill,
                    skill with { Name = "another-domain-skill", Path = skill.Path },
                },
            };
            var error = Assert.Throws<InvalidOperationException>(() => InitializationEnvelope.Write(
                runsRoot, "session-ambiguous", "run-1", ambiguous, Bindings(),
                new ModelConfiguration("deepseek-official", "deepseek-flash")));
            Assert.Contains("multiple allowed Product Skills", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(runsRoot, recursive: true);
        }
    }
}
