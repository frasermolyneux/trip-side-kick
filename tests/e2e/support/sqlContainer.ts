import { GenericContainer, Wait, type StartedTestContainer } from 'testcontainers';

import { SQL_DATABASE_NAME, SQL_SA_PASSWORD } from './env.ts';

/** Disposable PostgreSQL 18 instance, shared by this single-worker browser run only. */
export async function startSqlContainer(): Promise<StartedTestContainer> {
  return new GenericContainer('postgres:18-bookworm')
    .withEnvironment({
      POSTGRES_USER: 'tripsidekick',
      POSTGRES_DB: SQL_DATABASE_NAME,
      POSTGRES_PASSWORD: SQL_SA_PASSWORD
    })
    .withExposedPorts(5432)
    .withWaitStrategy(Wait.forLogMessage(/database system is ready to accept connections/, 2))
    .withStartupTimeout(120_000)
    .start();
}

export function buildConnectionString(container: StartedTestContainer): string {
  return `Host=${container.getHost()};Port=${container.getMappedPort(5432)};Database=${SQL_DATABASE_NAME};Username=tripsidekick;Password=${SQL_SA_PASSWORD};GSS Encryption Mode=Disable;SSL Mode=Disable`;
}
