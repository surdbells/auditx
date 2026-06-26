namespace AuditX.Application.Common.Exceptions;

/// <summary>An upload exceeded a configured size limit (US-M5-010). Maps to 413 Payload Too Large.</summary>
public sealed class PayloadTooLargeException(string code, string message) : Exception(message)
{
    public string ErrorCode => code;
}
