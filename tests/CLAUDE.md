# Tests

xUnit. `RomMBat.Tests` covers Client, Core and UI; `RomMBat.Agent.Tests` covers the agent's
subcommands, in its own project so the agent's Windows manifest stays out of the main test host.

| Where                                     | What                                                            |
| ----------------------------------------- | --------------------------------------------------------------- |
| `RomMBat.Tests/Support/`                  | `TempRetroBatTree`, `StubRomMServer`, `ApprovingUser`, fixtures |
| `RomMBat.Tests/Support/GeneratedPage.cs`  | Holds a generated guide page to its source; `wiki/README.md`    |
| `RomMBat.Tests/fixtures/`                 | Captures from a real install, byte exact and excluded from lint |
| `RomMBat.Agent.Tests/Support/AgentRun.cs` | Runs a subcommand through `Program.DispatchAsync`               |

## Traps

- **Budget is CI's Test step**, where disk-bound work runs ten times slower than on a dev box.
  Profile before adding a slow test (`pre-pr-verification`, "Test cost").
- **A test that needs a live RomM calls `Assert.SkipUnless`** on its environment variables, so
  a clone with no server runs green.
- **Offline behaviour is tested through `StubRomMServer`'s unreachable mode**, which throws
  what `SocketsHttpHandler` throws on a connect timeout. Do not fake it with a cancellation.
- **A temp tree must be disposed.** `TempTreeLeakCheck` fails the run if one is left in
  `%TEMP%\rommbat-tests`.
- **Fixtures are layout, config and logs from a real install, never game content.** A test that
  needs a ROM, BIOS or save records its name, size, md5 and magic bytes and builds a stand-in.
- **Save-shape and mapping logic is not finished without a fixture.**
