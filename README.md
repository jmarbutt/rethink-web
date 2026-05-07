# RethinkWeb

> A .NET 9 thought-exercise framework. Server-rendered HTML over HTTP, attribute-driven entity metadata, unified actions across HTTP / MCP / UI. **Personal use only — not shipping.**

## The Bet

**HTMX + server-rendered metadata can produce a UX that feels good enough that I don't miss React.**

Everything in this repo exists to test that bet with as little code as possible before committing weeks to a full framework. The full design doc lives at:
`~/.claude/plans/ok-so-i-want-precious-bird.md`.

## Layout

```
src/
  RethinkWeb.Core/             Abstractions, attributes, in-proc defaults, registries.
                               ZERO non-MS dependencies. Everything else is opt-in.
  RethinkWeb.Render.Razor/     Razor Components + HtmlRenderer for server-side rendering.
  RethinkWeb.Http.MinimalApi/  Maps registered actions/views to ASP.NET Core endpoints.
  RethinkWeb.Mcp/              MCP HTTP endpoint exposing the action registry as tools.
  RethinkWeb.Sample.Donor/     The prototype app — Donor entity, UpdateAddress action,
                               Amount→Deductible computed field via event subscriber.
tests/
  RethinkWeb.Core.Tests/
  RethinkWeb.Render.Razor.Tests/
  RethinkWeb.Sample.Donor.Tests/
```

## Run the prototype

```bash
dotnet run --project src/RethinkWeb.Sample.Donor
```

Then open http://localhost:5000 — donor grid. Click a donor to edit. Save to see HTMX swap in the updated row. Visit `/_framework/manifest` for the JSON manifest. Hit `/mcp` with an MCP client to invoke actions.

## Run the tests

```bash
dotnet test
```

## Abandonment criterion (set before starting, per the plan)

If after 16 hours of building this prototype it isn't usable end-to-end, the framework is too ambitious as designed. Cut scope or kill it. Write down what you learned. The cost of stopping now is one weekend; the cost of stopping after building "Phase 2" is months.
