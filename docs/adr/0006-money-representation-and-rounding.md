# 0006. Money: representation, rounding and where it lives

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

Almost every module works with monetary amounts: the Ledger posts them, Transfers checks them, Payments will compute fees. A ledger must never lose or invent a single kuruş, so money has rules that must be identical everywhere: amounts are exact, every currency has its own smallest unit (TRY 2 digits, JPY 0, KWD 3), and calculations such as fees or splitting a bill produce fractions that must be resolved the same way in every module. If two modules round differently, fractions of a kuruş accumulate and reconciliation fails.

## Decision

Where money lives:

- `Money` and `Currency` are immutable value objects in **`Banking.SharedKernel`**, used by every module's domain. The Shared Kernel only admits dependency-free building blocks that rarely change; no rule that belongs to a single module.
- **Contracts never expose `Money`.** Module APIs and integration events carry `decimal amount` and `string currency` (ISO 4217), because a contract may later be read by a separate process that must not depend on our domain types.

How money works:

- `Money` holds a **`decimal` amount and a `Currency`**. `decimal` is base-10, so `0.1 + 0.2` is exactly `0.3`, and it maps directly to PostgreSQL `numeric`.
- Supported currencies are a closed list: **TRY, USD, EUR** (2 digits) and **JPY** (0 digits, to prove the code makes no two-digit assumption).
- **An amount with more decimal places than the currency allows is rejected** (`Money.Of(10.505m, TRY)` throws). Money never rounds silently.
- Calculations that produce fractions call **`Money.Round`** or `Multiply` explicitly. The default mode is **half away from zero** (2.5 becomes 3). The .NET default, half to even (2.5 becomes 2), is only used when asked for.
- **`Allocate(n)`** splits an amount into parts that differ by at most one smallest unit and **always add up to the original** (100.00 / 3 = 33.34 + 33.33 + 33.33).
- `Money` **may be negative**. Rules such as "a transfer amount must be positive" belong to the code that uses it.
- Combining different currencies throws: no valid flow adds TRY to USD, so it is a bug.

## Alternatives considered

- **Each module defines its own `Money`:** full independence, but duplicated money rules that can drift apart. For money, divergent rounding is a correctness bug, not a style issue.
- **`Money` owned by the Ledger, other modules use raw `decimal`:** clear ownership, but Transfers and Payments lose type safety for their own calculations, or quietly re-implement `Money`.
- **`long` in minor units (kuruş):** exact and fast, but every boundary needs conversions, and interest, fee and FX calculations still need fractional intermediates. Common in high-volume payment processors; unnecessary here.
- **`double`:** binary floating point cannot represent 0.1. Never used for money.
- **Rounding silently on creation:** convenient, but rounding then happens in places nobody chose, and the differences surface only at reconciliation.

## Consequences

- One definition of money semantics across the system. A change to `Money` affects every module, so changes must be rare and well tested.
- Every rounding point is visible in the code (search for `Round` and `Multiply`).
- Callers that receive user input must check the number of decimal places before creating `Money`.
- Mapping between `Money` and primitive fields is needed at every Contracts boundary.
- Cross-currency operations need an explicit exchange step, which is out of scope for now.
- Tests pass decimal values as strings parsed with `CultureInfo.InvariantCulture`, never as `double` attribute arguments: C# attributes cannot hold `decimal`, and a `double` such as `-55.55` arrives as `-55.549999999999997`, so the test would check a different amount than the one written.
