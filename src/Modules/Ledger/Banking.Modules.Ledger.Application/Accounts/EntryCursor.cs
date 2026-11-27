using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using Banking.SharedKernel;

namespace Banking.Modules.Ledger.Application.Accounts;

/// <summary>
/// Where a page of entries ended, as text a client sends back unchanged. Encoded so that clients do not build one
/// themselves and what is inside may change; it is not secret and not signed, so it must never decide access.
/// </summary>
internal static class EntryCursor
{
    public static Error Invalid { get; } =
        new("ledger.invalid_cursor", "The cursor is not valid. Use the nextCursor of a previous page.", ErrorType.Validation);

    public static string Encode(long position)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, position);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string cursor, out long position)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        var status = Base64Url.DecodeFromChars(cursor, bytes, out _, out var written);
        var decoded = status == OperationStatus.Done && written == sizeof(long);

        position = decoded ? BinaryPrimitives.ReadInt64BigEndian(bytes) : 0;
        return decoded;
    }
}
