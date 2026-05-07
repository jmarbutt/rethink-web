using RethinkWeb.Actions;
using RethinkWeb.Sample.Chat.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Chat.Actions;

public sealed record SendMessageInput(string Author, string Body);

public sealed record SendMessageResult(Guid MessageId, DateTime PostedAt);

/// <summary>
/// "Post a message into this channel." The action is on Channel — the entity instance
/// in the URL is the target channel; a new Message is created as a side effect.
/// Same shape works as HTTP form post, MCP tool ("channels.send"), or programmatic call.
/// </summary>
[Action(name: "send", displayName: "Send Message",
    Description = "Post a message into the channel. The author and body are taken from input; entityId is the channel.",
    Icon = "send")]
public sealed class SendMessageAction(
    IEntityStore<Message> messages,
    IClock clock,
    IIdGenerator ids) : IAction<Channel, SendMessageInput, SendMessageResult>
{
    public async Task<SendMessageResult> ExecuteAsync(
        Channel channel,
        SendMessageInput input,
        IActionContext context,
        CancellationToken ct = default)
    {
        var msg = new Message
        {
            Id = ids.NewId(),
            ChannelId = channel.Id,
            Author = input.Author,
            Body = input.Body,
            PostedAt = clock.UtcNow.UtcDateTime,
        };
        await messages.SaveAsync(msg, ct);
        return new SendMessageResult(msg.Id, msg.PostedAt);
    }
}
