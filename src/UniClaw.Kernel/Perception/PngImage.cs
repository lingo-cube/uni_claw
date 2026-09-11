using System.IO.Compression;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// 最小 PNG 解码（PER-005 D9 零依赖路线）。只解码 adb screencap 产出的
/// 8-bit 非隔行 RGB(2)/RGBA(6) PNG → 原始 RGBA 字节 + 尺寸，供感知服务
/// <c>/v1/analyze_raw</c> 传输（X-Image-Width/Height 头 + width×height×4
/// body）。相较 legacy 的 SkiaSharp JPEG q92 路线：PNG 无损解码无编码器
/// 版本漂移，Kernel 保持零 NuGet 依赖（GREENFIELD 隔离），且确定性锚本就
/// 在服务响应 JSON（PER-005 D9/D10），传输格式不承担 replay 语义。
/// fail-closed：不支持的颜色型 / 隔行 / 截断输入一律抛
/// InvalidOperationException，绝不猜像素。
/// </summary>
public sealed record PngImage(int Width, int Height, byte[] Rgba)
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static PngImage Decode(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        return Decode(png.AsSpan());
    }

    public static PngImage Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature))
            throw NewMalformed("PNG signature 不匹配");

        // IHDR 必为首块
        if (png.Length < 8 + 25
            || ReadChunkLength(png, 8) != 13
            || !png.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw NewMalformed("IHDR 缺失或非首块");

        var width = BinaryPrimitivesReadUInt32BE(png, 16);
        var height = BinaryPrimitivesReadUInt32BE(png, 20);
        var bitDepth = png[24];
        var colorType = png[25];
        var compression = png[26];
        var filterMethod = png[27];
        var interlace = png[28];
        if (width == 0 || height == 0)
            throw NewMalformed("尺寸为 0");
        if (bitDepth != 8)
            throw NewMalformed($"不支持的 bit depth {bitDepth}（仅 8）");
        if (colorType is not (2 or 6))
            throw NewMalformed($"不支持的颜色型 {colorType}（仅 RGB(2)/RGBA(6)，adb screencap 产出域）");
        if (compression != 0 || filterMethod != 0)
            throw NewMalformed("不支持的压缩/过滤方法");
        if (interlace != 0)
            throw NewMalformed("不支持隔行（Adam7）");

        var bytesPerPixel = colorType == 6 ? 4 : 3;
        var stride = checked((int)width) * bytesPerPixel;

        // 遍历块：收集 IDAT；CRC 不校验（内容寻址 artifact 已提供完整性语义）
        var idat = new MemoryStream();
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = ReadChunkLength(png, offset);
            var type = png.Slice(offset + 4, 4);
            var dataStart = offset + 8;
            if (dataStart + length + 4 > png.Length)
                throw NewMalformed("块长度越过文件尾（截断输入）");
            if (type.SequenceEqual("IDAT"u8))
                idat.Write(png.Slice(dataStart, (int)length));
            else if (type.SequenceEqual("IEND"u8))
                break;
            offset = dataStart + (int)length + 4;
        }
        if (idat.Length == 0)
            throw NewMalformed("无 IDAT 数据");

        byte[] inflated;
        try
        {
            idat.Position = 0; // 写完后位置在末尾——回绕到数据起点
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            inflated = output.ToArray();
        }
        catch (InvalidDataException exception)
        {
            throw NewMalformed("zlib 解压失败", exception);
        }

        var expectedSize = (stride + 1) * checked((int)height);
        if (inflated.Length < expectedSize)
            throw NewMalformed($"扫描线数据不足（{inflated.Length} < {expectedSize}）");

        // 逐行去过滤（None/Sub/Up/Average/Paeth）→ RGBA
        var rgba = new byte[checked((int)width) * checked((int)height) * 4];
        var previous = new byte[stride];
        var current = new byte[stride];
        var source = 0;
        var target = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = inflated[source++];
            inflated.AsSpan(source, stride).CopyTo(current);
            source += stride;
            UnfilterRow(filter, current, previous, bytesPerPixel);
            for (var x = 0; x < width; x++)
            {
                var sx = x * bytesPerPixel;
                rgba[target++] = current[sx];
                rgba[target++] = current[sx + 1];
                rgba[target++] = current[sx + 2];
                rgba[target++] = bytesPerPixel == 4 ? current[sx + 3] : byte.MaxValue;
            }
            current.AsSpan().CopyTo(previous);
            current.AsSpan().Clear();
        }
        return new PngImage(checked((int)width), checked((int)height), rgba);
    }

    private static void UnfilterRow(byte filter, byte[] current, byte[] previous, int bytesPerPixel)
    {
        switch (filter)
        {
            case 0: // None
                return;
            case 1: // Sub
                for (var i = bytesPerPixel; i < current.Length; i++)
                    current[i] += current[i - bytesPerPixel];
                return;
            case 2: // Up
                for (var i = 0; i < current.Length; i++)
                    current[i] += previous[i];
                return;
            case 3: // Average
                for (var i = 0; i < current.Length; i++)
                {
                    var left = i - bytesPerPixel < 0 ? (byte)0 : current[i - bytesPerPixel];
                    current[i] += (byte)((left + previous[i]) / 2);
                }
                return;
            case 4: // Paeth
                for (var i = 0; i < current.Length; i++)
                {
                    var left = i - bytesPerPixel < 0 ? (byte)0 : current[i - bytesPerPixel];
                    current[i] += PaethPredictor(left, previous[i],
                        i - bytesPerPixel < 0 ? (byte)0 : previous[i - bytesPerPixel]);
                }
                return;
            default:
                throw NewMalformed($"未知扫描线过滤器 {filter}");
        }
    }

    private static byte PaethPredictor(byte left, byte up, byte upperLeft)
    {
        var p = left + up - upperLeft;
        var pa = Math.Abs(p - left);
        var pb = Math.Abs(p - up);
        var pc = Math.Abs(p - upperLeft);
        if (pa <= pb && pa <= pc)
            return left;
        if (pb <= pc)
            return up;
        return upperLeft;
    }

    private static uint ReadChunkLength(ReadOnlySpan<byte> png, int offset) =>
        (uint)((png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3]);

    private static uint BinaryPrimitivesReadUInt32BE(ReadOnlySpan<byte> png, int offset) =>
        (uint)((png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3]);

    private static InvalidOperationException NewMalformed(string reason, Exception? inner = null) =>
        new($"PNG 解码失败（fail-closed）：{reason}", inner);
}
