namespace Banking.SharedKernel;

/// <summary>The kind of an expected failure. The domain decides the kind; the API maps it to an HTTP status code.</summary>
public enum ErrorType
{
    /// <summary>The request is valid, but a business rule does not allow it, such as insufficient funds.</summary>
    BusinessRule,

    /// <summary>The input itself is malformed, such as an unknown currency code.</summary>
    Validation,

    /// <summary>Something the request refers to does not exist.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state, such as the same request still being processed.</summary>
    Conflict,
}
