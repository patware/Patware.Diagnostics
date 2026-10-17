# Architecture Decision Records

Architecture Decision Records (ADRs) capture decisions that materially affect the project's long-term public contract, architecture, dependencies, compatibility, or operational behavior.

## When to create an ADR

Create an ADR for decisions such as:

- final product and NuGet package naming;
- supported target frameworks;
- event catalog source of truth;
- package decomposition;
- registration API shape;
- use of source-generated logging;
- OpenTelemetry-specific behavior;
- event ID allocation/reservation strategy;
- controversial automatic instrumentation boundaries.

Routine implementation choices do not require ADRs.

## Statuses

Use `Proposed`, `Accepted`, `Superseded`, or `Rejected`.

## Naming

```text
0001-product-name.md
0002-supported-dotnet-versions.md
0003-event-catalog-source-of-truth.md
```

## Template

```markdown
# ADR NNNN: Decision title

## Status

Proposed

## Context

What problem or constraint requires a decision?

## Decision

What is being decided?

## Consequences

What becomes easier, harder, constrained, or intentionally unsupported?

## Alternatives considered

What credible alternatives were evaluated and why were they not selected?
```

ADRs should explain the reasoning available at decision time. When a decision changes, preserve history and supersede the earlier ADR rather than rewriting it as though the original decision never existed.
