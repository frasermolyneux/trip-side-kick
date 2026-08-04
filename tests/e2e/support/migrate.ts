import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import {
  INFRASTRUCTURE_PROJECT_DIR,
  MIGRATION_CONNECTION_STRING_ENV_VAR,
  REPO_ROOT,
  SOLUTION_PATH,
  WEB_PROJECT_DIR
} from './env.ts';

/**
 * Applies EF Core migrations to the given (ephemeral, Testcontainers) database via
 * `dotnet ef database update`. Mirrors `SqlServerContainerFixture`'s
 * `dbContext.Database.MigrateAsync()` approach used by the .NET integration tests - appropriate
 * for a throwaway database, unlike the CI deploy workflows' idempotent-script mechanism (see
 * docs/data-and-persistence.md), which targets a real, persistent Azure SQL database.
 */
export async function applyMigrations(connectionString: string): Promise<void> {
  await runDotnet(
    [
      'tool',
      'run',
      'dotnet-ef',
      'database',
      'update',
      '--project',
      fileURLToPath(INFRASTRUCTURE_PROJECT_DIR),
      '--startup-project',
      fileURLToPath(WEB_PROJECT_DIR)
    ],
    { [MIGRATION_CONNECTION_STRING_ENV_VAR]: connectionString }
  );
}

/** Restores the repo's local `dotnet-ef` tool (see `.config/dotnet-tools.json`) if not already present. */
export async function restoreDotnetTools(): Promise<void> {
  await runDotnet(['tool', 'restore']);
}

/**
 * Restores NuGet packages for the whole solution. `dotnet ef database update` needs the
 * Infrastructure/Web projects' `obj/project.assets.json` to build its design-time model and does
 * NOT trigger an implicit restore itself - on a fresh checkout (no prior `dotnet build`/`dotnet
 * test`/`dotnet restore`), skipping this makes migrations fail with NETSDK1004. Idempotent/cheap
 * to re-run, so always called from `global-setup.ts` rather than relying on a separate CI step or
 * a developer having already built the solution.
 */
export async function restoreSolution(): Promise<void> {
  await runDotnet(['restore', fileURLToPath(SOLUTION_PATH)]);
}

function runDotnet(args: string[], extraEnv: Record<string, string> = {}): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn('dotnet', args, {
      cwd: fileURLToPath(REPO_ROOT),
      env: { ...process.env, ...extraEnv },
      stdio: 'inherit',
      shell: process.platform === 'win32'
    });

    child.on('error', reject);
    child.on('exit', (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`dotnet ${args.join(' ')} exited with code ${code}`));
      }
    });
  });
}
