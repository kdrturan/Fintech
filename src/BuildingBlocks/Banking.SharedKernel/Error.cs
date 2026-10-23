using System.Diagnostics.CodeAnalysis;

namespace Banking.SharedKernel;

/// <summary>
/// An expected business outcome that stopped an operation, such as insufficient funds.
/// <see cref="Code"/> is stable and machine-readable (it is the API error code); <see cref="Message"/> is for people.
/// <see cref="Type"/> says what kind of outcome it is, so the API can choose a status code without knowing every code.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Error' is only reserved in Visual Basic; this solution is C#-only and the name is the clearest one.")]
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.BusinessRule);
