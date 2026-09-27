---
summary: How an unreachable or slow server fails, and what `HttpClient` does and does not time out.
read-when: Before setting a timeout or classifying a network failure.
---

# RomM: connectivity

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-11. That timeout is 21s and cannot be a UI budget

Plan says: The unreachable-host timeout "becomes the budget" for UI checks (L383)

Measurement says: That timeout is 21s and cannot be a UI budget. `ConnectTimeout` must be set explicitly; 2s recommended

## RB-224. 2046, 2002 and 2004 ms

Question: (not addressed) what an unreachable server costs through the interface, rather than through a handler in isolation

Measured: **2046, 2002 and 2004 ms**, driven three times against `192.0.2.1:8080` through `ServerProbes.TryContactAsync`, which is the call the pairing screen makes. So M0 experiment 6's 2 s budget holds end to end and is not just a property of the handler. The address matters: it is TEST-NET-1 from RFC 5737 and routes nowhere, where a made-up **hostname** fails at DNS in milliseconds and never exercises the connect timeout at all

## RB-267. Whether `HttpClient.Timeout` covers a body read under `ResponseHeadersRead` (#198, M0 probe 6b)

Question: Whether `HttpClient.Timeout` covers a body read under `ResponseHeadersRead` (#198, **M0 probe 6b**)

Measured: **No.** Against a loopback socket that sent headers and 100 of a declared 1,000 bytes, then held the connection open: with `Timeout = 2s`, `ResponseContentRead` threw `TaskCanceledException` at 2.0 s, while `ResponseHeadersRead` was still reading at 8 s with 100 bytes in. `HttpClient` disposes its timeout source when `SendAsync` returns. Every `RomM.Client` call used `ResponseHeadersRead`, so save, state and screenshot downloads, which had no stall watchdog, hung until cancelled, and so could any JSON call whose body stalled. JSON calls now buffer the body inside the timeout. Probe 6b's reading of the timeout as bounding the body was true of the call it measured, not of this client

## RB-353. Probe 6b: reachability timeout (complete)

**This is the most consequential number measured so far, and it invalidates the plan's
assumption that the OS timeout can serve as the UI budget.**

Raw TCP connect, no client-side timeout, 5 repetitions each
(`tools/m0-probes/probe6-reachability.ps1`):

| Case                                           | First    | Median       | Max      | Error                       |
| ---------------------------------------------- | -------- | ------------ | -------- | --------------------------- |
| **Host absent, address inside the LAN subnet** | 21092 ms | **21049 ms** | 21093 ms | `TimedOut` (10060)          |
| Host up, port closed                           | 2039 ms  | 2040 ms      | 2041 ms  | `ConnectionRefused` (10061) |
| Off-subnet blackhole (TEST-NET-1)              | 5613 ms  | 5668 ms      | 11631 ms | mixed                       |
| Hostname does not resolve                      | 45 ms    | 0.8 ms       | 45 ms    | `HostNotFound` (11001)      |

The case that matters is the first one, and it is the common one: the RomM box is powered
off or unplugged, but its address is still a valid address on the user's subnet. **21
seconds, every time.** It does not improve across repetitions, so there is no negative-ARP
caching to lean on. A UI that calls a reachability check on this path without its own
timeout appears frozen for 21 seconds.

Through `HttpClient`, which is what actually ships
(`tools/m0-probes/probe6-httpclient.cs`):

| Configuration                           | Elapsed  | Exception chain                                                      |
| --------------------------------------- | -------- | -------------------------------------------------------------------- |
| Default handler                         | 21113 ms | `HttpRequestException -> SocketException(TimedOut)`                  |
| `HttpClient.Timeout = 5s`               | 5005 ms  | `TaskCanceledException -> TimeoutException -> TaskCanceledException` |
| `ConnectTimeout = 1s`                   | 1021 ms  | `TaskCanceledException -> TimeoutException`                          |
| `ConnectTimeout = 2s`                   | 2014 ms  | `TaskCanceledException -> TimeoutException`                          |
| `ConnectTimeout = 3s`                   | 3008 ms  | `TaskCanceledException -> TimeoutException`                          |
| `ConnectTimeout = 3s` + `Timeout = 10s` | 3008 ms  | `TaskCanceledException -> TimeoutException`                          |
| User cancels via token after 1s         | 1006 ms  | `TaskCanceledException -> TaskCanceledException`                     |

Three things follow, and all three are binding on `RomM.Client`:

1. **A default `HttpClient` inherits the full 21 second stall.** `SocketsHttpHandler.ConnectTimeout`
   must be set explicitly on every client instance. It caps the wait precisely (within 20 ms
   of the requested value at every value tested), so the mitigation works, but nothing
   applies it by default.
2. **`HttpClient.Timeout` is the wrong lever.** It bounds the whole request including the
   response body, so setting it low enough to make reachability feel responsive would abort
   legitimate large downloads. **That holds only under the default `ResponseContentRead`**;
   under `ResponseHeadersRead`, which is how `RomM.Client` sends a transfer, it stops at the
   headers (RB-267). `ConnectTimeout` bounds only the TCP handshake, which is
   exactly the thing that hangs. Set both, for different reasons.
3. **An unreachable host and a user cancellation are the same exception type.** Both surface
   as `TaskCanceledException`. They are distinguishable only by the inner exception
   (`TimeoutException` for a timeout, absent for a real cancellation) or by checking the
   token's `IsCancellationRequested`. Code that does `catch (TaskCanceledException)` and
   reports "cancelled" will silently mislabel every offline server as a user action.

**Recommended budget: `ConnectTimeout = 2s` for interactive reachability checks.** Two
seconds is above the LAN RTT by orders of magnitude, so it will not produce false negatives
on a healthy network, and it keeps a failed check inside the window where a spinner still
reads as responsive. Sync operations that are already known to be long-running can afford a
longer connect timeout; the UI probe cannot.
