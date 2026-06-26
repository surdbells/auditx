using AuditX.Infrastructure.Persistence.Naming;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AuditX.Infrastructure.Persistence.Conversions;

/// <summary>
/// Persists an enum as its snake_case string (e.g. <c>AwaitingRoleAssignment</c> →
/// <c>awaiting_role_assignment</c>), matching the values used throughout the specification and APIs.
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum>()
    : ValueConverter<TEnum, string>(
        v => SnakeCase.Convert(v.ToString()!),
        v => Parse(v))
    where TEnum : struct, Enum
{
    private static TEnum Parse(string value)
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (SnakeCase.Convert(candidate.ToString()) == value)
            {
                return candidate;
            }
        }

        return Enum.Parse<TEnum>(value, ignoreCase: true);
    }
}
