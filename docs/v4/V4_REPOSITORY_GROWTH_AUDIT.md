# Repository growth audit

The repository deliberately stores JSON metadata and immutable report text, not original Pixiv images or runner files. Current candidate scan measures about 2.3 MB across 337 files. Existing compact V4 record sizes indicate a conservative daily growth budget of 100 KB for collection snapshots, generated output, report revision and publication history.

| Horizon | Estimated additional size | Retention |
| --- | ---: | --- |
| 1 year | 36.5 MB | Keep all locked/published/superseded revisions and attempts. |
| 3 years | 109.5 MB | Keep the same audit records. |

Actual Git history will be measured after hosted runs. If a source metadata file grows faster than the budget, V4-C may add a bounded source snapshot policy, but it must not delete locked/published/superseded revisions or publication attempts. Artwork originals remain runner-temporary in the future publisher and must never be committed.
