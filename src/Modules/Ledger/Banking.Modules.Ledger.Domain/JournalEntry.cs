using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Domain;

/// <summary>
/// A balanced set of postings that moves money between accounts: total debits always equal total credits.
/// Entries are never changed or deleted; a mistake is corrected with a reversing entry.
/// </summary>
public sealed class JournalEntry
{
    private readonly List<Posting> _postings;

    private JournalEntry(JournalEntryId id, Guid idempotencyKey, List<Posting> postings)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        _postings = postings;
    }

    /// <summary>For EF Core, which creates the entry first and then loads its postings into the list.</summary>
    private JournalEntry(JournalEntryId id, Guid idempotencyKey)
        : this(id, idempotencyKey, [])
    {
    }

    public JournalEntryId Id { get; }

    /// <summary>Identifies the business operation (for example the transfer id), so posting it twice can be detected.</summary>
    public Guid IdempotencyKey { get; }

    /// <summary>A read-only view: casting it back to a list and changing it is not possible.</summary>
    public IReadOnlyList<Posting> Postings => _postings.AsReadOnly();

    public Currency Currency => Postings[0].Amount.Currency;

    /// <summary>
    /// Creates a balanced entry. Entries are built by our own code, never directly from user input,
    /// so a malformed entry is a bug and throws instead of returning a <see cref="Result"/>.
    /// </summary>
    public static JournalEntry Create(Guid idempotencyKey, IReadOnlyList<Posting> postings)
    {
        ArgumentNullException.ThrowIfNull(postings);

        if (idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException("A journal entry needs an idempotency key.", nameof(idempotencyKey));
        }

        if (postings.Count < 2)
        {
            throw new ArgumentException("A journal entry needs at least two postings.", nameof(postings));
        }

        var currency = postings[0].Amount.Currency;
        if (postings.Any(posting => posting.Amount.Currency != currency))
        {
            throw new ArgumentException("All postings of a journal entry must use the same currency.", nameof(postings));
        }

        if (postings.Any(posting => !posting.Amount.IsPositive))
        {
            throw new ArgumentException("Posting amounts must be positive; the direction carries the sign.", nameof(postings));
        }

        var debits = Total(postings, Direction.Debit, currency);
        var credits = Total(postings, Direction.Credit, currency);
        if (debits != credits)
        {
            throw new ArgumentException($"The entry does not balance: debits {debits}, credits {credits}.", nameof(postings));
        }

        return new JournalEntry(JournalEntryId.New(), idempotencyKey, [.. postings]);
    }

    /// <summary>Creates the entry that cancels this one: the same postings with opposite directions.</summary>
    public JournalEntry Reverse(Guid idempotencyKey) =>
        Create(idempotencyKey, [.. Postings.Select(posting => posting with { Direction = Opposite(posting.Direction) })]);

    private static Money Total(IEnumerable<Posting> postings, Direction direction, Currency currency) =>
        postings
            .Where(posting => posting.Direction == direction)
            .Aggregate(Money.Zero(currency), (total, posting) => total + posting.Amount);

    private static Direction Opposite(Direction direction) =>
        direction == Direction.Debit ? Direction.Credit : Direction.Debit;
}
