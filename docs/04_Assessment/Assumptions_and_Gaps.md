# Assumptions and Gaps

Source: `docs/04_Assessment/Native_Assessment.md`

## Assumption status
- A1: Confirmed - no intrinsic .NET blocker.
- A2: Confirmed (tested surfaces) - binary-first native deps viable.
- A3: Confirmed - thin C ABI implemented and validated from .NET.
- A4: Partially confirmed - plugin/fallback architecture parity still pending.
- A5: Confirmed - pipeline semantics are substantial effort.
- A6: Confirmed - backend/schema semantics dominate port complexity.

## Primary remaining gaps
1. Plugin contract parity and compatibility testing.
2. Pipeline behavior parity refinement (cleanup + telemetry + stage-result contracts + runner partial/transform/context extraction controls + structured diagnostics + multi-document/artifact contracts now exist; broader upstream-aligned integration scenarios still needed).
3. Deep corpus-level field-value parity expansion and CI gating for parity harness outputs (text + non-text context fields now include geometry/font/confidence/reading-order + text direction/rendering/widget/presence/equality semantics + text semantic sequence signatures + bitmap/widget/hyperlink/shape signatures + non-text field-level semantic distributions; deeper text-value/sequence semantics remain).
4. Backend semantic parity breadth (DOCX/PPTX/HTML/LaTeX/XML).
5. Multi-platform parity harness operations and artifact retention in CI (Linux baseline gate now blocks; strict Linux lane remains telemetry; additional platform gates still pending).

## Practical interpretation
- Interop foundation is viable.
- Most remaining risk is behavioral fidelity.
- Upstream upgrade traceability is now script-driven via baseline metadata + one-command upgrade orchestration.

See:
- [[02_Architecture/Port_Strategy]]
- [[04_Assessment/Story_Verification]]
- [[06_Backlog/Future_Stories]]
