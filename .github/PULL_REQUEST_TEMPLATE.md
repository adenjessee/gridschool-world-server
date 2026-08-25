# Mission PR

Closes #<!-- issue number -->

## 1. Engineering contract (accepted before execution)

**Outcome, to whom:** <!-- user/world effect, not the code change -->
**Evidence this is the real problem:** <!-- repro, log, issue link -->
**Why AI / a script / by hand / not at all:** <!-- one honest paragraph. If you cannot finish it, you may be holding the trap. -->
**Allowed surface:** <!-- files the change may touch -->
**Prohibited:** <!-- what it must not touch -->
**Acceptance evidence (decided before execution):** <!-- the test/command a stranger runs -->

## 2. Failure log (a real miss, not "all good")

**What I thought:** 
**What was true:** 
**Where I stopped the agent / myself:** 
**Prediction vs reality:** <!-- what you predicted would fail, what actually did -->

## 3. Agent log

| Pass | Asked for | Model (frontier/workhorse/none) | Touched | Stopped? | Minutes |
|---|---|---|---|---|---|
|  |  |  |  |  |  |

**False assumption the model made:** 
**Would not delegate next time:** 

## 4. Proof

- [ ] The acceptance command fails on `main`, passes on this branch
- [ ] `done_when` on the issue is satisfied, literally
- [ ] No prohibited surface touched
- [ ] I can defend this in eight minutes to someone who did not help
