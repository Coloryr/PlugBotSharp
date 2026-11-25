using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PlugBotSharp.Core.Http;
using PlugBotSharp.OneBotProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 反向 HTTP / 反向 WS 服务端的接收
/// </summary>
public class OneBotServerTests
{
    [Fact]
    public async Task Http_event_is_raised()
    {
        var owner = new object();
        var server = new OneBotServer();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.EventReceived += evt => received.TrySetResult(evt.GetProperty("post_type").GetString()!);

        try
        {
            Assert.True(server.Start(owner, "127.0.0.1", 23112, "secret", webSocket: false, http: true));

            using var http = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:23112/")
            {
                Content = new StringContent("""{"post_type":"message","message_type":"group"}""", Encoding.UTF8, "application/json")
            };

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");

            using var res = await http.SendAsync(req);

            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
            Assert.Equal("message", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            ServerRegistry.StopAll(owner);
        }
    }

    [Fact]
    public async Task Wrong_token_is_rejected()
    {
        var owner = new object();
        var server = new OneBotServer();

        try
        {
            Assert.True(server.Start(owner, "127.0.0.1", 23113, "secret", webSocket: false, http: true));

            using var http = new HttpClient();
            using var res = await http.PostAsync("http://127.0.0.1:23113/",
                new StringContent("""{"post_type":"message"}""", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
        finally
        {
            ServerRegistry.StopAll(owner);
        }
    }

    [Fact]
    public async Task Non_event_body_is_ignored()
    {
        var owner = new object();
        var server = new OneBotServer();
        var raised = false;

        server.EventReceived += _ => raised = true;

        try
        {
            Assert.True(server.Start(owner, "127.0.0.1", 23114, null, webSocket: false, http: true));

            using var http = new HttpClient();
            await http.PostAsync("http://127.0.0.1:23114/",
                new StringContent("""{"hello":"world"}""", Encoding.UTF8, "application/json"));

            await Task.Delay(200);

            Assert.False(raised);
        }
        finally
        {
            ServerRegistry.StopAll(owner);
        }
    }

    [Fact]
    public async Task Server_is_closed_when_the_owner_stops()
    {
        var owner = new object();
        var server = new OneBotServer();

        Assert.True(server.Start(owner, "127.0.0.1", 23116, null, webSocket: false, http: true));

        using var http = new HttpClient();

        using (var res = await http.PostAsync("http://127.0.0.1:23116/",
            new StringContent("""{"post_type":"message"}""", Encoding.UTF8, "application/json")))
        {
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        ServerRegistry.StopAll(owner);

        await Task.Delay(300);

        await Assert.ThrowsAnyAsync<Exception>(() => http.PostAsync("http://127.0.0.1:23116/",
            new StringContent("""{"post_type":"message"}""", Encoding.UTF8, "application/json")));
    }
}
