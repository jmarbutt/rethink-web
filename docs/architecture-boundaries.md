# Architecture Boundaries

This diagram shows RethinkWeb as an app-manifest runtime: app code defines the business contract, `RethinkWeb.Core` owns the runtime model and extension points, and adapter packages expose that model through web, MCP, rendering, and storage surfaces.

```mermaid
flowchart TB
    subgraph App["Developer App / Sample Host"]
        Entities["Entities + Field Attributes"]
        Operations["Queries, Mutations, Actions"]
        Subscribers["Event Subscribers"]
        CustomPages["Custom Pages / Escape Hatches"]
        Bootstrap["Program.cs registration"]
    end

    subgraph Core["RethinkWeb.Core Boundary"]
        Registries["Metadata Registries<br/>Entity / Query / Mutation / Action"]
        Manifest["Permission-Scoped Manifest Builder"]
        Dispatchers["Operation Dispatchers<br/>Query / Mutation / Action"]
        Events["IEventBus<br/>default: in-proc"]
        StoreContract["IEntityStore&lt;T&gt;"]
        CrossCutting["Auth, Tenancy, Clock, Id Generator"]
    end

    subgraph Surfaces["Adapter / Surface Boundaries"]
        Http["RethinkWeb.Http.MinimalApi<br/>routes, form binding, HTMX fragments"]
        Razor["RethinkWeb.Render.Razor<br/>IEntityRenderer implementation"]
        Mcp["RethinkWeb.Mcp<br/>tools/list + tools/call"]
        EfCore["RethinkWeb.Store.EfCore<br/>EF Core store adapter"]
    end

    subgraph Runtime["Runtime Consumers"]
        Browser["Browser / HTMX UI"]
        Agent["MCP Client / Agent"]
        Docs["Docs, Inspector, Future Renderers"]
        Db["SQLite / EF Database"]
    end

    Entities --> Bootstrap
    Operations --> Bootstrap
    Subscribers --> Bootstrap
    Bootstrap --> Registries
    Bootstrap --> StoreContract
    Bootstrap --> CrossCutting

    Registries --> Manifest
    Registries --> Dispatchers
    Registries --> Http
    Registries --> Mcp
    Registries --> Razor

    Http --> Browser
    Http --> Manifest
    Http --> Dispatchers
    Http --> Razor
    Http --> StoreContract

    Razor --> Browser
    Mcp --> Agent
    Mcp --> Dispatchers
    Mcp --> Manifest
    Manifest --> Docs

    Dispatchers --> StoreContract
    Dispatchers --> Events
    StoreContract --> Events
    Events --> Subscribers
    Subscribers --> StoreContract

    StoreContract --> EfCore
    EfCore --> Db

    CustomPages --> Http
    CustomPages --> StoreContract
    CustomPages --> Events
```

## Boundary Rules

`RethinkWeb.Core` is the center of gravity. It owns metadata, registries, manifest generation, operation dispatch, event contracts, tenancy/auth abstractions, and the `IEntityStore<T>` contract. It should not know about ASP.NET endpoints, Razor rendering, MCP transport, or EF Core.

Surface packages are opt-in adapters. `Http.MinimalApi`, `Render.Razor`, `Mcp`, and `Store.EfCore` each depend on `Core` plus their external framework. They should translate the core app contract into a surface, not smuggle surface assumptions back into core.

The manifest is the public contract. Browser UI, MCP clients, docs, inspectors, and future renderers should consume runtime metadata or the manifest instead of scraping generated HTML.

Storage is deliberately narrow. App code talks to `IEntityStore<T>` unless it needs an escape hatch, and adapter packages can replace the default in-memory store with EF Core or future stores without changing the operation model.

## Save And Operation Flow

```mermaid
sequenceDiagram
    participant User as Browser or MCP Client
    participant Surface as HTTP or MCP Surface
    participant Dispatcher as Core Dispatcher
    participant Store as IEntityStore configured chain
    participant Decorators as Tenant / publishing decorators
    participant InnerStore as In-memory or EF Core store
    participant Bus as IEventBus
    participant Subscriber as App Subscriber

    User->>Surface: Submit form or call tool
    Surface->>Dispatcher: Invoke query, mutation, or action
    Dispatcher->>Store: Load entity / list data
    Dispatcher->>Store: Save changed entity
    Store->>Decorators: Apply tenant scope and publish behavior
    Decorators->>InnerStore: Persist entity
    Decorators->>Bus: Publish EntitySaved event
    Bus->>Subscriber: Handle side effects or computed fields
    Subscriber->>Store: Optional follow-up save
    Dispatcher-->>Surface: Typed result
    Surface-->>User: HTML fragment, JSON, or MCP tool text
```

The slightly sharp edge: the same action or mutation can be reached through generated HTTP endpoints and MCP tools. That only works because both surfaces go through the same registries, dispatchers, store contract, and event pipeline. If a new surface bypasses those, it creates drift.
