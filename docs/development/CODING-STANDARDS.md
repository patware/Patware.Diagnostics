# Coding standards

Diagnostics favors small, explicit implementations with clear responsibilities.
Code should be easy to understand, inexpensive to execute, and straightforward
for application developers to adopt.

## Formatting and naming

Follow the repository’s [.editorconfig](../../.editorconfig) and the surrounding
code. Treat `.editorconfig` as the source of truth for formatting rather than
maintaining a second set of detailed formatting rules here.

Current C# preferences include:

- Four-space indentation.
- File-scoped namespaces.
- Using directives outside the namespace.
- Explicit types for built-in types.
- Braces for control-flow statements.
- Readonly fields when their values do not change after construction.

Use names that describe responsibilities and behavior. Name asynchronous methods
with the `Async` suffix.

Enable nullable reference types in new projects. Configure implicit usings explicitly and consistently.

## Library responsibilities

Keep each concern in the library that owns it.

| Library | Responsibility |
| --- | --- |
| | |

Applications own telemetry providers, exporters, credentials, retention, and access policies. Libraries emit diagnostics through standard .NET abstractions.

## Public API documentation

Document publicly visible types and members with XML documentation, including
public members in test projects.

Use:

- `<summary>` to describe the purpose or behavior.
- `<param>` for each parameter.
- `<returns>` for a return value.
- `<exception>` for exceptions that form part of the documented contract.
- `<remarks>` for useful design justification, limitations, or usage details.

Use `<see cref="..."/>` and `<paramref name="..."/>` when referring to symbols and
parameters. Use `<inheritdoc/>` when inherited documentation accurately describes
the implementation.

Documentation should explain the contract without narrating the implementation.
When a design choice needs justification, record that justification in
`<remarks>` near the relevant API.

For example:

```csharp
/// <summary>
/// Lorem ipsum
/// </summary>
/// <param name="cancellationToken">
/// Lorem ipsum
/// </param>
/// <returns>
/// Lorem ipsum
/// </returns>
/// <remarks>
/// Lorem ipsum
/// </remarks>
Task<IReadOnlyList<LoremIpsum>> GetLoremIpsumAsync(
    CancellationToken cancellationToken = default);
```

Complete public XML documentation avoids missing-comment warnings such as
CS1591 when XML documentation generation is enabled.

Documentation generation and warning enforcement depend on project settings.
Do not assume every project currently generates XML documentation or treats
documentation warnings as errors.

## Validation and failures

Validate public arguments at the boundary that owns them. Use standard guard
clauses where practical, and provide exception messages that help identify the
invalid value or configuration.

Keep validation focused on the current operation. Avoid introducing additional
configuration or abstractions for hypothetical future requirements.

Preserve useful failure information. Do not catch an exception solely to return
a successful-looking result.

## Dependency injection and resource ownership

Use dependency injection for runtime dependencies. Keep registration code
concerned with service configuration and construction.

Keep constructors inexpensive.

Follow the existing service lifetimes and registration behavior unless a change
explicitly requires revisiting them.

Make resource ownership clear. Dispose resources owned by the implementation,
and let the dependency injection container manage services that it owns.

## Unit tests

Great unit tests document themselves: **test method names should document what
each test verifies.**

Use descriptive names that identify the scenario and expected behavior. Avoid
maintaining a separate prose inventory of individual test cases.

Organize test bodies with:

```csharp
// Arrange

// Act

// Assert
```

Keep tests focused and independent. Use controlled substitutes for external
dependencies when testing library behavior, and avoid timing-dependent tests
that rely on arbitrary delays.

Pass `TestContext.CancellationToken` to asynchronous operations when applicable.
Retain concise XML documentation for publicly visible test types and members.

See [Testing](TESTING.md) for the test project organization and execution
instructions.

## Reviewing a change

Keep changes focused on the intended behavior. Update public XML documentation
and relevant Markdown when a contract or usage pattern changes.

Run the checks appropriate to the affected projects. Test names should make the
verified behavior clear, while documentation should explain how and why a
consumer uses the feature.

See [Getting started](GETTING-STARTED.md) for the development workflow.
