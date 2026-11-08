# Trip Side Kick

[![Build and Test](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Auto-Merge](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/dependabot-automerge.yml)
[![Publish Image](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/publish-image.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/publish-image.yml)
[![Promote Image](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/promote-image.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/promote-image.yml)
[![Destroy Environment](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/pr-verify.yml)

## Documentation

* [Constrained Runtime](/docs/workload-consolidation.md) - Runtime configuration, local/CI tests and activation gates
* [Architecture Overview](/docs/architecture-overview.md) - Modular monolith layout, host-aware surfaces and the runtime topology
* [Development Workflows](/docs/development-workflows.md) - Local setup, build/test commands, branch strategy and CI/CD triggers
* [DNS and Custom Domains](/docs/dns-and-custom-domains.md) - Cloudflare records and ingress
* [Identity and Access](/docs/identity-and-access.md) - Certificate-backed BFF sign-in
* [Data and Persistence](/docs/data-and-persistence.md) - Persistence and domain invariants
* [Testing](/docs/testing.md) - Local and CI validation
* [Infrastructure and Cost](/docs/infrastructure-and-cost.md) - Retained infrastructure and cost guidance

## Overview

Trip Side Kick is a travel itinerary planner: one place for flights, stays, bookings and day-by-day
plans that you can share with the people you are travelling with. It is an ASP.NET Core
modular monolith that serves two surfaces from a single constrained container — a Razor Pages
brochure site on `tripsidekick.net` and a React + TypeScript progressive web app plus versioned
`/v1` API on `tripsidekick.app`. PostgreSQL supplies persistence; local mounts
retain Data Protection keys and documents. The target is the existing `ns346663`
platform runtime, with no hosted Development environment. Legacy Azure Terraform
is retained for coordinated retirement, not application delivery. Initial image
digests are deliberately nondeployable; host activation remains a separate review.

## Contributing

Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security

Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.
