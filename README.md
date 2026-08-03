# Trip Side Kick

[![Build and Test](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Auto-Merge](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/dependabot-automerge.yml)
[![Deploy Dev](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/deploy-dev.yml)
[![Deploy Prd](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/deploy-prd.yml)
[![Destroy Development](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/destroy-development.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/destroy-development.yml)
[![Destroy Environment](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/trip-side-kick/actions/workflows/pr-verify.yml)

## Documentation

* [Architecture Overview](/docs/architecture-overview.md) - Modular monolith layout, host-aware surfaces and the runtime topology
* [Development Workflows](/docs/development-workflows.md) - Local setup, build/test commands, branch strategy and CI/CD triggers
* [DNS and Custom Domains](/docs/dns-and-custom-domains.md) - Cloudflare zones, App Service bindings and the proxied-mode decision
* [Identity and Access](/docs/identity-and-access.md) - The identity model and the stubs left for the identity slice
* [Infrastructure and Cost](/docs/infrastructure-and-cost.md) - Azure resources provisioned per environment and what they cost

## Overview

Trip Side Kick is a travel itinerary planner: one place for flights, stays, bookings and day-by-day
plans that you can share with the people you are travelling with. It is an ASP.NET Core (.NET 10)
modular monolith that serves two surfaces from a single App Service deployment — a Razor Pages
brochure site on `tripsidekick.net` and a React + TypeScript progressive web app plus versioned
`/v1` API on `tripsidekick.app`. Azure infrastructure is provisioned with Terraform onto the shared
`platform-hosting` App Service plan, with DNS in Cloudflare.

## Contributing

Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security

Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.
