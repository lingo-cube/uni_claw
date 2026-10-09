using System.Text.Json;
using UniClaw.Agent.Profile;
using Xunit;

namespace UniClaw.Agent.Tests;

/// <summary>
/// PRF-002 — host-neutral 产品 UniAgent Profile loader 的 fail-closed 契约：
/// 身份漂移、schema 版本、模型角色值域与 required 字段全部拒载；合法文件
/// 加载出与 product/profiles/uniagent-prod.yaml 同形的三角色声明。
/// </summary>
public sealed class UniAgentProfileYamlTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private const string ValidProfile = """
        schemaVersion: "uniagent.profile/v1"
        profileId: uniagent-prod
        profileVersion: "1"
        profileRevision: 1
        identity:
          name: UniAgent
          description: test
        capabilities:
          - submit_decision
        modelRoles:
          agent.decision:
            required: true
          slow.semantic.text:
            required: true
          slow.semantic.visual:
            required: false
        """;

    private static string WriteFixture(string content)
    {
        var path = Path.Combine(Path.GetTempPath(),
            "uniagent-profile-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Repo_Profile_Loads_With_Declared_Roles()
    {
        var profile = UniAgentProfileYaml.LoadDefault();

        Assert.Equal(UniagentProdProfile.ProfileId, profile.Identity.ProfileId);
        Assert.Equal(
            new[]
            {
                new ModelRoleDeclaration("agent.decision", Required: true),
                new ModelRoleDeclaration("slow.semantic.text", Required: true),
                new ModelRoleDeclaration("slow.semantic.visual", Required: false),
            },
            profile.ModelRoles);
    }

    [Fact]
    public void Unknown_ModelRole_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "  slow.semantic.visual:\n    required: false",
            "  slow.semantic.visual:\n    required: false\n  bogus.role:\n    required: true"));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("bogus.role", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Malformed_Required_Field_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "  agent.decision:\n    required: true",
            "  agent.decision:\n    required: maybe"));
        try
        {
            Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SchemaVersion_Drift_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "uniagent.profile/v1", "uniagent.profile/v0"));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("schemaVersion", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Identity_Drift_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "profileVersion: \"1\"", "profileVersion: \"2\""));
        try
        {
            Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- PRF-005：assembly 引用执法 ----

    [Fact]
    public void Repo_Profile_Loads_With_Assembly_And_Revision()
    {
        var profile = UniAgentProfileYaml.LoadDefault();

        Assert.Equal(2, profile.Revision);
        Assert.NotNull(profile.Assembly);
        Assert.NotNull(profile.Assembly!.PromptManifest);
        Assert.Equal("product/prompt/uniagent-prod", profile.Assembly.PromptManifest!.Path);
        Assert.NotNull(profile.Assembly.SafetyPolicy);
        Assert.Equal("product/policy/android-settings-forbidden-actions.json", profile.Assembly.SafetyPolicy!.Path);
    }

    [Fact]
    public void Repo_Profile_Loads_Allowed_Domain_Skill()
    {
        var profile = UniAgentProfileYaml.LoadDefault();

        var skill = Assert.Single(profile.AllowedSkillRefs!);
        Assert.Equal("android-automotive-ui-testing", skill.Name);
        Assert.Equal(1, skill.Revision);
        Assert.Equal("product/skills/android-automotive-ui-testing", skill.Path);
        Assert.Equal(64, skill.Sha256.Length);
    }

    [Fact]
    public void Skill_Path_Traversal_Fails_Closed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "profileRevision: 1",
            "profileRevision: 1\nallowedSkillRefs:\n  android-automotive-ui-testing:\n    revision: 1\n    path: ../outside\n    sha256: " + new string('a', 64)));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("path", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Skill_Hash_Drift_Fails_Closed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "profileRevision: 1",
            "profileRevision: 1\nallowedSkillRefs:\n  android-automotive-ui-testing:\n    revision: 1\n    path: product/skills/android-automotive-ui-testing\n    sha256: " + new string('a', 64)));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("skill-hash-mismatch", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Duplicate_Profile_Keys_Fail_Closed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "profileRevision: 1",
            "profileRevision: 1\nprofileRevision: 2"));
        try
        {
            Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Assembly_With_Tampered_Prompt_Hash_Fails_Closed()
    {
        var path = WriteFixture(ValidProfile
            .Replace("profileRevision: 1",
                "profileRevision: 3\nassembly:\n  promptManifest:\n    path: product/prompt/uniagent-prod\n    hash: deadbeef".PadRight(64, '0')));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("assembly", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Assembly_With_Missing_Policy_File_Fails_Closed()
    {
        var path = WriteFixture(ValidProfile
            .Replace("profileRevision: 1",
                "profileRevision: 3\nassembly:\n  safetyPolicy:\n    path: product/policy/no-such-policy.json\n    hash: " + new string('a', 64)));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("assembly-not-found", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
