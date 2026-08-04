namespace UniClaw.Device;

public sealed record class ShellResult(
    bool Success,
    string StandardOutput,
    string StandardError);
