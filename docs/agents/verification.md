# Verification

```sh
dotnet tool restore && dotnet csharpier check . && dotnet test --project tests/OpenSettlers.Tests && dotnet build OpenSettlers.csproj
```

```sh
dotnet test --project tests/OpenSettlers.Tests
```

The full command restores the local tools, checks formatting (CSharpier names each unformatted file;
`dotnet csharpier format .` fixes them), runs the unit tests, and builds the project.

The focused command runs the unit tests alone. They build their fixtures in code and need neither an
install nor Godot.

No timing command: the test runner's per-test durations are not wired in.

## Test paths

- `tests/`
- `*.Tests/`
