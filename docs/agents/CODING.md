# Coding Guidelines (LLM Reference)

Target audience: LLMs. The general rules come first, then the C# rules.

## General

### Immutable data / behavioral services

Favor functional-ish programming: split code into immutable DTOs that only contain data and separate behavioral types (e.g. services) where possible. Properties, fields and parameters should also be readonly, get/init where possible. For collections favour IReadOnlyCollection<T> or Immutable*<T> where possible.

### No Over-engineering

Favour simple and concise solutions. Don't add complexity where not needed. Honor SOLID, KISS and YAGNI principles.

### Composition over inheritance

Prefer composing small, focused types over deep inheritance hierarchies.

### Blank lines

**Primary rule: group by logical context.** Statements that belong to the same step sit together. A blank line signals a context switch.

**Sync↔async boundary is always a context switch.** Every transition from sync to async (or back) gets a blank line.

**Log statements are always their own context group.** A log call always gets a blank line above and below it.

**A statement after a block starts a new context.** A statement that follows a closing brace (the end of an `if`, a loop, a `try`, or a nested block) gets a blank line above it.

### Jump statements

Always put a blank line before a jump statement **unless it is the sole statement in its block**.

```
// sole statement — no blank line, no braces needed
if (condition)
    return;

// preceding code — blank line before jump
if (condition) {
    log();

    return;
}
```

### Braces / control flow

Always use braces around loops and conditional branching. The one exception is a branch whose sole statement is a jump (`return`, `break`, `continue`, `throw`): it may go without braces. CSharpier puts that statement on its own line.

### Document

Document public and protected/abstract/overridable facing types and members using appropriate code documentation. Be specific and concise but also mention side-effects, possible errors/exceptions and possibly unexpected behavior.

### Godot `.uid` files

A new script or shader in a Godot-scanned folder (`src/Assets/`, `src/Formats/`, `src/Launch/`, `src/MapView/`, `src/Viewer/`) gets a `.uid` file from the Godot editor. Generate it with `mise exec -- <godot> --headless --import --path .`, where `<godot>` is the `GODOT` name in `mise.toml` for the platform, and commit it with the source. A new folder of C# sources outside the Godot assembly (`<Compile Remove>` in `OpenSettlers.csproj`) gets a `.gdignore`.

## C# Style

### What the build enforces

The root `.editorconfig` turns these rules into build errors. Each has its own `dotnet_diagnostic.<ID>.severity = warning` line, and `Directory.Build.props` sets `EnforceCodeStyleInBuild` with `TreatWarningsAsErrors`. Every build of every project fails on a violation and names the file, line, and rule ID.

| Rule | ID |
|------|----|
| Access modifiers: no default modifier | IDE0040 |
| `var` wherever it gives the same type | IDE0007 |
| A private field assigned only in its declaration or constructor is `readonly` | IDE0044 |

These rules stay with review: records and their positional-record rule (`PositionalRecordTests` checks the latter in the unit tests), member order, blank lines, and composition over inheritance. The `.editorconfig` sets no formatting rule: CSharpier owns formatting.

### Member ordering

Order by visibility first (`public` → `protected` → `private`), then within each group:

1. Constants / static fields
2. Properties / instance fields
3. Static methods
4. Instance methods

### Access modifiers

Omit every access modifier that is the default where it stands: `private` on every member and nested type (fields, methods, properties, constructors, events, nested classes and records), and `internal` on every top-level type, one inside a block-scoped namespace included. These modifiers are not the default and stay: `private protected`, an accessor's own modifier (`{ get; private set; }`), `file`, `internal` on a member, and `internal` on a nested type. Both parts of a partial type follow the rule together. A partial method with a return type, such as a `[GeneratedRegex]` method, keeps its `private`, because the compiler requires an explicit modifier there.

The build enforces the rule. `.editorconfig` sets IDE0040 to `omit_if_default` at warning severity. `Directory.Build.props` sets `EnforceCodeStyleInBuild` with `TreatWarningsAsErrors`, so a default modifier fails the build. `CodeStyleBuildTests` fails when either file drops one of these settings. It also fails when an `.editorconfig` section or an MSBuild file sets either setting to another value.

### var

Use `var` wherever it gives the same type. Write the type only where `var` would give another type or none, as with a target-typed expression: `IReadOnlyCollection<string> names = ["alice", "bob"]`, `int? count = null`. IDE0007 enforces the rule.

### Naming

| Thing | Convention | Example |
|-------|-----------|---------|
| Classes / interfaces | PascalCase | `Archive`, `AssetKind` |
| Interface prefix | `I` | `IComparer<T>` |
| Private fields | `_camelCase` | `_entries` |
| Records | PascalCase | `TextEntry`, `Rgb` |
| Constants | PascalCase | `PaletteKind.KindName`, `Palette.ColorCount` |

### Modern C# features

Use records, primary constructors, collection expressions, and other C# 12 features. The root project and the Domain, Application, and Adapters projects target `net8.0` and set no `<LangVersion>`, so C# 12 is their language version. The generator project is the exception (see Records). Newer features, such as `field` or extension members, do not compile.

### Classes

Always use primary constructors with no class fields, if possible.

### Records

Records carry data through `required` init-only properties, never positional (primary-constructor) parameters. Each property is its own member and carries its description in its own `<summary>`, and a record's doc comment holds no `<param>`. A property of a public record always has a description. Construct with an object initializer, and read a record through its properties or a property pattern, never through deconstruction or a positional pattern.

```csharp
// Yes
public sealed record Alarm
{
    /// <summary>The job the alarm is about.</summary>
    public required string JobName { get; init; }

    /// <summary>Whether the alarm is raised or cleared.</summary>
    public required AlarmState State { get; init; }
}

var alarm = new Alarm { JobName = "job-a", State = AlarmState.Raised };

// No — positional record
public sealed record Alarm(string JobName, AlarmState State);
```

A property that carries a default stays a non-`required` init property with an initializer (`public uint Flags { get; init; } = 0;`). A `readonly record struct` whose defaults are all zero leaves the initializers out, because a struct with field initializers needs an explicit constructor. A record that refuses bad input checks it in the `init` accessor through a backing field. A copy made with `with` is then checked too (`WorkerRequest.Job`). Primary constructors remain the norm for classes; the rule above is records-only.

The properties stand in the order of the record's data, a base record's properties first: the order sets the record's equality, hash code, and `ToString` text. Another order changes the hash code, and with it the iteration order of a hash set that holds the record.

Two exemptions stay positional:

- **The job generator project** (`src/Generators`). It targets netstandard2.0, because Roslyn loads a generator into the compiler, and netstandard2.0 lacks the attributes that `required` members need.
- **`Knight` and `UnportedJob`.** The generated `Job.Of` builds both from a number through their constructors, and `UnportedJob.Code` overrides the abstract get-only `Job.Code`, which an init property cannot override.

`PositionalRecordTests` enforces the rule: it parses every C# file under `src/` and `tests/` and fails on a positional record outside the two exemptions. A record inside a string literal, such as a generator test's fixture source, is no declaration and is not checked.

