using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using PlugBotSharp.Core;
using PlugBotSharp.Core.Http;
using PlugBotSharp.Core.Messages;
using PlugBotSharp.GsCoreProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// GsCore 客户端从 core 收消息，顺带验证分片拼包
/// </summary>
public class GenshinClientTests
{
    private const string Frame = """
        {
          "bot_id": "QQ",
          "bot_self_id": "10000",
          "msg_id": "7",
          "user_type": "group",
          "group_id": "30000",
          "user_id": "20000",
          "content": [{ "type": "text", "data": "hi" }]
        }
        """;

    [Fact]
    public async Task Split_frames_are_joined_and_raised()
    {
        var owner = new object();
        var port = FreePort();
        var received = new TaskCompletionSource<GsMessageReceive>(TaskCreationOptions.RunContinuationsAsynchronously);

        var server = ComponentApi.StartServer(owner, new ServerOptions
        {
            Host = "127.0.0.1",
            Port = (ushort)port,
            WebSocket = true
        }, app => app.Map("/ws/{botId}", async ctx =>
        {
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var bytes = Encoding.UTF8.GetBytes(Frame);

            await ws.SendAsync(bytes.AsMemory(0, 20), WebSocketMessageType.Text, false, ctx.RequestAborted);
            await ws.SendAsync(bytes.AsMemory(20), WebSocketMessageType.Text, true, ctx.RequestAborted);
            await Task.Delay(300, ctx.RequestAborted);
        }));

        try
        {
            Assert.True(server.Start());

            using var client = new GenshinClient();

            client.MessageReceived += message => received.TrySetResult(message);

            await client.StartAsync(new Uri($"ws://127.0.0.1:{port}"), "bot", null);

            var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("QQ", result.BotId);
            Assert.Equal("7", result.MsgId);
            Assert.Equal("30000", result.GroupId);
            Assert.Equal("hi", result.Content[0].Data!.GetValue<string>());

            await client.StopAsync();
        }
        finally
        {
            ServerRegistry.StopAll(owner);
        }
    }

    [Fact]
    public async Task Instance_dispatches_incoming_messages_and_sends_replies_back()
    {
        var owner = new object();
        var port = FreePort();
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var server = ComponentApi.StartServer(owner, new ServerOptions
        {
            Host = "127.0.0.1",
            Port = (ushort)port,
            WebSocket = true
        }, app => app.Map("/ws/{botId}", async ctx =>
        {
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var bytes = Encoding.UTF8.GetBytes(Frame);

            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ctx.RequestAborted);

            var buffer = new byte[8192];
            var result = await ws.ReceiveAsync(buffer, ctx.RequestAborted);

            if (result.MessageType != WebSocketMessageType.Close)
            {
                reply.TrySetResult(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
        }));

        var folder = Path.Combine(Path.GetTempPath(), "pbs-gscore", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        var instance = new GenshinInstance("gscoreproxy");
        var plugin = new object();

        try
        {
            Assert.True(server.Start());

            File.WriteAllText(Path.Combine(folder, "config.json"),
                $$"""{"Url":"ws://127.0.0.1:{{port}}","BotId":"test","Token":"","ReconnectSeconds":5}""");

            var uuid = Guid.NewGuid();
            instance.Init(folder, uuid);
            Assert.True(instance.Load());
            instance.RegisterEventHook(new ComponentRegister(instance, "gscoreproxy", "gscoreproxy", uuid));

            // 实例平时由 InstanceManager 创建，这里手动补上同样的发送转接
            EventManager.RegisterSendTarget(instance, (ctx, reply) =>
                ctx.Uuid == uuid ? instance.SendAsync(ctx, reply) : Task.CompletedTask);

            EventManager.SubscribeMessage(plugin, _ => [new TextMessage("pong")]);

            Assert.True(instance.Enable());

            var json = await reply.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Contains("pong", json);
            Assert.Contains("user_type", json);
        }
        finally
        {
            instance.Disable();
            EventManager.Unregister(plugin);
            EventManager.Unregister(instance);
            ServerRegistry.StopAll(owner);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// 找一个空闲端口
    /// </summary>
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        listener.Stop();

        return port;
    }
}
