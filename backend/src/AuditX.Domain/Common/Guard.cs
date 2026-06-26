namespace AuditX.Domain.Common;

/// <summary>Lightweight guard clauses for protecting domain invariants at construction/mutation time.</summary>
public static class Guard
{
    public static string NotNullOrWhiteSpace(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(code, message);
        }

        return value;
    }

    public static void Against(bool condition, string code, string message)
    {
        if (condition)
        {
            throw new DomainException(code, message);
        }
    }

    public static string MinLength(string? value, int minLength, string code, string message)
    {
        if (value is null || value.Trim().Length < minLength)
        {
            throw new DomainException(code, message);
        }

        return value;
    }
}
