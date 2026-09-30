# Save states

Save states are pushed automatically and pulled only when asked. A state goes up when its
content changes, and no sync brings one down. `saves restore` offers saves and states in one
preview, labelling each row `save` or `state`, and writes nothing without `--apply`. It also says
per state whether a screenshot is linked.

A state it brings down is unverified twice over, and it says so: RomM publishes no hash for a
state, and a state carries no emulator version either, so one made on a different build of the
same emulator cannot be told apart from one made here.

The name a state is uploaded under carries the emulator and core, so two cores writing one
filename for one game stay two states on the server. Coming back, the ROM on disk names the
file, so the state lands where the emulator looks.
