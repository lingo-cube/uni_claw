using SkiaSharp;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Android.Visual;

/// <summary>Optional Android visual binding. It emits only qualitative evidence.</summary>
public sealed class AndroidVisualControlStateReaderFactory : IVisualControlStateReaderFactory
{
    public bool CanRead(string? providerType) => providerType is "toggle" or "switch" or "checkbox";

    public ISwitchStateReader Create(ReadOnlyMemory<byte> encodedFrame, int width, int height)
    {
        if (encodedFrame.IsEmpty || width <= 0 || height <= 0)
            throw new ArgumentException("A valid encoded frame is required.");
        using var data = SKData.CreateCopy(encodedFrame.Span);
        var bitmap = SKBitmap.Decode(data) ?? throw new InvalidOperationException("Frame decode failed.");
        return new ImageSwitchStateProvider(bitmap, width, height);
    }

    internal sealed class Reader(SKBitmap bitmap, int width, int height) : ISwitchStateReader
    {
        public PerceptionFrame Frame { get; } = new();

        public ValueTask<bool?> ReadAsync(ElementBounds bounds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!bounds.IsValid) return ValueTask.FromResult<bool?>(null);
            try
            {
                var rect = new SKRectI((int)(bounds.X1 * width), (int)(bounds.Y1 * height),
                    (int)(bounds.X2 * width), (int)(bounds.Y2 * height));
                if (rect.Width < 8 || rect.Height < 8) return ValueTask.FromResult<bool?>(null);
                using var crop = new SKBitmap(rect.Width, rect.Height);
                if (!bitmap.ExtractSubset(crop, rect)) return ValueTask.FromResult<bool?>(null);
                var top = crop.Height / 3; var bottom = 2 * crop.Height / 3; var mid = crop.Width / 2;
                var values = new List<int>();
                for (var y = top; y < bottom; y++) for (var x = 0; x < crop.Width; x++)
                { var p = crop.GetPixel(x, y); values.Add((p.Red + p.Green + p.Blue) / 3); }
                if (values.Count == 0) return ValueTask.FromResult<bool?>(null);
                values.Sort(); var median = values[values.Count / 2];
                var left = 0; var right = 0; var leftTotal = 0; var rightTotal = 0;
                for (var y = top; y < bottom; y++) for (var x = 0; x < crop.Width; x++)
                { var p = crop.GetPixel(x, y); var outlier = Math.Abs((p.Red + p.Green + p.Blue) / 3 - median) >= 60;
                  if (x < mid) { leftTotal++; if (outlier) left++; } else { rightTotal++; if (outlier) right++; } }
                if (leftTotal == 0 || rightTotal == 0) return ValueTask.FromResult<bool?>(null);
                var difference = (float)right / rightTotal - (float)left / leftTotal;
                return ValueTask.FromResult<bool?>(difference > .15f ? true : difference < -.15f ? false : null);
            }
            catch { return ValueTask.FromResult<bool?>(null); }
        }
    }
}

public sealed class ImageSwitchStateProvider : ISwitchStateReader
{
    private readonly AndroidVisualControlStateReaderFactory.Reader _reader;
    public ImageSwitchStateProvider(SKBitmap bitmap, int width, int height) => _reader = new(bitmap, width, height);
    public PerceptionFrame Frame => _reader.Frame;
    public ValueTask<bool?> ReadAsync(ElementBounds bounds, CancellationToken cancellationToken = default) => _reader.ReadAsync(bounds, cancellationToken);
}
