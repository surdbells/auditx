using System.Text;

namespace AuditX.Application.Common.Enums;

/// <summary>
/// Renders enum members as snake_case strings for API DTOs (e.g. <c>AwaitingRoleAssignment</c> →
/// <c>awaiting_role_assignment</c>), matching the values used throughout the specification, the SPA, and
/// the persisted column values.
/// </summary>
public static class EnumExtensions
{
    public static string ToSnake(this Enum value)
    {
        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
