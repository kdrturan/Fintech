namespace Banking.Modules.Ledger.Domain;

/// <summary>
/// The accounting type decides which side increases the balance:
/// assets and expenses grow with debits, liabilities and income grow with credits.
/// </summary>
public enum AccountType
{
    /// <summary>Something the bank owns, such as cash or money held at the clearing house.</summary>
    Asset,

    /// <summary>Something the bank owes. Every customer account is a liability: the deposit belongs to the customer.</summary>
    Liability,

    /// <summary>Money the bank earns, such as transfer fees.</summary>
    Income,

    /// <summary>Money the bank spends.</summary>
    Expense,
}
