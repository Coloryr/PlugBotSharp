using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using System.Security.Cryptography.X509Certificates;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;

namespace PlugBotSharp.Tests;

/// <summary>
/// ComponentApi.StartServer 从证书仓库里选证书
/// </summary>
public class ComponentServerSslTests : ComponentTestBase
{
    [Fact]
    public async Task Server_uses_a_cert_from_the_store()
    {
        var certs = Path.Combine(Root, "Certs");
        Directory.CreateDirectory(certs);

        CreateCertificate(Path.Combine(certs, "panel.pfx"), "123456");
        File.WriteAllText(Path.Combine(certs, "panel.txt"), "123456");

        CertStore.Init(certs);

        Assert.True(CertStore.TryResolve("panel", null, out var resolved, out var resolvedPassword));
        Assert.Equal("123456", resolvedPassword);

        var owner = new object();
        var server = ComponentApi.StartServer(owner, new ServerOptions
        {
            Host = "127.0.0.1",
            Port = 23120,
            Certificate = "panel"
        }, app => app.MapGet("/", () => "ok"));

        try
        {
            Assert.True(server.Start());

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            using var http = new HttpClient(handler);

            Assert.Equal("ok", await http.GetStringAsync("https://127.0.0.1:23120/"));
        }
        finally
        {
            ServerRegistry.StopAll(owner);
        }
    }

    [Fact]
    public void Unknown_cert_makes_start_fail()
    {
        var owner = new object();
        var server = ComponentApi.StartServer(owner, new ServerOptions
        {
            Host = "127.0.0.1",
            Port = 23121,
            Certificate = "missing"
        }, app => app.MapGet("/", () => "ok"));

        try
        {
            Assert.False(server.Start());
        }
        finally
        {
            ServerRegistry.StopAll(owner);
        }
    }

    /// <summary>
    /// 生成一个自签名的 pfx
    /// </summary>
    private static void CreateCertificate(string path, string password)
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));

        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }
}