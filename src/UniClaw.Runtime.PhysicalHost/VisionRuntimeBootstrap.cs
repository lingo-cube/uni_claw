namespace UniClaw.Runtime.PhysicalHost;

using System.Text.Json.Nodes;
using UniClaw.Vision.Host;

/// <summary>Vision 运行时模式（vision-runtime-bootstrap）：显式、互斥、绝不推断。</summary>
public enum VisionRuntimeMode
{
    /// <summary>PhysicalHost 组合根启动并拥有 VisionServiceHost 生命周期；端点是 host 输出。</summary>
    Managed,

    /// <summary>PhysicalHost 消费显式提供的外部端点；不拥有 Vision 进程。</summary>
    External,
}

/// <summary>
/// 解析后的 Vision 运行时配置（单一配置源）。managed 模式下端点不是配置输入——
/// 由 VisionServiceHost.SocketPath 产出。
/// </summary>
public sealed record VisionRuntimeConfiguration(
    VisionRuntimeMode Mode,
    string? PythonExecutable,
    string AppRoot,
    string PerceptionRepoRoot,
    string? ReceiptPath,
    string? ExternalSocketPath)
{
    public static VisionRuntimeConfiguration Managed(
        string pythonExecutable, string appRoot, string perceptionRepoRoot, string receiptPath)
        => new(VisionRuntimeMode.Managed, pythonExecutable, appRoot, perceptionRepoRoot, receiptPath, null);

    public static VisionRuntimeConfiguration External(string externalSocketPath)
        => new(VisionRuntimeMode.External, null, "", "", null, externalSocketPath);
}

/// <summary>
/// 生产 Vision 运行时 bootstrap（vision-runtime-bootstrap A1/A1.1/A1.2/A3）：
/// 解析 → 早验证 → 经 CanonicalVisionHostFactory 创建 managed host。
/// 生命周期 owner 是 PhysicalHost 应用/组合根；本类只提供组合 helper，
/// 不引入隐藏的长驻全局进程所有权。
/// </summary>
public static class VisionRuntimeBootstrap
{
    /// <summary>仓库管理开发运行时（repository truth：系统 python3 无法导入 uniclaw_perception）。</summary>
    public const string RepositoryManagedPythonRelative = ".venv-local-vision/bin/python";

    /// <summary>感知仓库相对仓库根。</summary>
    public const string PerceptionRepoRelative = "platforms/perception";

    /// <summary>部署身份 receipt 相对仓库根（mi.py ACTIVE_IDENTITY 权威源）。</summary>
    public const string ReceiptRelative = "platforms/perception/governance/artifacts/current-active-identity.json";

    /// <summary>模块入口相对感知仓库（-m uvicorn uniclaw_perception.server:app 的模块路径）。</summary>
    public const string ServerModuleRelative = "uniclaw_perception/server.py";

    /// <summary>
    /// 确定性解析仓库/应用根：从当前程序集输出目录向上找含 platforms/perception 的目录。
    /// 不依赖任意进程工作目录（T14）。
    /// </summary>
    public static string ResolveAppRoot(string? startDirectory = null)
    {
        var dir = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, PerceptionRepoRelative)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"无法从 '{startDirectory ?? AppContext.BaseDirectory}' 向上定位仓库根（需含 {PerceptionRepoRelative}）。");
    }

    /// <summary>
    /// 解析 Vision 运行时配置（A1）：
    ///   - 显式 --vision-socket ⇒ EXTERNAL_ATTACH（端点即配置，不拥有进程）
    ///   - 否则 ⇒ MANAGED：python 优先级 1 显式 --vision-python → 2 仓库管理
    ///     .venv-local-vision → 3 actionable 配置失败；repo/receipt 从仓库根确定性解析。
    /// </summary>
    public static VisionRuntimeConfiguration ResolveVisionRuntimeConfiguration(
        PhysicalHostOptions options,
        string appRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(appRoot);

        if (options.VisionSocketPath is not null)
        {
            return VisionRuntimeConfiguration.External(options.VisionSocketPath);
        }

        var python = options.VisionPythonExecutable
            ?? Path.Combine(appRoot, RepositoryManagedPythonRelative);
        var repoRoot = Path.Combine(appRoot, PerceptionRepoRelative);
        var receipt = Path.Combine(appRoot, ReceiptRelative);
        return VisionRuntimeConfiguration.Managed(python, appRoot, repoRoot, receipt);
    }

    /// <summary>
    /// 早验证（A1.1）：在启动 Vision 子进程前对已知无效配置立即、可操作地失败——
    /// 绝不等待通用健康超时。
    /// </summary>
    public static void ValidateVisionRuntimeConfiguration(VisionRuntimeConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Mode == VisionRuntimeMode.External)
        {
            if (string.IsNullOrWhiteSpace(config.ExternalSocketPath))
            {
                throw new InvalidOperationException(
                    "EXTERNAL_ATTACH 模式缺少外部 Vision 端点：请提供 --vision-socket <path>。");
            }

            return;
        }

        var python = config.PythonExecutable;
        if (string.IsNullOrWhiteSpace(python) || !File.Exists(python))
        {
            throw new FileNotFoundException(
                $"Vision Python 可执行不存在：'{python}'。请安装仓库管理运行时（.venv-local-vision）或经 --vision-python 显式指定。");
        }

        if (!Directory.Exists(config.PerceptionRepoRoot))
        {
            throw new DirectoryNotFoundException(
                $"感知仓库根不存在：'{config.PerceptionRepoRoot}'。");
        }

        if (!File.Exists(Path.Combine(config.PerceptionRepoRoot, ServerModuleRelative)))
        {
            throw new FileNotFoundException(
                $"感知模块不可解析：'{Path.Combine(config.PerceptionRepoRoot, ServerModuleRelative)}' 缺失（PYTHONPATH 应为感知仓库根）。");
        }

        if (string.IsNullOrWhiteSpace(config.ReceiptPath) || !File.Exists(config.ReceiptPath))
        {
            throw new FileNotFoundException(
                $"部署身份 receipt 不存在：'{config.ReceiptPath}'（期望 governance 工件 current-active-identity.json）。");
        }
    }

    /// <summary>
    /// 经 CanonicalVisionHostFactory 创建 managed Vision host（A1.2）：
    /// receipt 的读取与四轴身份验证由既有 canonical 验证完成（不绕过、不伪造）；
    /// 验证通过的身份在该 host 生命周期内固定——后续文件变更不会静默改变已运行进程的身份。
    /// </summary>
    public static VisionServiceHost CreateManagedVisionHost(VisionRuntimeConfiguration config)
    {
        if (config.Mode != VisionRuntimeMode.Managed)
        {
            throw new InvalidOperationException("CreateManagedVisionHost 仅用于 MANAGED 模式。");
        }

        ValidateVisionRuntimeConfiguration(config);

        // CanonicalVisionHostFactory 默认 serviceEntryPoint/modelPath/configPath 均为相对仓库根；
        // repoRoot 传仓库根（VisionServiceHost 以 {repoRoot}/platforms/perception 为 PYTHONPATH/cwd）。
        return CanonicalVisionHostFactory.Create(
            currentActiveReceiptPath: config.ReceiptPath!,
            pythonExecutable: config.PythonExecutable!,
            repoRoot: config.AppRoot);
    }
}
