# V4-B.1 Pages production polish

Status: **PASS — public Pages UI, workflow runtime and artifact safety verified.**

## Delivered changes

- The public control centre now uses Chinese-facing labels: `绮喵日报 · 自动化控制中心`、`日报版本`、`今日内容` and `来源健康`.
- Persisted machine values remain unchanged in JSON. The web client maps report states and provider states to Chinese only when displaying them, including `演练完成`、`准备重新发布`、`正常`、`部分来源异常`、`需要登录凭据` and `失败`.
- Responsive CSS protects the shell, cards, action group, report text and provider rows from horizontal overflow. At narrow widths the primary actions stack, the copy button is full width, and long report/provider text wraps.
- Official GitHub actions were upgraded without adding third-party actions:
  - `actions/checkout@v6`
  - `actions/setup-dotnet@v5`
  - `actions/configure-pages@v6`
  - `actions/upload-pages-artifact@v5`
  - `actions/deploy-pages@v5`

## Hosted workflow evidence

| Workflow | Run | Result |
| --- | ---: | --- |
| Validate | [33976367232](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33976367232) | PASS |
| Generate (manual dispatch, 2026-09-09) | [33976367801](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33976367801) | PASS; ran `checkout@v6` and `setup-dotnet@v5` |
| Deploy Pages | [33976393497](https://github.com/Tooltingsu/qimiao-daily/actions/runs/33976393497) | PASS; deployed the generated projection |

The latest deployment log contains no Node 20 deprecation warning. It contains only Node's unrelated `punycode` module warning emitted while `actions/deploy-pages@v5` runs; that is not a Node 20 action-runtime warning.

## Production browser QA

Public site: <https://tooltingsu.github.io/qimiao-daily/>.

| Viewport | `documentElement.scrollWidth` | `clientWidth` | Result |
| --- | ---: | ---: | --- |
| Desktop 1920 × 1080 | 1920 | 1920 | PASS |
| Mobile 390 × 844 | 390 | 390 | PASS |

The same checks found `body.scrollWidth <= clientWidth` for both viewports, no untranslated state/provider enum in the visible status UI, and an in-bounds mobile copy button (`left=29`, `right=361`, width `332`).

Production screenshots, captured from the public Pages URL rather than localhost:

1. `docs/v4/evidence/v4-b1-production/pages-desktop-1920x1080.png`
2. `docs/v4/evidence/v4-b1-production/pages-mobile-390x844.png`

## Security boundary

- `python tools/v4/secret_scan.py` = PASS (`382` scanned files, `0` findings).
- The production Deploy Pages run also ran `secret_scan.py --tree web` before upload = PASS (`6` public files, `0` findings).
- No QQ publishing API was invoked. QQ credentials remain absent and production QQ posting remains out of scope for V4-B.1.
