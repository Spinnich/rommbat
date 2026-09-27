---
summary: Which of Core or a front end owns a user-facing sentence.
read-when: Writing any message a Core service returns, or one the agent or UI prints.
---

# Where a sentence lives

**Where a sentence lives is a rule.** A sentence stating a rule or a fact about the library is
Core's, because it reads the same on either front end; a sentence naming a subcommand or a
flag is the caller's, because it would be false on the other one. A test sweeps every string
Core returns for the second kind.
