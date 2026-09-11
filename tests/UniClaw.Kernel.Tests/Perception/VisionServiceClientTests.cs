using System.Net;
using System.Net.Sockets;
using System.Text;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-005 A2（transport 失败分类，DETERMINISTIC）——VisionServiceClient
/// 对全部失败族的 fail-closed 分类，以及 D3 语义等价（uds 与 tcp 两种
/// transport 只改连接方式，分类面必须一致）。服务端为进程内 socket double
/// （零外部依赖）；timeout 用显式短 timeout 触发。
/// </summary>
public sealed class VisionServiceClientTests
{
    private static readonly byte[] Rgba = new byte[2 * 2 * 4]; // 2×2 帧（内容不重要）

    // ---- 进程内 HTTP 服务 double -------------------------------------------

    private sealed class HttpServerDouble : IDisposable
    {
        private readonly Socket _listener;
        private readonly Thread _loop;
        private volatile bool _disposed;

        /// <summary>handler：(requestHead, body) → 完整响应字节；null = 收下请求但不回应。</summary>
        private HttpServerDouble(Socket listener, Func<string, byte[], byte[]?> handler)
        {
            _listener = listener;
            _loop = new Thread(() => AcceptLoop(handler)) { IsBackground = true };
            _loop.Start();
        }

        public static (HttpServerDouble Server, VisionServiceTransport Transport) OnLoopbackTcp(
            Func<string, byte[], byte[]?> handler)
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(4);
            var port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            return (new HttpServerDouble(listener, handler), new VisionServiceTransport.LoopbackTcp(port));
        }

        public static (HttpServerDouble Server, VisionServiceTransport Transport) OnUnixSocket(
            Func<string, byte[], byte[]?> handler)
        {
            var path = Path.Combine(Path.GetTempPath(), $"uniclaw-test-{Guid.NewGuid():N}.sock");
            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(4);
            return (new HttpServerDouble(listener, handler),
                new VisionServiceTransport.UnixDomainSocket(path));
        }

