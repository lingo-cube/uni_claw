using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

[Collection("SettingsCoverageConfigSerial")]
public sealed class SettingsCoverageConfigTests
{
    private static string WriteConfig(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-coverage-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string MinimalConfig(string configVersion = "1") => $"""
        configVersion: "{configVersion}"
        session:
          taskTitle: 遍历设置菜单覆盖测试
          workspace: UniClaw_Product_Tasks
          workspaceReuse: true
          autoCloseTurn: false
        bounds:
          maxSteps: 24
          maxConsultRounds: 24
          maxScrolls: 4
          maxConsecutiveFailures: 3
          maxDirectiveRetries: 1
        coverage:
          rootPage: true
          firstLevelMode: all-visible
          scrollDiscoveredEntries: 1
          secondLevelPages: 2
          backNavigation: true
          repeatedEntries: 1
        targetPages:
          - Network & internet
          - Connected devices
        termination:
          onCoverageComplete: true
          onMaxSteps: true
          onMaxScrolls: true
          onConsecutiveFailures: true
        rootRoute: android.settings|route:Settings
        scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
        backDescriptor: Navigate up
        """;

    private static void AssertInvalid(string yaml, string expectedPrefix)
    {
        var path = WriteConfig(yaml);
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => SettingsCoverageConfig.Load(path));
            Assert.StartsWith("config-", error.Message);
            Assert.Contains(expectedPrefix, error.Message);
            Assert.Contains(path, error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EvidenceOptions_AbsentSection_DefaultsToPersistBoth()
    {
        var loaded = SettingsCoverageConfig.LoadDefault();
        Assert.True(loaded.EvidenceSettings.PersistScreenshots);
        Assert.True(loaded.EvidenceSettings.PersistHierarchies);
    }

    [Fact]
    public void EvidenceOptions_ExplicitFalse_Honored()
    {
        var path = WriteConfig("""
            configVersion: "1"
            session:
              taskTitle: evidence-off
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 24
              maxConsultRounds: 24
              maxScrolls: 4
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 1
              secondLevelPages: 2
              backNavigation: true
              repeatedEntries: 1
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            evidence:
              persistScreenshots: false
            rootRoute: android.settings|route:Settings
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """);
        var loaded = SettingsCoverageConfig.Load(path);
        Assert.False(loaded.EvidenceSettings.PersistScreenshots);
        Assert.True(loaded.EvidenceSettings.PersistHierarchies); // 未写 → 缺省 true
    }

    [Fact]
    public void EvidenceOptions_InvalidValue_FailsClosed()
    {
        var path = WriteConfig("""
            configVersion: "1"
            session:
              taskTitle: evidence-bad
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 24
              maxConsultRounds: 24
              maxScrolls: 4
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 1
              secondLevelPages: 2
              backNavigation: true
              repeatedEntries: 1
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            evidence:
              persistScreenshots: maybe
            rootRoute: android.settings|route:Settings
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """);
        var ex = Assert.Throws<InvalidOperationException>(() => SettingsCoverageConfig.Load(path));
        Assert.Contains("config-invalid:evidence.persistScreenshots", ex.Message);
    }

    [Fact]
    public void LoadDefault_EnvironmentVariableOverride_TakesPrecedence()
    {
        // AGT-006 回归：曾出现常量声明未读、env 配置被静默忽略的缺陷。
        var path = WriteConfig("""
            configVersion: "1"
            session:
              taskTitle: env-override-test
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 6
              maxConsultRounds: 24
              maxScrolls: 4
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 1
              secondLevelPages: 2
              backNavigation: true
              repeatedEntries: 1
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            rootRoute: android.settings|route:Settings
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """);
        var prior = Environment.GetEnvironmentVariable(SettingsCoverageConfig.ConfigPathEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(SettingsCoverageConfig.ConfigPathEnvironmentVariable, path);
            var loaded = SettingsCoverageConfig.LoadDefault();
            Assert.Equal(6, loaded.Bounds.MaxSteps);
            Assert.Equal("env-override-test", loaded.Session.TaskTitle);
            Assert.Equal(new[] { "Network & internet" }, loaded.TargetPages);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SettingsCoverageConfig.ConfigPathEnvironmentVariable, prior);
        }
    }

    [Fact]
    public void LoadDefault_ParsesDefaultProfileFile()
    {
        var config = SettingsCoverageConfig.LoadDefault();

        Assert.Equal("1", config.ConfigVersion);
        Assert.Equal("遍历设置菜单覆盖测试", config.Session.TaskTitle);
        Assert.Equal("UniClaw_Product_Tasks", config.Session.Workspace);
        Assert.True(config.Session.WorkspaceReuse);
        Assert.False(config.Session.AutoCloseTurn);
        Assert.Equal(new CoverageBounds(24, 24, 4, 3, 1), config.Bounds);
        Assert.Equal(new CoverageRequirements(true, "all-visible", 1, 2, true, 1), config.Coverage);
        Assert.Equal(7, config.TargetPages.Count);
        Assert.Contains("Network & internet", config.TargetPages);
        Assert.Equal("Sound & vibration", config.TargetPages[^1]);
        Assert.Equal(new CoverageTermination(true, true, true, true), config.Termination);
        Assert.Equal("android.settings|route:Settings", config.RootRoute);
        Assert.Equal("com.android.settings:id/main_content_scrollable_container",
            config.ScrollContainerDescriptor);
        Assert.Equal("Navigate up", config.BackDescriptor);
    }

    [Fact]
    public void Load_RoundTripsValidConfig()
    {
        var path = WriteConfig(MinimalConfig());
        try
        {
            var config = SettingsCoverageConfig.Load(path);

            Assert.Equal("1", config.ConfigVersion);
            Assert.Equal("遍历设置菜单覆盖测试", config.Session.TaskTitle);
            Assert.True(config.Session.WorkspaceReuse);
            Assert.False(config.Session.AutoCloseTurn);
            Assert.Equal(new CoverageBounds(24, 24, 4, 3, 1), config.Bounds);
            Assert.Equal(new CoverageRequirements(true, "all-visible", 1, 2, true, 1), config.Coverage);
            Assert.Equal(["Network & internet", "Connected devices"], config.TargetPages);
            Assert.Equal(new CoverageTermination(true, true, true, true), config.Termination);
            Assert.Equal("android.settings|route:Settings", config.RootRoute);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            SettingsCoverageConfig.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.yaml")));
    }

    [Fact]
    public void Load_UnsupportedConfigVersion_FailsClosed()
    {
        AssertInvalid(MinimalConfig(configVersion: "2"), "config-version-unsupported");
    }

    [Fact]
    public void Load_MissingSessionTaskTitle_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  taskTitle: 遍历设置菜单覆盖测试\n", "");
        AssertInvalid(yaml, "config-missing:session.taskTitle");
    }

    [Fact]
    public void Load_MissingSessionWorkspace_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  workspace: UniClaw_Product_Tasks\n", "");
        AssertInvalid(yaml, "config-missing:session.workspace");
    }

