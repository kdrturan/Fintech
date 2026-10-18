# 0007. Results for business outcomes, exceptions for bugs

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

Two very different things can stop an operation. A **bug**: adding TRY to USD, an unbalanced journal entry built by our own code, completing a failed transfer. And an **expected business outcome**: insufficient funds, a transfer to the same account. The second kind happens thousands of times a day and every caller must handle it.

## Decision

- **Bugs throw exceptions** and are never caught in order to continue.
- **Business outcomes return a `Result` or `Result<T>`** carrying an `Error(Code, Message)`. `Code` is stable and machine-readable (`ledger.insufficient_funds`) and will become the API error code.
- `Result` is our own small type in the Shared Kernel. Reading `Value` from a failed result throws, because that is a bug.

## Alternatives considered

- **Exceptions for everything** (a `BusinessException` with an error code, translated to HTTP 422 by middleware): the most common style in existing .NET banking code, and less code to write. Rejected because the method signature then hides that it can fail for a business reason, and expected outcomes travel through the mechanism meant for unexpected ones.
- **A library (ErrorOr, FluentResults):** well tested, but adds a dependency for about fifty lines of code.
- **Results for everything:** bugs could be silently ignored.

## Consequences

- A method that can fail for a business reason says so in its return type.
- More `if (result.IsFailure) return result.Error;` code.
- Deciding which kind a failure is becomes part of every design discussion. Rule of thumb: can a correct program, given valid input, reach this state? If yes, it is a `Result`.
