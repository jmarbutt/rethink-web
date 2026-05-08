# Agent Instructions

## Project Shape

This repository is a .NET 9 prototype for an app-manifest runtime. The current package and docs use the name `RethinkWeb`, but treat that as a working name, not a permanent brand.

The framework center is `RethinkWeb.Core`. Core owns the app contract: metadata, registries, manifest generation, dispatchers, lifecycle contracts, events, auth, tenancy, clock/id abstractions, and in-proc defaults.

Surface and storage packages are adapters. They may depend on external frameworks, but they should not push ASP.NET, Razor, MCP, EF Core, Postgres, or other provider assumptions back into Core.

## Before Coding

Read the docs that match the task:

- `docs/architecture.md` for package boundaries and dependency rules.
- `docs/architecture-boundaries.md` for the runtime flow and adapter boundaries.
- `docs/testing.md` for test patterns and deterministic defaults.
- `docs/query-mutation-plan.md` before changing queries, mutations, actions, lifecycle, or manifest behavior.
- `docs/roadmap.md` before selecting the next feature.

Use the README for product context, but prefer the docs above for implementation decisions.

## Development Rules

- Use `dotnet` commands from the repo root.
- Run `dotnet test RethinkWeb.sln` before claiming code is done.
- Write focused failing tests before implementing behavior.
- Keep changes small and directly tied to the issue or task.
- Prefer simple framework primitives over clever abstractions.
- Do not add repository/service layers, CQRS, or compatibility shims unless the task explicitly justifies them.
- Delete unused code instead of preserving dead paths.
- Keep Core provider-neutral. Provider-specific behavior belongs in adapter packages.
- Preserve deterministic defaults: use `IClock`, `IIdGenerator`, `IAuthContext`, and tenant abstractions instead of static runtime state.

## Lifecycle And Storage

Lifecycle facts are a Core concept, not logging garnish. Core owns the contracts and fact models. Durable persistence belongs in storage adapters.

The default lifecycle registration should preserve no-persistence behavior unless a durable or in-memory store is explicitly configured.

Postgres-specific lifecycle behavior should live in a Postgres adapter package. Do not add Npgsql, EF Core provider details, SQL schema assumptions, or migration mechanics to Core.

## Manifest Contract

The manifest is the public contract for renderers, HTTP, MCP, docs, inspectors, and agents. Attributes and C# interfaces are authoring tools; runtime metadata and manifest JSON are what consumers should use.

Do not create a new surface that bypasses registries, dispatchers, permission checks, tenant scope, lifecycle recording, or the configured entity store chain.

## Naming

`RethinkWeb` is a working name. If renaming comes up, favor names that describe the product thesis: a manifest-centered app runtime for operational tools, not just a web renderer or CRUD generator.

Avoid names that overfit to HTMX, Razor, MCP, Postgres, or anti-React positioning. Those are implementation surfaces, not the center of gravity.