        private void AcceptLoop(Func<string, byte[], byte[]?> handler)
        {
            while (!_disposed)
            {
                Socket connection;
                try
                {
                    connection = _listener.Accept();
                }
                catch (SocketException)
                {
                    return; // listener 关闭
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                var thread = new Thread(() => Serve(connection, handler)) { IsBackground = true };
                thread.Start();
            }
        }

        private static void Serve(Socket connection, Func<string, byte[], byte[]?> handler)
        {
            using (connection)
            {
                try
                {
                    // 读到 headers 结束 + Content-Length 指定的 body
                    var buffer = new byte[8192];
                    var received = new List<byte>();
                    int headerEnd;
                    int contentLength = 0;
                    while (true)
                    {
                        var read = connection.Receive(buffer);
                        if (read == 0)
                            return;
                        received.AddRange(buffer.AsSpan(0, read).ToArray());
                        var text = Encoding.ASCII.GetString(received.ToArray());
                        headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (headerEnd >= 0)
                        {
                            foreach (var line in text.Split("\r\n"))
                            {
                                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                    contentLength = int.Parse(line["Content-Length:".Length..].Trim());
                            }
                            if (received.Count >= headerEnd + 4 + contentLength)
                                break;
                        }
                    }
                    var head = Encoding.ASCII.GetString(received.ToArray(), 0, headerEnd);
                    var body = received.Skip(headerEnd + 4).ToArray();
                    var response = handler(head, body);
                    if (response is not null)
                        connection.Send(response);
                    else
                        Thread.Sleep(TimeSpan.FromSeconds(5)); // 不回应：挂住连接等客户端超时（测试侧 timeout 先到）
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _listener.Dispose();
        }
    }

    private static byte[] HttpResponse(int status, string reason, string body) =>
        Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");

    private static Task<VisionServiceResult> AnalyzeAsync(
        VisionServiceTransport transport, TimeSpan timeout) =>
        new VisionServiceClient(transport, timeout).AnalyzeAsync(Rgba, 2, 2, CancellationToken.None);

    // ---- 成功与失败分类（TCP） ---------------------------------------------

    [Fact]
    public async Task Tcp_ValidJson_ReturnsSuccessWithVerbatimJson()
    {
        var json = """{"yolo":[],"ocr":[],"candidates":[]}""";
        var (server, transport) = HttpServerDouble.OnLoopbackTcp(
            (head, _) => HttpResponse(200, "OK", json));
        using (server)
        {
            var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
            Assert.True(result.Success);
            Assert.Equal(VisionServiceDiagnostic.None, result.Diagnostic);
            Assert.Equal(json, result.ResponseJson);
        }
    }

    [Fact]
    public async Task Tcp_NonSuccessStatus_InfrastructureFailure()
    {
        var (server, transport) = HttpServerDouble.OnLoopbackTcp(
            (_, _) => HttpResponse(500, "Internal Server Error", "boom"));
        using (server)
        {
            var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Null(result.ResponseJson);
            Assert.Equal(VisionServiceDiagnostic.InfrastructureFailure, result.Diagnostic);
        }
    }

    [Fact]
    public async Task Tcp_GarbageBody_MalformedResponse()
    {
        var (server, transport) = HttpServerDouble.OnLoopbackTcp(
            (_, _) => HttpResponse(200, "OK", "not-json{"));
        using (server)
        {
            var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
            Assert.Equal(VisionServiceDiagnostic.MalformedResponse, result.Diagnostic);
        }
    }

    [Fact]
    public async Task Tcp_MissingEnvelopeArrays_SchemaFailure()
    {
        var (server, transport) = HttpServerDouble.OnLoopbackTcp(
            (_, _) => HttpResponse(200, "OK", """{"candidates":[]}"""));
        using (server)
        {
            var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
            Assert.Equal(VisionServiceDiagnostic.SchemaFailure, result.Diagnostic);
        }
    }

    [Fact]
    public async Task Tcp_DiagnosticsInvalidGeometry_InvalidGeometry()
    {
        var (server, transport) = HttpServerDouble.OnLoopbackTcp(
            (_, _) => HttpResponse(200, "OK",
                """{"yolo":[],"ocr":[],"diagnostics":[{"code":"INVALID_GEOMETRY"}]}"""));
        using (server)
        {
            var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
            Assert.Equal(VisionServiceDiagnostic.InvalidGeometry, result.Diagnostic);
        }
    }

    [Fact]
    public async Task Tcp_NoResponse_Timeout()
    {
        var (server, transport) = HttpServerDouble.OnLoopbackTcp((_, _) => null);
        using (server)
        {
            var result = await AnalyzeAsync(transport, TimeSpan.FromMilliseconds(300));
            Assert.Equal(VisionServiceDiagnostic.Timeout, result.Diagnostic);
        }
    }

    [Fact]
    public async Task Tcp_ConnectionRefused_InfrastructureFailure()
    {
        // 独占一个端口然后关掉服务 → 连接拒绝
        var (server, transport) = HttpServerDouble.OnLoopbackTcp(
            (_, _) => HttpResponse(200, "OK", "{}"));
        server.Dispose();
        var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
        Assert.Equal(VisionServiceDiagnostic.InfrastructureFailure, result.Diagnostic);
    }

    // ---- D3：UDS 面（确定性子集 + 真实服务全链归 A4/A5） --------------------
    //
    // 说明：进程内手写 HTTP double × HttpClient-UDS-ConnectCallback 在 testhost
    // 内会 wedge（裸 UDS socket 与真实 uvicorn 服务均正常——分别由
    // RawUds 经由 A4/A5 覆盖）。故 UDS 确定性覆盖取两段：
    // (a) 全栈连接失败分类（不需要 double）；(b) endpoint 装配断言。
    // UDS 全链 happy path 与分类一致性由 A4/A5（默认 UDS transport × 真实
    // 服务）承担。

    [Fact]
    public async Task Uds_ConnectionRefused_InfrastructureFailure_SameAsTcp()
    {
        // 无人监听的 socket 路径 → 连接拒绝 → 与 TCP 面同分类
        var transport = new VisionServiceTransport.UnixDomainSocket(
            Path.Combine(Path.GetTempPath(), $"nobody-{Guid.NewGuid():N}.sock"));
        var result = await AnalyzeAsync(transport, TimeSpan.FromSeconds(5));
        Assert.Equal(VisionServiceDiagnostic.InfrastructureFailure, result.Diagnostic);
    }

    [Fact]
    public void Uds_CreateHttpClient_WiresUdsEndpoint()
    {
        var transport = new VisionServiceTransport.UnixDomainSocket("/tmp/uniclaw-vision.sock");
        using var http = transport.CreateHttpClient(TimeSpan.FromSeconds(1));
        Assert.Equal("http://localhost/", http.BaseAddress!.ToString());
        Assert.Equal(TimeSpan.FromSeconds(1), http.Timeout);
    }

    // ---- transport 结构性执法 ----------------------------------------------

    [Fact]
    public void Transport_LoopbackOnlyByConstruction_NoRemoteSurface()
    {
        // D3 结构性执法：LoopbackTcp 无 host 字段（127.0.0.1 固定）；UDS 是本机路径。
        var tcp = new VisionServiceTransport.LoopbackTcp(8080);
        Assert.Equal(8080, tcp.Port);
        using var http = tcp.CreateHttpClient(TimeSpan.FromSeconds(1));
        Assert.Equal("http://127.0.0.1:8080/", http.BaseAddress!.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisionServiceTransport.LoopbackTcp(0));
        Assert.Throws<ArgumentException>(() => new VisionServiceTransport.UnixDomainSocket(" "));
    }

    [Fact]
    public async Task Client_RejectsMismatchedRgbaLength_FailClosed()
    {
        var (_, transport) = HttpServerDouble.OnLoopbackTcp((_, _) => null);
        using var client = new VisionServiceClient(transport);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.AnalyzeAsync(new byte[7], 2, 2, CancellationToken.None));
    }
}
