"""Persist a qq-test result and refresh only the public Pages projection."""
import datetime as dt
import json
import os
from pathlib import Path
import subprocess

from writeback import git, persist

ROOT = Path.cwd()
DLL = ROOT / "src/QimiaoDaily.V4/bin/Release/net8.0/QimiaoDaily.V4.dll"

def main():
    date = os.environ.get("INPUT_REPORT_DATE") or dt.datetime.now(dt.timezone.utc).astimezone(
        dt.timezone(dt.timedelta(hours=8))).date().isoformat()
    if dt.date.fromisoformat(date).isoformat() != date:
        raise ValueError("reportDate must be yyyy-MM-dd")

    # Pages is only a presentation of the already persisted qq-test log; it
    # never receives credentials, tokens or a raw target id.
    built = subprocess.run(
        ["dotnet", str(DLL), "build-pages", "--root", str(ROOT), "--date", date],
        capture_output=True, text=True, encoding="utf-8", check=False)
    if built.returncode:
        raise RuntimeError(built.stderr.strip() or built.stdout[-1200:])

    log_path = ROOT / "test-publish-log" / f"{date}.json"
    log = json.loads(log_path.read_text(encoding="utf-8")) if log_path.exists() else {"attempts": []}
    latest = log.get("attempts", [])[-1] if log.get("attempts") else {}
    result = persist("qq-test", f"chore(qq-test): {date} {latest.get('mode', 'unknown')} {latest.get('status', 'unknown')}")
    summary = {
        "date": date,
        "environment": "qq-test",
        "status": latest.get("status", "NOT_TESTED"),
        "mode": latest.get("mode"),
        "messageCount": len(latest.get("messages", [])),
        "writeback": result,
    }
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
            output.write("# QimiaoDaily QQ test result\n\n```json\n" + json.dumps(summary, ensure_ascii=False, indent=2) + "\n```\n")
    print(json.dumps(summary, ensure_ascii=False))

if __name__ == "__main__":
    main()
