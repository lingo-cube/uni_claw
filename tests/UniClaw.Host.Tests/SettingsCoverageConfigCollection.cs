using Xunit;

namespace UniClaw.Host.Tests;

// These tests temporarily override the process-wide config-path environment
// variable. Keep them out of parallel execution so a default-profile assertion
// cannot observe another test's temporary profile.
[CollectionDefinition("SettingsCoverageConfigSerial", DisableParallelization = true)]
public sealed class SettingsCoverageConfigCollection
{
}
