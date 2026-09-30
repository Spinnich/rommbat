# Pairing

The gamepad UI pairs from the couch. The console agent does the same thing without a window,
ASCII QR included, which is what a headless or scripted install uses:

```powershell
dotnet run --project src/RomMBat.Agent -- pair --root D:\retrobat-test --server https://your-romm-instance
dotnet run --project src/RomMBat.Agent -- status --root D:\retrobat-test
```

`--root` is only needed when the agent is not running from inside the tree. Add `--protect`
to encrypt the stored token with a passphrase, and `--offline` to `status` to skip the
reachability probe. A token stored with `--protect` needs `--passphrase` on a later `status`.

`status` also reads this device's play sessions back from RomM and prints the count, the last
session and the ten newest under a `Playtime` block; `--all-sessions` lists up to 50.
