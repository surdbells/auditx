using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AuditX.Infrastructure.Persistence;

/// <summary>Helpers for classifying EF Core persistence failures at the Infrastructure boundary.</summary>
internal static class PersistenceErrors
{
    /// <summary>True when the exception is a SQL Server unique-constraint / unique-index violation (2627 / 2601).</summary>
    public static bool IsUniqueViolation(Exception ex)
        => ex is DbUpdateException && ex.InnerException is SqlException { Number: 2601 or 2627 };
}
