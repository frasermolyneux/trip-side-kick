# AGENTS.md — trip-side-kick

Trip Side Kick: an ASP.NET Core (.NET 10) modular monolith serving a Razor Pages brochure site and a React + TypeScript PWA / versioned `/v1` API from one Azure App Service, provisioned with Terraform and fronted by Cloudflare DNS.

## Required reading (read these first)

1. `.github/copilot-instructions.md` — repo-specific orientation
2. `.github-copilot/.github/instructions/personal.working-preferences.instructions.md` — Fraser's always-on rules (git hands-off, default to `main`, `code-review` gate)
3. `.github-copilot/.github/copilot-instructions.md` — org-wide context catalog
4. Stack-specific instruction files for the work area (see Stack guardrails below)
5. `docs/architecture-overview.md` — the host-aware surface split and modular-monolith boundaries
6. `docs/identity-and-access.md` — identity is **stubbed**; read before touching anything auth-shaped

## Org conventions via MCP (when available)

If a `frasermolyneux-copilot` MCP server is configured in your client (`~/.copilot/mcp-config.json`, VS Code user `mcp.json`, or an equivalent stdio MCP wire-up), **prefer its catalog tools** over your own assumptions when answering questions about org standards, branching, workflows, Terraform, .NET projects, Azure patterns, or shared library / platform consumption contracts. The catalog source-of-truth lives in `frasermolyneux/.github-copilot` — see `mcp-server/README.md` there for the tool contract.

This is **complementary** to the file-load model: if `./.github-copilot/` is checked out in the runner (per `copilot-setup-steps.yml`), continue to read those files directly. If both are available, prefer MCP for freshness. If no MCP server is configured in your client, treat this section as a no-op and fall back to the file paths above.

## Stack guardrails

**.NET / ASP.NET Core** — `standards.dotnet-project`, `standards.health-endpoints`, `patterns.versioned-apis`, `patterns.repository`, `patterns.nbgv-versioning`, `standards.vscode-dotnet-tasks`

**Terraform / Azure** — `standards.terraform-style`, `patterns.terraform-remote-state`, `standards.azure-naming`, `standards.azure-tagging`, `standards.oidc-and-secrets`, `tenant.subscriptions`, `tenant.regions`, `tenant.dns`, `tenant.identity`

**Platform consumption** (via `terraform_remote_state`) — `platform.workloads` (resource groups, backends, workload SP), `platform.hosting` (shared Linux App Service plan — **never create a plan**), `platform.monitoring` (shared Log Analytics workspace), `platform.connectivity` (Cloudflare zone ownership)

**Workflows** — `workflows.build-and-test`, `workflows.pr-verify`, `workflows.codequality`, `workflows.deploy-dev`, `workflows.deploy-prd`, `workflows.destroy-development`, `workflows.destroy-environment`, `workflows.dependabot-automerge`, `workflows.copilot-setup-steps`, `workflows.terraform`, `workflows.security`

**Shared libraries** — `MX.Observability.ApplicationInsights.AspNetCore` (`AddObservability()`); do not hand-roll telemetry initialisers.

## Build / test / run commands

```bash
# .NET (repo root) — warnings are errors
dotnet build src/MX.TripSideKick.sln
dotnet test  src/MX.TripSideKick.sln
dotnet test  src/MX.TripSideKick.sln --filter "FullyQualifiedName~HostRoutingTests"   # single class
dotnet test  src/MX.TripSideKick.sln --filter "FullyQualifiedName!~IntegrationTests"  # CI's filter

# Format gate — run from src/, exactly as CI does
cd src && dotnet format "." --verify-no-changes

# Client (src/MX.TripSideKick.Web/ClientApp)
npm ci && npm run build && npm run test

# Terraform (terraform/) — offline validation only; plan/apply needs CI credentials
terraform fmt -check -recursive
terraform init -backend=false
terraform validate

# Local run
dotnet run --project src/MX.TripSideKick.Web    # https://localhost:7207 = app, http://127.0.0.1:5207 = site
```

`.gitattributes` sets `* text=auto eol=lf` and `.editorconfig` sets `end_of_line = lf`. **Write LF line endings** — `dotnet format --verify-no-changes` fails on CRLF, and that is the most common way a change breaks CI from a Windows machine.

## Do NOT

