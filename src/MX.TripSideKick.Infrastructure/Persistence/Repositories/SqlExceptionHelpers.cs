using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

internal static class SqlExceptionHelpers
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static bool IsTransactionConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }
}
