namespace UniClaw.Runtime.Capabilities.Perception.Vision;

/// <summary>Optional external visual semantic binding for frame-scoped controls.</summary>
public interface IVisualControlStateReaderFactory
{
    bool CanRead(string? providerType);
    ISwitchStateReader Create(ReadOnlyMemory<byte> encodedFrame, int width, int height);
}
