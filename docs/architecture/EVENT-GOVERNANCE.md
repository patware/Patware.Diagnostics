# Event Governance

## Why governance matters

A standard event catalog creates value only when event semantics remain predictable. Adding an event is therefore a contract-design decision, not simply adding another logging method.

## Admission criteria

A proposed standard event should be common across multiple applications or represent a framework/application operational concept with durable meaning. It should provide monitoring or troubleshooting value beyond ordinary diagnostic prose.

Do not add a standard event solely because one application wants a convenient message.

## Proposal requirements

Every new event proposal should specify:

1. problem/use case;
2. exact trigger semantics;
3. proposed category/range;
4. proposed ID and symbolic name;
5. default log level;
6. structured message template;
7. required and optional properties;
8. privacy/security considerations;
9. automatic versus explicit emission;
10. related/recovery/escalation events;
11. expected operational response, if any;
12. tests proving both positive and negative trigger conditions.

## Review questions

- Can the condition be determined reliably?
- Is this already represented by .NET/OpenTelemetry telemetry?
- Is a log event the correct signal, rather than a metric or trace?
- Is the event broadly reusable?
- Could it cause alert fatigue or excessive volume?
- Does it expose sensitive or high-cardinality data?
- Is the chosen level appropriate?
- Is the event materially distinct from an existing event?

## Stability rules

Once stable and published, an event ID must never be reused for another meaning. Event names should remain stable. Required properties should not be removed or redefined incompatibly. Message prose may be clarified only when structured semantics remain unchanged.

## Deprecation

Deprecated events remain documented with their replacement and migration guidance. Numeric IDs are retired permanently rather than recycled.

## Reserved ranges

Do not reserve large ranges for hypothetical future categories without need. Any reservation scheme should be documented and approved through an ADR.

## Catalog publication

The project should eventually publish a generated catalog in human-readable Markdown and, if useful, a machine-readable form such as JSON. Ideally both are generated from one authoritative definition to prevent drift.
