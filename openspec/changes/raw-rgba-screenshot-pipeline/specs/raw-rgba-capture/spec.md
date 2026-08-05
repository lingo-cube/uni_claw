## ADDED Requirements

### Requirement: ADB raw screencap without -p flag

`IAdbSession` SHALL expose `CaptureRawScreenBufferAsync(CancellationToken ct)` returning `RawScreenBuffer`. The implementation SHALL execute `adb exec-out screencap` (WITHOUT `-p`) with `CaptureBinaryOutput: true`, parse the 12-byte Android framebuffer header:

- Bytes 0-3: `width` (uint32, little-endian)
- Bytes 4-7: `height` (uint32, little-endian)
- Bytes 8-11: `pixel_format` (uint32, little-endian)

If `pixel_format != 1` (RGBA_8888), the method SHALL throw `AdbCommandException`. If the binary output is shorter than 12 bytes, the method SHALL throw `AdbCommandException`. On success, it SHALL return `RawScreenBuffer` with `Pixels = bytes[12..]`, `Width`, `Height`, and `PixelFormat`.

#### Scenario: Raw screencap returns valid RGBA buffer

- **WHEN** `CaptureRawScreenBufferAsync` is called on an Android device
- **THEN** the returned `RawScreenBuffer.PixelFormat` is 1 (RGBA_8888), `Width` and `Height` match the device resolution, and `Pixels.Length == Width * Height * 4`

#### Scenario: Unsupported pixel format throws

- **WHEN** `adb exec-out screencap` returns `pixel_format != 1`
- **THEN** `AdbCommandException` is thrown with message containing "Unsupported pixel format"

#### Scenario: Header too short throws

- **WHEN** binary output is fewer than 12 bytes
- **THEN** `AdbCommandException` is thrown with message containing "header too short"

### Requirement: IScreenCapture exposes raw capture method

`IScreenCapture` SHALL expose `CaptureRawAsync(CancellationToken ct)` returning `Task<RawScreenBuffer>`. `AdbScreenCapture` SHALL delegate to `IAdbSession.CaptureRawScreenBufferAsync`. Non-ADB implementations MAY fallback to `CaptureAsync()` + decode to RGBA.

#### Scenario: AdbScreenCapture delegates to IAdbSession

- **WHEN** `AdbScreenCapture.CaptureRawAsync` is called
- **THEN** it calls `_session.CaptureRawScreenBufferAsync` and returns the result

### Requirement: C# side performs zero pixel operations on raw buffer

The C# raw path SHALL NOT construct `SKBitmap`, call `SetPixels`, `Decode`, `Encode`, `crop`, `resize`, or any pixel-level operation on the `RawScreenBuffer.Pixels` content. The only permitted transformation is the 12-byte header parse. The raw bytes SHALL be forwarded as-is to the Python vision service. The ONLY exception is `RunAssetHook` encoding raw RGBA to PNG at the storage boundary.

#### Scenario: Raw bytes forwarded without pixel manipulation

- **WHEN** `PageAnalyzer` takes the raw path
- **THEN** the `RawScreenBuffer.Pixels` passed to `LocalVisionProvider.CompleteVisionRawAsync` are byte-identical to the `screencap` output bytes[12..] (no crop, no resize, no color conversion)

### Requirement: RunAssetHook encodes raw RGBA to PNG at storage boundary

When the raw path is active, `RunAssetHook` SHALL encode `RawScreenBuffer.Pixels` to PNG via SkiaSharp (`SKBitmap` with `SKColorType.Rgba8888` + `SetPixels` + `Encode(SKEncodedImageFormat.Png, 100)`) before submitting to `TracePipeline`. The file name SHALL remain `before.png` / `after.png`.

#### Scenario: Raw screenshot saved as valid PNG

- **WHEN** raw path `RunAssetHook` submits a before screenshot
- **THEN** the stored `before.png` is a valid PNG decodable by PIL, with dimensions matching `RawScreenBuffer.Width × RawScreenBuffer.Height`
