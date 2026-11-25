using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using NLog;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.QQProxy.Crypto;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.QQProxy;

namespace PlugBotSharp.Tests;

public class QQCallbackTests
{
    private const string Secret = "pbs-test-secret";
    private const string EventTs = "1720000000";
    private const string PlainToken = "pbs_validation_token";
    private const string ExpectedSeed = "7062732d746573742d7365637265747062732d746573742d7365637265747062";
    private const string ExpectedPublicKey = "6c527f6c9ca37f2769e8be4b231cd337fdb94c23b6de6b8af0ec57b4ca6ab7c9";
    private const string ExpectedSignature =
        "a79104ad6aadd754f180eca418ba384d799060fc4fb55c0b9f43e5a2fcf3ce20" +
        "0b2d778a6a064de95581522a0a9b3a75dd83c8b09a1fc5fe23fc4bb980a84c01";

    public QQCallbackTests()
    {
        ProxyInstance.Logger = LogManager.GetLogger("QQCallbackTests");
    }

    private static async Task<string> SendAsync(string body, string secret = Secret)
    {
        var (_, text) = await SendFullAsync(body, secret);

        return text;
    }

    private static async Task<(int Status, string Body)> SendFullAsync(string body, string secret = Secret,
        string? timestamp = null, string? signature = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "pbs-qq-callback", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            File.WriteAllText(Path.Combine(folder, "config.json"),
                $$"""{"AppId":"102000001","AppSecret":"{{secret}}","Port":10000,"Type":0}""");

            var instance = new ProxyInstance("qq");
            instance.Init(folder, Guid.NewGuid());
            instance.Load();

            var ctx = new DefaultHttpContext();
            ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            ctx.Response.Body = new MemoryStream();

            if (timestamp is not null)
            {
                ctx.Request.Headers["X-Signature-Timestamp"] = timestamp;
            }

            if (signature is not null)
            {
                ctx.Request.Headers["X-Signature-Ed25519"] = signature;
            }

            await CallbackHandler.HandleAsync(ctx, instance);

            ctx.Response.Body.Position = 0;
            var text = await new StreamReader(ctx.Response.Body).ReadToEndAsync();

            return (ctx.Response.StatusCode, text);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 按平台规则签一条事件报文
    /// </summary>
    private static string Sign(string body, string timestamp = EventTs, string secret = Secret)
    {
        var seed = CallbackHandler.DeriveSeed(secret);

        return Convert.ToHexStringLower(Ed25519.Sign(seed, Encoding.UTF8.GetBytes(timestamp + body)));
    }

    private static string ValidationBody()
    {
        return $"{{\"op\":13,\"d\":{{\"plain_token\":\"{PlainToken}\",\"event_ts\":\"{EventTs}\"}}}}";
    }

    [Fact]
    public void DeriveSeed_pads_the_secret_to_32_bytes()
    {
        Assert.Equal(ExpectedSeed, Convert.ToHexStringLower(CallbackHandler.DeriveSeed(Secret)));
        Assert.Equal(new string('a', 32), Encoding.UTF8.GetString(CallbackHandler.DeriveSeed(new string('a', 40))));
    }

    [Fact]
    public async Task Validation_request_returns_signed_plain_token()
    {
        var json = await SendAsync(ValidationBody());

        var obj = JsonNode.Parse(json)!;
        Assert.Equal(PlainToken, obj["plain_token"]!.GetValue<string>());
        Assert.Equal(ExpectedSignature, obj["signature"]!.GetValue<string>());
    }

    [Fact]
    public async Task Validation_signature_is_verifiable()
    {
        var json = await SendAsync(ValidationBody());

        var signature = Convert.FromHexString(JsonNode.Parse(json)!["signature"]!.GetValue<string>());
        var message = Encoding.UTF8.GetBytes(EventTs + PlainToken);

        Assert.True(Ed25519.Verify(Convert.FromHexString(ExpectedPublicKey), message, signature));
    }

    [Fact]
    public async Task Validation_request_without_secret_returns_ack()
    {
        var json = await SendAsync(ValidationBody(), "");

        Assert.Equal("", json);
    }

    [Fact]
    public async Task Event_push_returns_ack()
    {
        const string body = """{"op":0,"t":"GROUP_AT_MESSAGE_CREATE","d":{"id":"123"}}""";

        var (status, json) = await SendFullAsync(body, Secret, EventTs, Sign(body));

        Assert.Equal(200, status);
        Assert.Equal("", json);
    }

    [Fact]
    public async Task Event_without_signature_is_rejected()
    {
        const string body = """{"op":0,"t":"GROUP_AT_MESSAGE_CREATE","d":{"id":"123"}}""";

        var (status, json) = await SendFullAsync(body, Secret, EventTs);

        Assert.Equal(403, status);
        Assert.Equal("", json);
    }

    [Fact]
    public async Task Event_with_wrong_signature_is_rejected()
    {
        const string body = """{"op":0,"t":"GROUP_AT_MESSAGE_CREATE","d":{"id":"123"}}""";

        var (status, json) = await SendFullAsync(body, Secret, EventTs, new string('0', 128));

        Assert.Equal(403, status);
        Assert.Equal("", json);
    }

    [Fact]
    public async Task Event_push_reaches_the_message_hooks()
    {
        var owner = new object();
        ProxyMessage? received = null;

        try
        {
            EventManager.SubscribeMessage(owner, message =>
            {
                received = message;
                return [new TextMessage("reply")];
            });

            const string body = """
                {"op":0,"t":"GROUP_AT_MESSAGE_CREATE","d":{"id":"msg-1","content":"/text","group_openid":"GROUP","author":{"member_openid":"SENDER"}}}
                """;

            var (status, json) = await SendFullAsync(body, Secret, EventTs, Sign(body));

            Assert.Equal(200, status);
            Assert.Equal("", json);

            // 事件是后台处理的，等一小会儿
            for (var i = 0; i < 100 && received is null; i++)
            {
                await Task.Delay(20);
            }

            Assert.NotNull(received);
            Assert.Equal("GROUP", received!.Target);
            Assert.Equal("SENDER", received.Sender);
            Assert.Equal("/text", Assert.IsType<TextMessage>(received.Message).Text);
        }
        finally
        {
            EventManager.Unregister(owner);
        }
    }

    [Fact]
    public async Task Broken_body_returns_ack()
    {
        var json = await SendAsync("not json");

        Assert.Equal("", json);
    }

    [Fact]
    public async Task Validation_without_data_returns_ack()
    {
        var json = await SendAsync("""{"op":13}""");

        Assert.Equal("", json);
    }
}
