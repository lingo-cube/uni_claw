using UniClaw.Core.Domain;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// AppEntryPoint — App 入口坐标 (归一化 0-1) + 置信度。
/// FindAppEntryAsync 的返回值。
/// 从 StateMachine/StepContext.cs 迁入 UniBrain/ (IPageAnalyzer 返回类型)。
/// 扩展: 新增 AppName + Confidence 字段。
/// </summary>
public sealed record class AppEntryPoint
{
    /// <summary>目标 App 名称</summary>
    public string AppName { get; init; }

    /// <summary>X 坐标 (归一化 0-1)</summary>
    public double X { get; init; }

    /// <summary>Y 坐标 (归一化 0-1)</summary>
    public double Y { get; init; }

    /// <summary>置信度 (0-1)</summary>
    public double Confidence { get; init; }

    /// <summary>
    /// 构造 AppEntryPoint — 所有值域校验 fail-fast。
    /// </summary>
    /// <param name="AppName">目标 App 名称 (非空)</param>
    /// <param name="X">X 坐标 [0,1]</param>
    /// <param name="Y">Y 坐标 [0,1]</param>
    /// <param name="Confidence">置信度 [0,1]</param>
    public AppEntryPoint(string AppName, double X, double Y, double Confidence = 1.0)
    {
        if (string.IsNullOrWhiteSpace(AppName))
            throw new DomainValidationException(nameof(AppName), AppName);
        if (X < 0.0 || X > 1.0)
            throw new DomainValidationException(nameof(X), X);
        if (Y < 0.0 || Y > 1.0)
            throw new DomainValidationException(nameof(Y), Y);
        if (Confidence < 0.0 || Confidence > 1.0)
            throw new DomainValidationException(nameof(Confidence), Confidence);
        this.AppName = AppName;
        this.X = X;
        this.Y = Y;
        this.Confidence = Confidence;
    }
}
