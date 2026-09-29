---
summary: How an unreachable or slow server fails, and what `HttpClient` does and does not time out.
read-when: Before setting a timeout or classifying a network failure.
---

# RomM: connectivity

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-353. An absent host on the local subnet takes 21 s to fail, and nothing caps it by default

Verified: Windows 11 26200, .NET 10, 2026-08-09 and 2026-09-29. How: raw TCP connects and `HttpClient` requests to an unused address on the LAN, a closed port on a live host, and a name that does not resolve.
A connect to an address in the subnet with no host behind it fails at 21.02 to 21.09 s, every
time, with `SocketException(TimedOut)`, and a default `HttpClient` waits the same 21 s. That is
the common offline case: the RomM box is off but its address is still valid. A closed port on a
live host is refused at 2.04 to 2.05 s, and an unresolvable name fails in under 50 ms.
`SocketsHttpHandler.ConnectTimeout` caps the wait to within 20 ms of its value, so every
`RomM.Client` handler sets it: 2 s interactive, 10 s for background work. `HttpClient.Timeout`
cannot be that lever, because it also bounds a reachable server's slow answer.

## RB-403. A timeout and a user cancellation differ only in the inner exception

Verified: Windows 11 26200, .NET 10, 2026-08-09 and 2026-09-29. How: read the exception chain of each way a request to the absent host of RB-353 ends.
All three throw `TaskCanceledException`. A `ConnectTimeout` wraps a bare `TimeoutException`; an
`HttpClient.Timeout` wraps a `TimeoutException` that wraps a further `TaskCanceledException`; a
cancelled token wraps only a `TaskCanceledException`. A bare `catch (TaskCanceledException)`
reports every offline server as the user's doing, so `RomMTransportErrors.Classify` checks the
token first, then reads that chain to report `ConnectTimeout` or `RequestTimeout`.

## RB-224. The pairing screen's contact call gives up at 2.0 s on an unreachable server

Verified: Windows 11 26200, .NET 10, 2026-08-25 and 2026-09-29. How: three `ServerProbes.TryContactAsync` calls to `192.0.2.1:8080`, and today three more to an absent LAN address.
Each returned no contact after 2.00 to 2.05 s, so the 2 s budget holds end to end, not only for
the handler in isolation. A test of this needs an address that routes nowhere, such as
TEST-NET-1 from RFC 5737: a made-up hostname fails at DNS in milliseconds and never reaches the
connect timeout.

## RB-267. `HttpClient.Timeout` stops covering a call once its headers arrive under `ResponseHeadersRead`

Verified: Windows 11 26200, .NET 10, 2026-09-21 and 2026-09-29. How: a loopback socket sent headers and 100 of a declared 1,000 bytes, then held the connection, against `Timeout = 2s`.
`ResponseContentRead` threw at 2.0 s. `ResponseHeadersRead` was still reading at 8 s with 100
bytes in, because `HttpClient` disposes its timeout when `SendAsync` returns. So a JSON call
goes through `SendAsync` with `ResponseContentRead`, and a streamed body is bounded by the
per-read watchdog, `RomMClientOptions.StallTimeout`.
