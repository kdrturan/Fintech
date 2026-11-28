using System.Text.Json;

namespace Banking.Messaging.Outbox;

/// <summary>
/// An integration event waiting to leave its module. It is written in the same transaction as the change it reports,
/// so either both are stored or neither is: an event can be late, but never lost and never invented.
/// A relay publishes it later and sets <see cref="PublishedAt"/>.
/// </summary>
public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage(Guid id, string type, string payload)
    {
        Id = id;
        Type = type;
        Payload = payload;
    }

    /// <summary>The message id. Consumers remember it to recognise a message they have already handled.</summary>
    public Guid Id { get; }

    /// <summary>What happened, with its version, such as <c>transfers.transfer_completed.v1</c>. Consumers route on it.</summary>
    public string Type { get; }

    /// <summary>The event as JSON.</summary>
    public string Payload { get; }

    /// <summary>Set by the database: the start of the transaction that stored the change.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Null until the relay has handed the message to the broker.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <param name="type">The event's stable name. Never the CLR type name: that changes when code is renamed.</param>
    /// <param name="payload">The event, a contract of primitives only.</param>
    public static OutboxMessage Create<TEvent>(string type, TEvent payload)
        where TEvent : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        // Version 7: ids grow with time, so the relay can take the oldest first by id alone.
        return new OutboxMessage(Guid.CreateVersion7(), type, JsonSerializer.Serialize(payload, JsonOptions));
    }
}
