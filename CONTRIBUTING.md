# Contributing to the world

Write access is earned through GridSchool and kept through alumni citizenship.
Everyone else: the repo is public to read, the world is public to watch.

## The ceremony (no exceptions, including the maintainer)

1. **Claim a mission.** One issue, assigned to you. One system per person at a time.
2. **Map before you touch.** Trace the path your change affects. If you cannot redraw it
   from memory, you may not change it.
3. **Contract first.** Fill the Engineering Contract section of the PR template *before*
   writing the fix. Maintainer accepts or rejects the contract before execution.
4. **Predict, then execute.** Write where you expect the model (or you) to fail. Then do it.
   The gap between prediction and reality goes in the failure log.
5. **Prove it.** A test or a command a stranger can run that fails on the old behavior.
6. **PR with the three artifacts.** Contract, failure log, agent log. The template enforces it.
7. **Review.** Answer every comment. The thread is part of your evidence.
8. **Staging.** Merge deploys to staging automatically. Watch your change live.
9. **Promote.** Live deploys are manual, done together on Fridays. Incidents on live are
   written up, not hidden. Rollbacks are canon.

## Environments

| Env | Deploy | Purpose |
|---|---|---|
| local | `dotnet run` / `docker compose up` | your machine, your rules |
| staging | auto on merge to `main` | courage is cheap here |
| live | manual promote, Fridays | the world people stand in |

## Model routing (agent log expectations)

Frontier model for architecture, debugging unknowns, review. Workhorse model for mechanical
edits and scaffolds. **No model for comprehension** — reading the system is your job.
The agent log names which model did what and what it cost. Doing the whole ticket by hand is
fine; say so.

## What gets a PR closed without review

Employer code. A tidied file nobody asked for. A portfolio bullet a vibe-coder could claim
from the same prompt. An empty failure log on a nontrivial change.
