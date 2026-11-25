using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.OneBotProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 服务端用 IIS 导出的 pfx 证书跑 https
/// </summary>
public class OneBotSslTests
{
    [Fact]
    public async Task Https_event_is_raised()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-onebot-ssl", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var owner = new object();
        var server = new OneBotServer();

        try
        {
            var pfx = CreateCertificate(folder);
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            server.EventReceived += evt => received.TrySetResult(evt.GetProperty("post_type").GetString()!);

            Assert.True(server.Start(owner, "127.0.0.1", 23115, null, webSocket: false, http: true,
                certificate: pfx, certificatePassword: "123456"));

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            using var http = new HttpClient(handler);
            using var res = await http.PostAsync("https://127.0.0.1:23115/",
                new StringContent("""{"post_type":"message","message_type":"group"}""", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
            Assert.Equal("message", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            ServerRegistry.StopAll(owner);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 生成一个自签名的 pfx，模拟 IIS 导出的证书
    /// </summary>
    private static string CreateCertificate(string folder)
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));

        var path = Path.Combine(folder, "test.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "123456"));

        return path;
    }
}
