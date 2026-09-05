# Workflow trigger matrix

| Workflow | Trigger | Writes | May trigger | Protection |
| --- | --- | --- | --- | --- |
| Validate | PR/main push | none | none | Read-only, no secrets. |
| Collect | schedule/dispatch | collected/ | Generate only if a human pushes collected/ | Bot `GITHUB_TOKEN` pushes do not fire `push` workflows; no loop. |
| Calculate | data push/schedule/dispatch | generated/ | Generate only if human changes generated/ | Own bot push does not fire `push`; path allowlist. |
| Generate | data/collected/generated push/schedule/dispatch | generated/, reports/, web/data/ | Pages `workflow_run` | Bot output does not re-run Generate; shared writer lock. |
| Lock | dispatch | reports/ | none | Manual only, shared writer lock. |
| Publish DRY_RUN | watchdog/dispatch | reports/, publish-log/ | none | Shared writer lock; date/hash idempotency. |
| Republish DRY_RUN | dispatch force=true | generated/, reports/, publish-log/ | Pages `workflow_run` | Explicit force/reason, shared writer lock. |
| Deploy Pages | web push / Generate or Republish completion / dispatch | Pages deployment only | none | No repository write; Pages artifact scan; cannot recurse. |

GitHub documents that commits created with `GITHUB_TOKEN` do not trigger new `push` workflows. The graph is therefore acyclic even though source updates may start a human-triggered Calculate/Generate sequence. Shared `qimiao-v4-repository-writer` concurrency serializes every repository writer. Current hosted proof and run IDs are maintained in V4_GITHUB_RUNTIME_QA.md.
