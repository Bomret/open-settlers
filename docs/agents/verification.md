# Verification

```sh
mise run check
```

```sh
mise run test
```

The full command restores the local tools, checks formatting (CSharpier names each unformatted file;
`mise run format` fixes them), runs the unit tests, and builds the project. `mise.toml` holds the
steps.

The focused command runs the unit tests alone. They build their fixtures in code and need neither an
install nor Godot.

No timing command: the test runner's per-test durations are not wired in.

## Test paths

- `tests/`
- `*.Tests/`
