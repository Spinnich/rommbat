---
summary: How every operation behaves when the server is unreachable, a transfer is cut, or the clock is wrong.
read-when: Before adding an operation that reaches the server, to give it a defined offline behaviour.
---

# Being offline is the normal case

Not an error path. The network is an enrichment, probed with a short-timeout
`GET /api/heartbeat`, never assumed.

| Situation                | Behaviour                                                                                                                                                                                   |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Server unreachable       | Every operation completes locally or queues. Browse falls back to the local subset and says so                                                                                              |
| Mid-download disconnect  | `.part` file survives; the next run resumes with `Range`                                                                                                                                    |
| Days offline             | A bigger negotiate payload, nothing more. The protocol is full-state reconciliation                                                                                                         |
| Failed flush             | Replay is safe: sessions dedup on truncated-to-the-second timestamps, saves on `content_hash` within a slot                                                                                 |
| Entry the server refuses | Marked `failed` with its error and no longer retried or counted by `SaveGuard`; `status` names it and `outbox drop` clears it. Offline, a 5xx, a lost token or a refused batch stay pending |
| Wrong device clock       | Sequence numbers preserve ordering; skew is detected against the server `Date` header                                                                                                       |
| 401                      | Expected. Keep the database and the outbox intact, drop to the pairing screen, resume the flush after re-pairing on the same identifier                                                     |
| Conflict                 | Normal, not exceptional. Default to `keep_both`, never silently overwrite, always copy aside before any overwrite                                                                           |
| Partial write            | Write `.part`, verify, rename. A half-written file is never visible under its final name                                                                                                    |

Retries lean on the server's idempotency rather than on an invented ack protocol.
