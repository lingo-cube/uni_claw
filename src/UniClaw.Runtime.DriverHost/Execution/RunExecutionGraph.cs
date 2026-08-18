using UniClaw.Runtime.Environment;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Composition-root product for one device selector: the fully wired
/// Runtime.Agent graph over the device's IEnvironment. The Agent receives only
/// its normal injected dependencies (Startup/Traversal/Recovery/criteria); it
/// has ZERO awareness of device transports, serials, device plugins, the outer
/// control host, or DriverHost (Guard 2 / authority boundary).
/// </summary>
public sealed record RunExecutionGraph(
    RuntimeAgent Agent,
    IEnvironment Environment);

/// <summary>
/// Explicit composition-root factory: DeviceSelector → RunExecutionGraph.
/// No reflection discovery, MEF, dynamic provider registry, or assembly
/// loading. Unknown/unsupported selectors throw
/// <see cref="DeviceSelectorUnsupportedException"/> (→ REQUEST_REJECTED).
/// </summary>
public delegate RunExecutionGraph RunGraphFactory(DeviceSelector selector);
