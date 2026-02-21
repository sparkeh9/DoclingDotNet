---
name: debug-loop-breaker
description: "Break repeated debug loops by enforcing hypothesis-driven attempts, loop budgets, and evidence logging."
---

# Debug Loop Breaker

## When to use
- Same error appears after 2 attempts.
- You are repeating commands with only minor/no code changes.
- You are unsure whether failure is code, environment, or tooling.

## Protocol
1. Freeze retries.
2. Write one explicit hypothesis:
   - "I believe X fails because Y."
3. Define one falsifiable check for that hypothesis.
4. Run only that check.
5. If disproven, replace hypothesis; do not retry the same approach.

## Loop budget
- Max 3 attempts per hypothesis.
- After 3 failed attempts, mandatory pivot:
  - isolate smaller repro, or
  - switch tooling strategy, or
  - add instrumentation/assertions.

## Required evidence per attempt
- Command run.
- Observable output delta.
- Decision: continue / pivot / rollback.

## Exit criteria
- Root cause identified with evidence, or
- Problem reframed into a smaller reproducible failing case with next action.
