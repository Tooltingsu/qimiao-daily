# Repository content audit

2026-09-05 initial candidate scan: PASS, 328 files, 2,258,110 bytes, zero findings; machine-readable evidence in evidence/repository-scan.json. Scanner is tools/v4/secret_scan.py and is repeated on each proposed commit and the actual Pages artifact.

Included: V3/V4 source, tests, migration helpers, schemas, five data layers, compact POC revisions and documentation. The 33 inactive legacy calendar records remain preserved. Images are metadata; the only tracked raster is a UI screenshot.

Excluded by .gitignore: bin, obj, publish distributions, SQLite working databases, local log/cache/image directories, IDE state, node_modules, keys and credential files. `.env.example` contains empty credential placeholders only. Source tests contain dummy sessions, not real credentials. Runtime uses the existing authenticated CLI/keyring; its credential values are never copied into project files.

Target confirmed by GitHub API: Tooltingsu/qimiao-daily. User explicitly approved PUBLIC visibility after private Pages returned HTTP 422. `main` is the initial default branch. No other repository is targeted.

Before each push: scan candidate/tracked content, inspect diff/staged inventory, reject generated binaries or unexpected private files. Growth measurements and deployed artifact verification are recorded in V4_GITHUB_RUNTIME_QA.md.
