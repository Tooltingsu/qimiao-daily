"""Scan tracked/candidate files or a deployed Pages tree without printing secret values."""
import argparse
import json
import re
import subprocess
from pathlib import Path

PATTERNS = [
    rb"(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})",
    rb"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----",
    rb"(?:AKIA|ASIA)[A-Z0-9]{16}",
    rb"PHPSESSID=[0-9]{3,}_[A-Za-z0-9]{10,}",
    rb"(?i)(?:QQ_BOT_SECRET|PIXIV_SESSION|GITHUB_TOKEN|APP_SECRET)\s*[=:]\s*[\"']?[A-Za-z0-9_+/=-]{20,}",
]
DENIED = {"bin", "obj", "node_modules", ".vs", ".idea", "publish", "publish-v3", "cache", "logs", "browser-profile"}

def scan(root, files):
    findings = []
    count = size = 0
    for name in sorted(set(files)):
        p = root / name
        if not p.is_file():
            continue
        count += 1
        size += p.stat().st_size
        if any(part in DENIED for part in Path(name).parts) or p.name == '.env' or (p.name.startswith('.env.') and p.name != '.env.example') or p.suffix in {'.db', '.sqlite', '.sqlite3', '.pem', '.key', '.pfx', '.p12'}:
            findings.append({'path': name, 'reason': 'prohibited artifact'})
        data = p.read_bytes()
        if any(re.search(pattern, data) for pattern in PATTERNS):
            findings.append({'path': name, 'reason': 'credential-like content (redacted)'})
    return {'status': 'FAIL' if findings else 'PASS', 'files': count, 'bytes': size, 'findings': findings}

if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--tree')
    ap.add_argument('--output')
    args = ap.parse_args()
    root = Path(args.tree or '.').resolve()
    files = [str(p.relative_to(root)) for p in root.rglob('*') if p.is_file()] if args.tree else subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard']).decode().strip('\0').split('\0')
    result = scan(root, files)
    rendered = json.dumps(result, indent=2)
    print(rendered)
    if args.output:
        Path(args.output).parent.mkdir(parents=True, exist_ok=True)
        Path(args.output).write_text(rendered + '\n', encoding='utf-8')
    raise SystemExit(0 if result['status'] == 'PASS' else 1)
