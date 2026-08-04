import { GenericContainer, Wait, type StartedTestContainer } from 'testcontainers';

import { SQL_DATABASE_NAME, SQL_SA_PASSWORD } from './env.ts';

/**
 * Starts an ephemeral SQL Server 2022 container mirroring `docker-compose.yml`'s image and
 * credentials (this harness never touches Azure SQL - see docs/testing.md). The container starts
 * with the default `master` database; migrations (see `migrate.ts`) create
 * `TripSideKickE2E` via EF Core's own `CREATE DATABASE`-on-migrate behaviour.
 */
export async function startSqlContainer(): Promise<StartedTestContainer> {
  const container = await new GenericContainer('mcr.microsoft.com/mssql/server:2022-latest')
    .withEnvironment({
      ACCEPT_EULA: 'Y',
      MSSQL_SA_PASSWORD: SQL_SA_PASSWORD,
      MSSQL_PID: 'Developer'
    })
    .withExposedPorts(1433)
    .withWaitStrategy(Wait.forLogMessage(/SQL Server is now ready for client connections/i))
    .withStartupTimeout(180_000)
    .start();

  return container;
}

/** Builds the connection string for the master-scoped admin connection used by migrations. */
export function buildConnectionString(container: StartedTestContainer): string {
  const host = container.getHost();
  const port = container.getMappedPort(1433);

  return (
    `Server=${host},${port};Database=${SQL_DATABASE_NAME};User Id=sa;Password=${SQL_SA_PASSWORD};` +
    'TrustServerCertificate=true;Encrypt=true;'
  );
}
