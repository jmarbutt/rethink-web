# App Manifest Runtime Thesis

RethinkWeb is an **App Manifest Runtime**.

Developers author a durable business/app layer in C#. The framework turns that layer into a permission-scoped manifest that can be consumed by generated web UI, custom web UI, HTTP endpoints, MCP tools, docs, inspectors, AI agents, and future renderers.

The goal is not to make another CRUD generator. CRUD generation is useful, but it is a weak center of gravity. The stronger bet is that most internal and operational applications should have one explicit application contract that owns the entities, operations, views, events, and lifecycle facts, instead of scattering that knowledge across a backend API, React forms, mobile clients, docs, admin screens, and LLM prompts.

## The Bet

Most app stacks accidentally become distributed systems inside one product:

- The database has the shape.
- The API has a second shape.
- The web UI has a third shape.
- Mobile or MCP has another shape.
- Audit logs, docs, tests, and LLM context each reconstruct partial truth.

RethinkWeb intentionally pushes the other direction. It keeps the business/app layer tightly coupled and explicit, then lets many surfaces read from that same contract.

```text
Developer-authored app layer
  Entity
  View Profile
  Query
  Mutation
  Action
  Event
  Lifecycle Fact
        |
        v
Permission-scoped manifest
        |
        +--> Generated web UI
        +--> Custom web UI
        +--> HTTP operations
        +--> MCP tools
        +--> Inspector
        +--> Docs
        +--> Agent context
        +--> Future renderers
```

The manifest is not a convenience export. It is the public contract.

## Core Model

**Entity** describes the durable business object: identity, semantic fields, validation hints, permissions, and storage participation. Entity metadata should not become the dumping ground for every layout and widget decision.

**View Profile** is the planned presentation contract above entities. A profile describes how an entity or operation should appear in a context: grid, detail, edit, card, lookup, operation form, dashboard fragment, or custom renderer hint. This keeps "Title is required text" separate from "show Title first in the compact work-item card."

**Query** is a typed, permission-scoped read capability. It is how renderers, MCP tools, and agents ask safe questions without exposing SQL, EF, or arbitrary data access.

**Mutation** is a typed, permission-scoped state change. It is the long-term operation primitive for changes that load an entity, validate input, update state, publish events, and record lifecycle facts.

**Action** is user-facing operation language and the current compatibility path. A button labeled "Mark Complete" is an action in the UI, but the long-term application contract should treat it as a mutation.

**Event** is a fact emitted after something changes. Events let derived state, integrations, notifications, and future workflows react without hiding behavior in controller code.

**Lifecycle Fact** is an append-only record of what the framework observed: actor, tenant, correlation id, operation name, entity id, timestamps, status, and compact summaries. The first version is not event sourcing and not full before/after snapshots. It is enough to answer "what happened and why?" from the same runtime.

**Manifest** is the permission-scoped contract that exposes the app model to renderers, HTTP, MCP, inspectors, docs, and agents.

## Why This Matters For Agentic Development

AI agents are bad at extending apps when they have to reverse-engineer business intent from disconnected controllers, DTOs, React components, migration files, and stale docs. That is not a model problem. That is an architecture problem wearing a fake nose.

RethinkWeb should make the intended extension path obvious:

- Read the manifest to understand entities, views, operations, permissions, and lifecycle.
- Read the local app code to find the corresponding C# types.
- Add or change one business primitive.
- Let the framework expose it consistently through web, HTTP, MCP, docs, and tests.

That is the repeatable concept: one durable app contract, many surfaces, explicit history.

## Boundaries

RethinkWeb is not trying to be a no-code builder, a visual workflow designer, a generic admin UI replacement, or a distributed microservice architecture. It is for developers who want to build operational software with a hard application layer and fewer places for behavior to drift.

Web UI matters, but it is one consumer. MCP matters, but it is one consumer. REST-ish HTTP matters, but it is one consumer. The app manifest is the center.
