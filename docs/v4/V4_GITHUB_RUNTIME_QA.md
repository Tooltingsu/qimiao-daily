# GitHub runtime QA

Status: IN PROGRESS. This document records actual hosted results only; local evidence is not substituted.

| Workflow | Run ID | Commit | Expected | Actual | Status |
| --- | ---: | --- | --- | --- | --- |
| Initial Validate | 33945320863 | c16af97 | Linux portable V4 build/test/validate | Failed during full solution restore because Desktop targets Windows (`NETSDK1100`). | FIXED, re-run pending |
| Initial Calculate | 33945320855 | c16af97 | Derived data write-back | Calculation completed and committed, but stale checkout plain push was rejected. | FIXED, re-run pending |
| Initial Generate | 33945320854 | c16af97 | Immutable revision write-back | PASS; created revision 004 and pushed f83e27f. | PASS (baseline) |
| Initial Pages | 33945320872 | c16af97 | Production Pages deploy | PASS. | PASS (baseline) |

Pending real evidence: corrected Validate, Calculate, Collect, Generate, Pages, manual Lock, auto-lock simulation, publish duplicate skip, Republish, concurrency pair, conflict test, deployed artifact scan, desktop/mobile screenshots and all provider status values.

The Pages service was enabled at `https://tooltingsu.github.io/qimiao-daily/` after the user approved public repository visibility. Evidence files, downloaded logs and screenshots are added only after they are collected from actual GitHub or Pages.
