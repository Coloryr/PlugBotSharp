using PlugBotSharp.QQProxy;

namespace PlugBotSharp.Tests;

/// <summary>
/// 被动回复的序号按消息 id 各自递增
/// </summary>
public class QQReplySeqTests
{
    [Fact]
    public void Sequence_increments_per_message_id()
    {
        var instance = new ProxyInstance("qq");

        Assert.Equal(1, instance.NextSeq("msg-1"));
        Assert.Equal(2, instance.NextSeq("msg-1"));
        Assert.Equal(3, instance.NextSeq("msg-1"));

        Assert.Equal(1, instance.NextSeq("msg-2"));
    }

    [Fact]
    public void Sequence_is_zero_without_a_message_id()
    {
        var instance = new ProxyInstance("qq");

        Assert.Equal(0, instance.NextSeq(null));
        Assert.Equal(0, instance.NextSeq(""));
    }

    [Fact]
    public void Sequence_is_per_instance()
    {
        var first = new ProxyInstance("qq");
        var second = new ProxyInstance("qq");

        Assert.Equal(1, first.NextSeq("msg-1"));
        Assert.Equal(1, second.NextSeq("msg-1"));
    }
}
