# Verification

```sh
dotnet tool restore && dotnet csharpier check . && dotnet test --project tests/OpenSettlers.Tests && dotnet build OpenSettlers.csproj
```

```sh
dotnet test --project tests/OpenSettlers.Tests
```

The full command first checks formatting. It restores the local tools and runs CSharpier's check.
The check exits non-zero and names each C# or project file that is not formatted.
`dotnet csharpier format .` fixes them. The first `dotnet tool restore` on a machine needs NuGet
access. The full command then runs the unit tests and builds the project.

The focused command runs the unit tests alone. They build their fixtures in code and need neither an
install nor Godot.

No timing command: the test runner's per-test durations are not wired in.

## Test paths

- `tests/`
- `test/`
- `*.Tests/`
- `*.Test/`
- `*.test.*`
- `*_test.*`
