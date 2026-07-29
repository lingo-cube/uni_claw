using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Scenarios;

public sealed class ScenarioCatalogTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-scenarios-{Guid.NewGuid():N}");

    [Fact]
    public void LoadSnapshot_ValidContract_NormalizesAndHashesInputs()
    {
        var scenarioPath = WriteValidCatalog();

        var snapshot = new ScenarioCatalog().LoadSnapshot(scenarioPath);

        Assert.Equal("locate-one-item", snapshot.Scenario.ScenarioId);
        Assert.Equal("locate_one_item", snapshot.Scenario.Mode);
        Assert.Equal("settings-read-only-v1", snapshot.Policy.PolicyId);
        Assert.Equal(64, snapshot.ScenarioHash.Length);
        Assert.Equal(64, snapshot.PolicyHash.Length);
        Assert.Contains("\"schemaVersion\":\"1\"", snapshot.NormalizedScenarioJson);
        Assert.DoesNotContain("apiKey", snapshot.NormalizedScenarioJson);
        Assert.DoesNotContain("authorization", snapshot.NormalizedPolicyJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadSnapshot_MissingRequiredField_FailsFast()
    {
        var scenarioPath = WriteValidCatalog(
            scenarioJson: ValidScenarioJson.Replace(
                "\"boundaries\":",
                "\"missingBoundaries\":",
                StringComparison.Ordinal));

        var exception = Assert.Throws<ScenarioValidationException>(
            () => new ScenarioCatalog().LoadSnapshot(scenarioPath));

        Assert.Contains("missingBoundaries", exception.Message);
    }

    [Fact]
    public void LoadSnapshot_InvalidVocabulary_FailsBeforeDeviceAccess()
    {
        var scenarioPath = WriteValidCatalog(
            scenarioJson: ValidScenarioJson.Replace(
                "\"locate_one_item\"",
                "\"recursive_everything\"",
                StringComparison.Ordinal));

        var exception = Assert.Throws<ScenarioValidationException>(
            () => new ScenarioCatalog().LoadSnapshot(scenarioPath));

        Assert.Equal("mode", exception.FieldName);
        Assert.Contains("recursive_everything", exception.Message);
    }

    [Fact]
    public void LoadSnapshot_NonPositiveBudget_FailsWithFieldAndValue()
    {
        var scenarioPath = WriteValidCatalog(
            scenarioJson: ValidScenarioJson.Replace(
                "\"maxSteps\": 12",
                "\"maxSteps\": 0",
                StringComparison.Ordinal));

        var exception = Assert.Throws<ScenarioValidationException>(
            () => new ScenarioCatalog().LoadSnapshot(scenarioPath));

        Assert.Equal("boundaries.maxSteps", exception.FieldName);
        Assert.Equal(0, exception.IllegalValue);
    }

    [Fact]
    public void LoadSnapshot_UnsupportedSchemaVersion_Fails()
    {
        var scenarioPath = WriteValidCatalog(
            scenarioJson: ValidScenarioJson.Replace(
                "\"schemaVersion\": \"1\"",
                "\"schemaVersion\": \"2\"",
                StringComparison.Ordinal));

        var exception = Assert.Throws<ScenarioValidationException>(
            () => new ScenarioCatalog().LoadSnapshot(scenarioPath));

        Assert.Equal("schemaVersion", exception.FieldName);
    }

    [Fact]
    public void LoadDirectory_DuplicateScenarioIds_Fails()
    {
        WriteValidCatalog();
        File.WriteAllText(
            Path.Combine(_root, "duplicate.v1.json"),
            ValidScenarioJson.Replace(
                "Locate About phone",
                "Duplicate scenario",
                StringComparison.Ordinal));

        var exception = Assert.Throws<ScenarioValidationException>(
            () => new ScenarioCatalog().LoadDirectory(_root));

        Assert.Equal("scenarioId", exception.FieldName);
        Assert.Contains("duplicate", exception.Message);
    }

    [Fact]
    public void Snapshot_DoesNotChangeWhenSourceFileMutates()
    {
        var scenarioPath = WriteValidCatalog();
        var catalog = new ScenarioCatalog();
        var snapshot = catalog.LoadSnapshot(scenarioPath);

        File.WriteAllText(
            scenarioPath,
            ValidScenarioJson.Replace(
                "About phone",
                "Battery",
                StringComparison.Ordinal));

        Assert.Equal("About phone", snapshot.Scenario.Target?.Label);
        Assert.Contains("About phone", snapshot.NormalizedScenarioJson);
        Assert.Equal(
            snapshot.ScenarioHash,
            ScenarioCatalog.ComputeHash(snapshot.NormalizedScenarioJson));
    }

    [Fact]
    public void PolicyHash_ChangesWhenVersionedPolicyInputChanges()
    {
        var scenarioPath = WriteValidCatalog();
        var catalog = new ScenarioCatalog();
        var first = catalog.LoadSnapshot(scenarioPath);

        File.WriteAllText(
            Path.Combine(_root, "policies", "settings-read-only.v1.json"),
            ValidPolicyJson.Replace(
                "\"minimumTarget\": 0.8",
                "\"minimumTarget\": 0.9",
                StringComparison.Ordinal));
        var second = catalog.LoadSnapshot(scenarioPath);

        Assert.NotEqual(first.PolicyHash, second.PolicyHash);
        Assert.Equal(first.ScenarioHash, second.ScenarioHash);
    }

    [Fact]
    public void UnknownCredentialField_IsRejectedAndNeverSerialized()
    {
        const string secret = "secret-value-must-not-persist";
        var scenarioPath = WriteValidCatalog(
            policyJson: ValidPolicyJson.Replace(
                "\"version\": \"1.0.0\",",
                $"\"version\": \"1.0.0\", \"apiKey\": \"{secret}\",",
                StringComparison.Ordinal));

        var exception = Assert.Throws<ScenarioValidationException>(
            () => new ScenarioCatalog().LoadSnapshot(scenarioPath));

        Assert.DoesNotContain(secret, exception.Message);
        Assert.Contains("apiKey", exception.Message);
    }

    [Fact]
    public async Task WriteScenarioAsync_UsesFrozenNormalizedSnapshot()
    {
        var scenarioPath = WriteValidCatalog();
        var snapshot = new ScenarioCatalog().LoadSnapshot(scenarioPath);
        var destination = Path.Combine(_root, "run", "scenario.snapshot.json");

        await snapshot.WriteScenarioAsync(destination);

        Assert.Equal(snapshot.NormalizedScenarioJson, await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public void VersionedRepositoryScenarios_LoadAsOneUniqueCatalog()
    {
        var scenarioDirectory = Path.Combine(AppContext.BaseDirectory, "Scenarios");

        var snapshots = new ScenarioCatalog().LoadDirectory(scenarioDirectory);

        Assert.Equal(2, snapshots.Length);
        Assert.Contains(
            snapshots,
            snapshot => snapshot.Scenario.ScenarioId == "locate-one-item"
                        && snapshot.Scenario.Target?.Label == "About phone");
        Assert.Contains(
            snapshots,
            snapshot => snapshot.Scenario.ScenarioId == "enumerate-settings-safely"
                        && snapshot.Scenario.SuccessCriteria.RequireEndOfList);
        Assert.Single(snapshots.Select(snapshot => snapshot.PolicyHash).Distinct());
    }

    private string WriteValidCatalog(
        string? scenarioJson = null,
        string? policyJson = null)
    {
        Directory.CreateDirectory(Path.Combine(_root, "policies"));
        var scenarioPath = Path.Combine(_root, "locate-one-item.v1.json");
        File.WriteAllText(scenarioPath, scenarioJson ?? ValidScenarioJson);
        File.WriteAllText(
            Path.Combine(_root, "policies", "settings-read-only.v1.json"),
            policyJson ?? ValidPolicyJson);
        return scenarioPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private const string ValidScenarioJson =
        """
        {
          "schemaVersion": "1",
          "scenarioId": "locate-one-item",
          "description": "Locate About phone",
          "appPackage": "com.android.settings",
          "entryStrategy": "cold_launch",
          "mode": "locate_one_item",
          "target": {
            "label": "About phone",
            "aliases": ["About device"]
          },
          "boundaries": {
            "allowedPages": ["Settings", "About phone"],
            "maxDepth": 1,
            "maxSteps": 12,
            "maxScrolls": 6,
            "maxDurationSeconds": 120
          },
          "allowedActions": ["click", "back", "scroll", "launch", "wait"],
          "safetyPolicy": {
            "policyId": "settings-read-only-v1",
            "path": "policies/settings-read-only.v1.json"
          },
          "successCriteria": {
            "kind": "target_page_identity",
            "expectedPageIdentities": ["About phone", "About device"],
            "requireEndOfList": false
          },
          "resetProcedure": {
            "actions": ["back", "launch", "wait"],
            "expectedPageIdentity": "Settings",
            "timeoutSeconds": 20
          }
        }
        """;

    private const string ValidPolicyJson =
        """
        {
          "schemaVersion": "1",
          "policyId": "settings-read-only-v1",
          "version": "1.0.0",
          "allowedActions": ["click", "back", "scroll", "launch", "wait"],
          "safeNavigationSemantics": ["navigation_row", "settings_home", "up_navigation"],
          "dangerousSemantics": ["toggle", "input", "long_press"],
          "dangerousText": ["reset", "erase", "delete", "remove account"],
          "aliases": [
            {
              "canonical": "about phone",
              "values": ["about device", "phone information"]
            }
          ],
          "confidenceThresholds": {
            "minimumTarget": 0.8,
            "minimumPageIdentity": 0.8
          },
          "boundaries": {
            "allowedPackages": ["com.android.settings"],
            "allowedPagePrefixes": ["Settings", "com.android.settings"],
            "maxDepth": 1
          }
        }
        """;
}
