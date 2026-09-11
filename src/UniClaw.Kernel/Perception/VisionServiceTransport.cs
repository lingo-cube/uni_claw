using System.Net.Sockets;
using System.Net;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// 感知服务传输端点（PER-005 D1/D3）：配置驱动的部署选择，默认宿主本机。
/// 封闭层次结构——<see cref="UnixDomainSocket"/>（默认）与
/// <see cref="LoopbackTcp"/>（显式启用）。D3 语义：transport 只改变连接
/// 方式，不改变响应解析 / Artifact / ObservationProposal 语义；TCP 仅允许
/// loopback（无 host 字段即结构性执法——非本机地址涉及认证/加密/远程部署
/// 治理，显式 out of scope）。
/// </summary>
public abstract record VisionServiceTransport
{
    private VisionServiceTransport()
    {
    }

    /// <summary>Unix Domain Socket（默认宿主本机路径，legacy 同款）。</summary>
    public sealed record UnixDomainSocket(string SocketPath) : VisionServiceTransport
    {
        public string SocketPath { get; } = string.IsNullOrWhiteSpace(SocketPath)
            ? throw new ArgumentException("Socket path is required.", nameof(SocketPath))
            : SocketPath;
    }

    /// <summary>loopback TCP（127.0.0.1 + 显式端口；仅本机回环）。</summary>
    public sealed record LoopbackTcp(int Port) : VisionServiceTransport
    {
        public int Port { get; } = Port is >= 1 and <= 65535
            ? Port
            : throw new ArgumentOutOfRangeException(nameof(Port), "Port must be 1-65535.");
    }

    /// <summary>构造与 transport 匹配的 HttpClient（BaseAddress 已含端口/UDS 约定）。</summary>
    public HttpClient CreateHttpClient(TimeSpan timeout)
    {
        var baseAddress = this switch
        {
            UnixDomainSocket => "http://localhost",
            LoopbackTcp tcp => $"http://127.0.0.1:{tcp.Port}",
            _ => throw new InvalidOperationException($"未知 transport：{GetType().Name}"),
        };
        return new HttpClient(CreateHandler())
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = timeout,
        };
    }

    private SocketsHttpHandler CreateHandler() => this switch
    {
        UnixDomainSocket uds => new SocketsHttpHandler
        {
            // 本机服务客户端：显式禁用系统代理（UDS 面 BaseAddress 是主机名
            // http://localhost，带系统代理的开发机会把请求发给代理 → 挂死；
            // TCP 面虽是字面 IP 也一并禁用，语义统一）
            UseProxy = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(uds.SocketPath), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        },
        // LoopbackTcp：SocketsHttpHandler 默认 TCP 连接即正确；host 已结构性
        // 固定在 BaseAddress 127.0.0.1，无远程面；与 UDS 面一致禁用代理。
        LoopbackTcp => new SocketsHttpHandler { UseProxy = false },
        _ => throw new InvalidOperationException($"未知 transport：{GetType().Name}"),
    };
}
