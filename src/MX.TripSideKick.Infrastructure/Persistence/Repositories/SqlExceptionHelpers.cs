using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>
/// Helpers for distinguishing specific SQL Server failure modes from a generic
/// <see cref="DbUpdateException"/>, so repositories can translate expected races (e.g. two
/// concurrent inserts violating a unique index) into the application's own domain exceptions
/// instead of letting a raw <see cref="SqlException"/> surface as an unhandled 500.
/// </summary>
internal static class SqlExceptionHelpers
{
    // SQL Server error numbers for unique index/constraint violations:
    // 2601 = "Cannot insert duplicate key row... with unique index"
    // 2627 = "Violation of UNIQUE KEY constraint" / "Violation of PRIMARY KEY constraint"
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is UniqueIndexViolation or UniqueConstraintViolation);
}
