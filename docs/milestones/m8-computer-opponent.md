# M8 Computer opponent: scope

Goal and done condition: [roadmap, M8](../roadmap.md#m8-computer-opponent). Marks:
[Leads](AGENTS.md#leads).

## Goal design

The computer opponent sets goals as bits and runs one routine per goal. The survey reads it this way:

- `Ai_UpdatePlayer`: known. `0x429580` calls it from the phase table of `Sim_StepPhase`. likely
- `0x4754b0`: dispatches nine actions: `0x47556e`, `0x4756ed`, `0x475950`, `0x475a7e`, `0x475c95`,
  `0x475e14`, `0x475ef6`, `0x475fd5`, `0x4760b4`. likely
- `0x475244`: the evaluator that the nine actions share. likely
- `0x47619e`: tests about 25 goal bits one by one; each test calls `0x476529`, which records the chosen
  goal. It works on arrays at player `+0x1dc` to `+0x33c`. likely
- `0x47661b`, `0x476862`, `0x4769ed`: the evaluators that the goal tests share. likely
- `0x476af1` to `0x47872c`: about 20 routines, one condition per goal bit; `0x47820f`, `0x47849d`, and
  `0x47895d` among them. likely
- `0x478f2b`: clears the scratch arrays and branches on the opponent kind at player `+0x101` through a
  table at `0x479213`. likely
- `0x479293`, `0x479403`, `0x47958c`, `0x4796f1`, `0x47978c`, `0x4798e0`, `0x479a9a`, `0x479abc`,
  `0x479ade`: map searches for `0x478f2b`. likely
- `0x44b63a`: the placement check for the computer player. likely

## Players compared

- `0x42965c`: a handler of the phase table that sums player `+0x112` over the active players, runs steps
  from byte tables at `0x42a313`, `0x42a35d`, and `0x42a3a7`, and draws from `Rng_Step`. guess
- `0x42a3f1`, `0x42a567`, `0x42a7ac`, `0x42b1ce`: compare and decay per-player values at `+0x112`,
  `+0x116`, `+0x17c` to `+0x188`, and `+0x1b8`. Opponent strength or statistics. guess

## Open questions

- What are the 25 goals? Each condition routine names one.
