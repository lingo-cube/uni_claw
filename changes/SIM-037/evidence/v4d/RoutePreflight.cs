using System.Text;
using System.Text.Json;
using UniClaw.Agent.Dsh;
try {
    var endpoint = new DshServiceEndpoint(new Uri("http://127.0.0.1:3081/"));
    var credential = DshWebCredential.Mint(endpoint);
    using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint.BaseUri, "/api/uniclaw-agent/handshake"));
    request.Headers.Host = endpoint.BaseUri.Authority;
    request.Headers.Add("Cookie", credential.CookieHeader);
    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    using var response = await client.SendAsync(request);
    var raw = await response.Content.ReadAsStringAsync();
    using var body = JsonDocument.Parse(raw);
    var ready = (int)response.StatusCode == 200 && body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("error", out _);
    Console.WriteLine(JsonSerializer.Serialize(new { ready, status = (int)response.StatusCode }));
    return ready ? 0 : 1;
} catch (Exception error) {
    Console.WriteLine(JsonSerializer.Serialize(new { ready = false, errorKind = error.GetType().Name }));
    return 1;
}
