# 0009. Double-entry ledger model

- **Status:** Accepted
- **Date:** 2026-10-12

## Context

[ADR 0003](0003-modular-monolith-with-ledger-as-posting-engine.md) makes the Ledger the only place where money moves. This ADR decides how it records money movement and keeps balances.

## Decision

- **Double entry.** Money moves through a `JournalEntry` of at least two `Posting`s. Total debits equal total credits, all in one currency. An entry that does not balance cannot be created.
- **A posting is an account, a `Direction` (Debit or Credit) and a positive amount.** The direction carries the sign.
- **Account types follow accounting.** Customer accounts are **liabilities** (the bank owes the deposit), so credits increase them. The bank's cash and clearing accounts are **assets**, increased by debits. Fees go to an **income** account.
- **The balance is stored** on the account and changed in the same transaction as the postings. The postings are the source of truth; a daily reconciliation job will check that the balance equals the sum of the postings.
- **Customer accounts cannot go negative** (insufficient funds is a `Result` failure). The bank's internal accounts can. Overdraft (KMH) is a later feature.
- **Entries are immutable.** A mistake is corrected with a reversing entry, never by editing or deleting.
- Each entry carries an **idempotency key**. Posting a key that was already posted must return the original result instead of posting again. The domain cannot see past entries, so this is enforced by a unique constraint in the database in phase 2; **until then, posting the same entry twice applies it twice.**
- `PostingEngine.Post` validates the whole entry before changing any balance, so a rejected entry leaves every account untouched.

Transfers use a small state machine: `Pending`, then `Completed` or `Failed(reason)`, both final. Repeating the same transition is harmless, so the recovery job can retry. A contradictory transition (failing a completed transfer) is a bug and throws.

## Alternatives considered

- **Signed amounts instead of directions:** the sum of an entry is simply zero, but what "-30" means depends on the account type. That invites mistakes and is not how auditors read the books.
- **Computing balances from postings on every read:** can never disagree with the postings, but gets slower as history grows and leaves no single row to lock against concurrent transfers.

## Consequences

- The accounting equation holds by construction: assets + expenses = liabilities + income.
- The reconciliation job and the row locking in phase 2 rely on the stored balance.
- Multi-currency transfers need an FX account and two balanced entries; not supported yet.
- Posting engine tests must include an entry where the same account appears in more than one posting, and a customer balance that ends exactly at zero. An entry that touches each account once cannot detect a bug that checks postings one by one instead of their total, and no other case detects a check that treats zero as insufficient.