- ❌ Do not `git commit`, `git push`, force-push, rebase, reset --hard, or create/delete branches. Work on the assigned branch.
- ❌ Do not introduce client secrets, connection strings, or hard-coded subscription IDs / GUIDs. Auth is OIDC + managed identity only.
- ❌ Do not bypass `terraform fmt`, `dotnet format`, test runs, or other validation gates.
- ❌ Do not change resource naming/tagging conventions (see `standards.azure-naming.instructions.md` / `standards.azure-tagging.instructions.md`).
- ❌ Do not pull context from sibling workspace folders — only what's inside this repo and `./.github-copilot/`.
- ❌ Do not assume tools/SDKs are installed beyond what `copilot-setup-steps.yml` provisions.
- ❌ Do not add an Azure DNS zone or `DNS Zone Contributor` role. DNS is **Cloudflare**; there is no Azure DNS for this workload.
- ❌ Do not create an App Service plan or a Log Analytics workspace — both come from platform remote state.
- ❌ Do not implement authentication opportunistically. Identity is a separate slice that needs Graph permissions this workload does not yet hold; leave the `IDENTITY STUB` / `TODO (identity slice)` markers intact unless that is the assigned task.
- ❌ Do not add MediatR, CQRS buses, or a separate API/SPA deployment. One App Service, two host surfaces, direct application services.
- ❌ Do not add `/health` or `/healthz` aliases. Exactly `/api/health/live` and `/api/health/ready`.
- ❌ Do not put `IntegrationTests` in a test's fully-qualified name unless the test is genuinely expensive — CI filters that string out.
- ❌ Do not commit `src/MX.TripSideKick.Web/wwwroot/` — it is generated Vite output and is git-ignored. Brochure static assets belong in `src/MX.TripSideKick.Web/SiteAssets/`, not in `ClientApp/public/`.
- ❌ Do not log or trace PII: no trip content, document contents, booking references, email addresses or display names.

## Opening the PR

You MUST use `.github/PULL_REQUEST_TEMPLATE.md` as your PR body — do **not** write a freeform body. The org template is inherited from `frasermolyneux/.github` and GitHub pre-populates it when you open the PR. Concretely:

1. Fill `## Summary` (one line) and `Closes #<issue>`.
2. Tick the relevant `## Type of change` box.
3. Paste the **actual command output** from your Build, Tests, and Format check runs into `## Validation evidence`. Show the real summary line, not "tests passed".
4. Fill `## Risk and rollout` — blast radius, auto-deploy?, manual steps post-merge, rollback plan.
5. Tick **every** box in `## Agent attestation`.
6. Delete `## Consumer impact` only if no published contract (Abstractions / Client NuGet / Service Bus DTO / Terraform output) changed.

Complete the `## Agent attestation` section before requesting review; reviewers use it as a readiness checklist.

## Pre-PR checks (run before you open the PR)

- [ ] `dotnet build src/MX.TripSideKick.sln` succeeds with zero warnings
- [ ] `dotnet test src/MX.TripSideKick.sln` passes
- [ ] `cd src && dotnet format "." --verify-no-changes` is clean
- [ ] `npm ci && npm run build && npm run test` passes in `src/MX.TripSideKick.Web/ClientApp`
- [ ] `terraform fmt -check -recursive`, `terraform init -backend=false`, `terraform validate` all pass
- [ ] No new secrets / GUIDs / connection strings introduced
- [ ] Changes match the conventions referenced in Required reading
- [ ] `code-review` sub-agent run; High/Medium findings resolved or justified in the PR body

## Escalation

If you hit any of the conditions below, **open the PR as draft** and **apply the `needs-decision` label** instead of pushing forward to ready-for-review. Post a comment on the originating issue summarising what's blocking you and what decision is needed.

This protects against the agent silently expanding scope, bypassing a contract change, or merging a half-resolved review finding.

Repo-specific conditions:

- A required-reading file is missing from the runner (`./.github-copilot/` not checked out).
- The work needs Entra/Graph permissions, a Cloudflare token scope, or an Azure role the workload identity does not hold.
- The change would alter the host-routing contract (which hostname serves which surface) or add a third surface.
- The change would add recurring Azure cost — a new App Service plan, a higher SQL tier, Private Link/VNet, or a second region.
- A `code-review` High finding cannot be resolved within the assigned scope.
- Terraform validates locally but the change cannot be proven safe without a real `plan` (no credentials locally — a `deploy-dev` labelled PR is required).
- Acceptance criteria are ambiguous about whether identity is in scope.
