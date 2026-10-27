using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

internal static class SqlExceptionHelpers
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
