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
        schemaVersion: "uniagent.profile/v2"
        profileId: uniagent-prod
        profileVersion: "1"
        profileRevision: 1
        identity:
          name: UniAgent
          description: test
        charter:
          role: Test role
          responsibilities:
            - Own the test objective
          boundaries:
            - Do not invent authority
          planningPrinciples:
            - Use current evidence
          skillUse:
            - Follow declared skills
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

    private static (string ProfilePath, string PromptDirectory) WriteProfileWithPromptFixture(
        string profileContent, string directoryName)
    {
        var repoRoot = RepoRoot();
        var promptDirectory = Path.Combine(repoRoot, "product/prompt", directoryName);
        Directory.CreateDirectory(promptDirectory);
        var sourceDirectory = Path.Combine(repoRoot, "product/prompt/uniagent-prod");
        var manifestPath = Path.Combine(sourceDirectory, "manifest.json");
        File.Copy(manifestPath, Path.Combine(promptDirectory, "manifest.json"));
        using var digest = new MemoryStream();
        digest.Write(File.ReadAllBytes(manifestPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        foreach (var segment in manifest.RootElement.GetProperty("segments").EnumerateArray())
        {
            var name = segment.GetString()!;
            var source = Path.Combine(sourceDirectory, name);
            File.Copy(source, Path.Combine(promptDirectory, name));
            digest.Write(File.ReadAllBytes(source));
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(digest.ToArray()))
            .ToLowerInvariant();
        File.WriteAllText(Path.Combine(promptDirectory, "prompt-hash.txt"), hash);
        var assembly = $"assembly:\n  promptManifest:\n    path: product/prompt/{directoryName}\n    hash: {hash}\n";
        var profilePath = WriteFixture(profileContent.Replace("profileRevision: 1", "profileRevision: 1\n" + assembly));
        return (profilePath, promptDirectory);
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
    public void Repo_Profile_Charter_Is_Rendered_As_IdentityPrompt()
    {
        var profile = UniAgentProfileYaml.LoadDefault();
        var identityPath = Path.Combine(RepoRoot(), "product/prompt/uniagent-prod/01-identity.txt");

        Assert.Equal(ProfileIdentityRenderer.Render(profile.Charter), File.ReadAllText(identityPath));
    }

    [Fact]
    public void Missing_Charter_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "charter:\n  role: Test role\n  responsibilities:\n    - Own the test objective\n"
            + "  boundaries:\n    - Do not invent authority\n  planningPrinciples:\n    - Use current evidence\n"
            + "  skillUse:\n    - Follow declared skills\n",
            ""));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("charter", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_Charter_Section_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "role: Test role", "role: \"\""));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("charter", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_Charter_List_FailsClosed()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "responsibilities:\n    - Own the test objective", "responsibilities: []"));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(path));
            Assert.Contains("charter.responsibilities", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CharterConstructor_CopiesInputArrays()
    {
        var responsibilities = new[] { "Keep the objective" };
        var boundaries = new[] { "Do not invent authority" };
        var principles = new[] { "Use current evidence" };
        var skillUse = new[] { "Use declared skills" };
        var charter = new ProfileCharter("Agent role", responsibilities, boundaries, principles, skillUse);

        responsibilities[0] = "Mutated after construction";

        Assert.Equal("Keep the objective", Assert.Single(charter.Responsibilities));
    }

    [Fact]
    public void LoadedCharterLists_AreNotBackedByMutableArrays()
    {
        var charter = UniAgentProfileYaml.LoadDefault().Charter;

        Assert.IsNotType<string[]>(charter.Responsibilities);
        Assert.Throws<NotSupportedException>(
            () => ((IList<string>)charter.Responsibilities)[0] = "Mutated at runtime");
        Assert.StartsWith("Advance the stated objective", charter.Responsibilities[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Loader_RejectsCharterDriftWithAValidPromptHash()
    {
        var fixture = WriteProfileWithPromptFixture(
            ValidProfile.Replace("role: Test role", "role: Changed test role"),
            "fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniAgentProfileYaml.Load(fixture.ProfilePath));
            Assert.True(error.Message.Contains("identity-charter-mismatch", StringComparison.Ordinal), error.Message);
        }
        finally
        {
            File.Delete(fixture.ProfilePath);
            Directory.Delete(fixture.PromptDirectory, recursive: true);
        }
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
    public void Legacy_V1_Profile_IsRejected()
    {
        var path = WriteFixture(ValidProfile.Replace(
            "uniagent.profile/v2", "uniagent.profile/v1"));
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

        // RUNTIME-PLAN-002：持续遍历/typed handlers提示词及生成Schema装配随动。
        Assert.Equal(26, profile.Revision);
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
        // RUNTIME-PLAN-004: 泛化计划生成指导 → revision 4。
        Assert.Equal(4, skill.Revision);
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
            "profileRevision: 1\nallowedSkillRefs:\n  android-automotive-ui-testing:\n    revision: 4\n    path: product/skills/android-automotive-ui-testing\n    sha256: " + new string('a', 64)));
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
