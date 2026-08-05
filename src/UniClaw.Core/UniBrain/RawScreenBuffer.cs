namespace UniClaw.Core.UniBrain;

/// <summary>
/// Raw screen buffer captured via adb exec-out screencap (no -p).
/// Carries parsed dimensions from the 12-byte Android framebuffer header
/// plus the raw RGBA pixel payload. C# side performs zero pixel operations —
/// crop/resize happen in Python where PIL is the natural image-processing layer.
/// </summary>
public readonly record struct RawScreenBuffer(
    byte[] Pixels,      // width * height * 4 bytes, RGBA_8888
    int Width,
    int Height,
    int PixelFormat     // 1 = RGBA_8888 (Android PIXEL_FORMAT_RGBA_8888)
);
