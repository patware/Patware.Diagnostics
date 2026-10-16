# Patware.Diagnostics

Less boilerplate. Better diagnostics. More time building features.

**Your application has a story to tell. Let it tell that story automatically.**

Patware.Diagnostics is a collection of .NET libraries that simplify application observability through standardized events, structured logging, metrics, and automatic instrumentation.

Developers shouldn't have to reinvent logging conventions, memorize event IDs, or write repetitive instrumentation code. And IT operations shouldn't have to decipher inconsistent log messages to understand what an application is doing.

**One registration. Meaningful diagnostics. Minimal effort.**

```csharp
builder.AddPatwareDiagnostics();
```

The goal is simple:

- **For developers:** Spend less time writing diagnostic plumbing and more time building features.
- **For IT operations:** Get consistent, predictable, machine-readable events and metrics across applications.
- **For everyone:** Know what happened, when it happened, and what it means.

Built on the .NET logging and diagnostics abstractions, Patware.Diagnostics complements your existing observability stack rather than replacing it.

*Instrument once. Understand everywhere.*

---

<!-- TODO: Add packages, getting started, documentation, examples, and contribution guidelines. -->

## Install

TODO: Install instructions

## Start with two calls

TODO: How to use instructions

> [NOTE!] One important detail: builder.AddPatwareDiagnostics() is presented as the intended developer experience, not an API that already exists (yet).

```csharp

var builder = WebApplication.CreateBuilder(args);

builder.AddPatwareDiagnostics();

var app = builder.Build();

app.Run();
```

The libraries currently target .NET 10.

## Try the various sample apps

TODO: List the various sample apps, what they demonstrate and how to run them.

## Libraries

TODO: List of libraries and their responsibility.

| Library | Responsibility |
| --- | --- |

## Current boundaries and direction

TODO: document the limitations of the current version.

Further work includes:

TODO: bullet list of unimplemented features.

See [capability boundaries](docs/design/CAPABILITY-BOUNDARIES.md) for the
distinction between implemented behavior and planned work.

## Documentation

- [Documentation home](docs/index.md)
- [Definitions](docs/architecture/DIAGNOSTICS-DEFINITIONS.md)
- [Architecture](docs/architecture/ARCHITECTURE.md)
- [Testing](tests/README.md)
- [Contributing](CONTRIBUTING.md)
- [License](LICENSE)
