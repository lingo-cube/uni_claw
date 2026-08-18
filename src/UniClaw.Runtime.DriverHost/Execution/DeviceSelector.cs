namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Explicit device selector for run.start (dsh-runtime-agent-subagent-run-entry).
/// First slice supports the current Android path only.
///
/// Supported form: <c>serial:&lt;device-serial&gt;</c> (Kind = <see cref="SerialKind"/>).
/// Any other non-empty string is treated as an explicit configured alias
/// (Kind = <see cref="AliasKind"/>) and resolved by the composition root.
/// Unknown/unsupported selectors are REQUEST_REJECTED — never a silent fallback
/// to a first-connected/default device.
/// </summary>
public sealed record DeviceSelector(string Kind, string Value)
{
    /// <summary>Serial-kind selector: <c>serial:&lt;device-serial&gt;</c>.</summary>
    public const string SerialKind = "serial";

    /// <summary>Configured-alias selector (explicit composition-root mapping).</summary>
    public const string AliasKind = "alias";

    /// <summary>Deterministic reservation key (<c>kind:value</c>).</summary>
    public string Key => $"{Kind}:{Value}";

    /// <summary>Parse the explicit selector string. Empty/whitespace input is invalid.</summary>
    public static bool TryParse(string? raw, out DeviceSelector selector)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            selector = null!;
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith(SerialKind + ":", StringComparison.Ordinal))
        {
            var serial = text[(SerialKind.Length + 1)..].Trim();
            if (serial.Length == 0)
            {
                selector = null!;
                return false;
            }

            selector = new DeviceSelector(SerialKind, serial);
            return true;
        }

        selector = new DeviceSelector(AliasKind, text);
        return true;
    }
}