    [Fact]
    public void Load_InvalidBoundsMaxSteps_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  maxSteps: 24", "  maxSteps: 0");
        AssertInvalid(yaml, "config-invalid:bounds.maxSteps");
    }

    [Fact]
    public void Load_InvalidBoundsMaxConsultRounds_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  maxConsultRounds: 24", "  maxConsultRounds: 0");
        AssertInvalid(yaml, "config-invalid:bounds.maxConsultRounds");
    }

    [Fact]
    public void Load_InvalidBoundsMaxScrolls_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  maxScrolls: 4", "  maxScrolls: -1");
        AssertInvalid(yaml, "config-invalid:bounds.maxScrolls");
    }

    [Fact]
    public void Load_InvalidBoundsMaxConsecutiveFailures_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  maxConsecutiveFailures: 3", "  maxConsecutiveFailures: 0");
        AssertInvalid(yaml, "config-invalid:bounds.maxConsecutiveFailures");
    }

    [Fact]
    public void Load_InvalidBoundsMaxDirectiveRetries_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  maxDirectiveRetries: 1", "  maxDirectiveRetries: -1");
        AssertInvalid(yaml, "config-invalid:bounds.maxDirectiveRetries");
    }

    [Fact]
    public void Load_InvalidFirstLevelMode_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  firstLevelMode: all-visible", "  firstLevelMode: click-all");
        AssertInvalid(yaml, "config-invalid:coverage.firstLevelMode");
    }

    [Fact]
    public void Load_InvalidScrollDiscoveredEntries_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  scrollDiscoveredEntries: 1", "  scrollDiscoveredEntries: 0");
        AssertInvalid(yaml, "config-invalid:coverage.scrollDiscoveredEntries");
    }

    [Fact]
    public void Load_InvalidSecondLevelPages_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  secondLevelPages: 2", "  secondLevelPages: 0");
        AssertInvalid(yaml, "config-invalid:coverage.secondLevelPages");
    }

    [Fact]
    public void Load_InvalidRepeatedEntries_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  repeatedEntries: 1", "  repeatedEntries: -1");
        AssertInvalid(yaml, "config-invalid:coverage.repeatedEntries");
    }

    [Fact]
    public void Load_EmptyTargetPages_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  - Network & internet\n  - Connected devices\n", "");
        AssertInvalid(yaml, "config-invalid:targetPages");
    }

    [Fact]
    public void Load_DuplicateTargetPages_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("  - Connected devices\n", "  - Network & internet\n");
        AssertInvalid(yaml, "config-invalid:targetPages");
    }

    [Fact]
    public void Load_TerminationAllFalse_FailsClosed()
    {
        var yaml = MinimalConfig()
            .Replace("  onCoverageComplete: true", "  onCoverageComplete: false")
            .Replace("  onMaxSteps: true", "  onMaxSteps: false")
            .Replace("  onMaxScrolls: true", "  onMaxScrolls: false")
            .Replace("  onConsecutiveFailures: true", "  onConsecutiveFailures: false");
        AssertInvalid(yaml, "config-invalid:termination.none-enabled");
    }

    [Fact]
    public void Load_MissingRootRoute_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("rootRoute: android.settings|route:Settings\n", "");
        AssertInvalid(yaml, "config-missing:rootRoute");
    }

    [Fact]
    public void Load_MissingScrollContainerDescriptor_FailsClosed()
    {
        var yaml = MinimalConfig().Replace(
            "scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container\n", "");
        AssertInvalid(yaml, "config-missing:scrollContainerDescriptor");
    }

    [Fact]
    public void Load_MissingBackDescriptor_FailsClosed()
    {
        var yaml = MinimalConfig().Replace("backDescriptor: Navigate up", "");
        AssertInvalid(yaml, "config-missing:backDescriptor");
    }
}
