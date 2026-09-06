"""Persist a read-only forum visibility result without exposing target IDs."""
import datetime as dt
import json
import os
from pathlib import Path
import subprocess

from writeback import persist

ROOT = Path.cwd()
DLL = ROOT / "src/QimiaoDaily.V4/bin/Release/net8.0/QimiaoDaily.V4.dll"

def main():
    date = os.environ["INPUT_REPORT_DATE"]
    title_prefix = os.environ["INPUT_TITLE_PREFIX"]
    result_path = Path(os.environ["FORUM_VERIFY_JSON"])
    result = json.loads(result_path.read_text(encoding="utf-8"))
    if result.get("titlePrefix") != title_prefix or int(result.get("matchCount", 0)) < 1:
        raise RuntimeError("Read-only forum verifier did not confirm the exact requested test title prefix.")

    log_path = ROOT / "test-publish-log" / f"{date}.json"
    log = json.loads(log_path.read_text(encoding="utf-8"))
    attempts = log.get("attempts", [])
    match = next((item for item in reversed(attempts)
                  if item.get("status") == "TEST_SUBMITTED"
                  and (item.get("testTitlePrefix") == title_prefix
                       or (not item.get("testTitlePrefix") and title_prefix.endswith(f" {date}")))), None)
    if not match:
        raise RuntimeError("No matching submitted qq-test attempt can be updated.")
    match["status"] = "TEST_VISIBLE"
    match["verifiedAt"] = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")
    match["error"] = None
    log_path.write_text(json.dumps(log, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    built = subprocess.run(["dotnet", str(DLL), "build-pages", "--root", str(ROOT), "--date", date],
                           capture_output=True, text=True, encoding="utf-8", check=False)
    if built.returncode:
        raise RuntimeError(built.stderr.strip() or built.stdout[-1200:])
    persisted = persist("qq-test", f"chore(qq-test): {date} forum visibility verified")
    print(json.dumps({"date": date, "status": "TEST_VISIBLE", "writeback": persisted}, ensure_ascii=False))

if __name__ == "__main__":
    main()
