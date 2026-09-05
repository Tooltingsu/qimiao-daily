"""Trusted-main workflow entry point. CLI business logic stays in .NET."""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from zoneinfo import ZoneInfo
from writeback import persist, git

ROOT = Path.cwd()
DLL = ROOT / 'src/QimiaoDaily.V4/bin/Release/net8.0/QimiaoDaily.V4.dll'

def cli(command, date, *options):
    p = subprocess.run(['dotnet', str(DLL), command, '--root', str(ROOT), '--date', date, *options], capture_output=True, text=True, encoding='utf-8')
    if p.returncode:
        raise RuntimeError(p.stderr.strip() or p.stdout[-1500:])
    return json.loads(p.stdout)

def read(path, fallback):
    p = ROOT / path
    return json.loads(p.read_text(encoding='utf-8-sig')) if p.exists() else fallback

def fingerprint():
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT / 'data').glob('*.json')}

def main():
    kind = sys.argv[1]
    started = dt.datetime.now(dt.timezone.utc).isoformat()
    date = os.environ.get('INPUT_DATE') or dt.datetime.now(ZoneInfo('Asia/Shanghai')).date().isoformat()
    if dt.date.fromisoformat(date).isoformat() != date:
        raise ValueError('Date must be yyyy-MM-dd')
    source = git('rev-parse', 'HEAD').stdout.strip()
    summary = {'workflow': kind, 'started': started, 'sourceCommit': source, 'date': date,
               'status': 'RUNNING', 'new': 0, 'updated': 0, 'skipped': 0, 'conflicts': 0}
    before = fingerprint()
    try:
        cli('validate', date)
        if kind == 'collect':
            result = cli('collect', date)
        elif kind in ('generate', 'republish'):
            cli('calculate', date)
            options = ['--source-commit', source]
            if kind == 'republish':
                if os.environ.get('INPUT_FORCE') != 'true':
                    raise ValueError('force=true is required')
                options += ['--force', 'true', '--reason', os.environ.get('INPUT_REASON', ''), '--dry-run', 'true', '--workflow-run', os.environ['RUN_URL']]
            result = cli(kind, date, *options)
            cli('build-pages', date)
        elif kind == 'lock':
            result = cli('lock', date, '--mode', 'manual')
        elif kind == 'publish':
            options = ['--dry-run', 'true', '--workflow-run', os.environ['RUN_URL'], '--watchdog', 'true']
            if os.environ.get('SIMULATE_DEADLINE') == 'true':
                options += ['--simulate-deadline', 'true']
            result = cli('publish', date, *options)
        elif kind == 'calculate':
            result = cli('calculate', date)
        else:
            raise ValueError('Unknown workflow operation')
        if fingerprint() != before:
            raise RuntimeError('Manual data changed during automatic workflow')
        manifest = read(f'reports/{date}/manifest.json', {})
        providers = read('collected/provider-status.json', [])
        summary.update({'status': 'PASS', 'result': result, 'generatedRevision': manifest.get('latestRevision'),
            'reportHash': manifest.get('reportHash'), 'lockStatus': manifest.get('lockReason'),
            'publishStatus': manifest.get('state'), 'degradedSources': [x for x in providers if x['status'] != 'HEALTHY']})
        summary['writeback'] = persist(kind, f"chore(auto): {kind} {date} r{manifest.get('latestRevision', 0):03d}")
        summary['updated'] = summary['writeback'].get('files', 0)
        summary['skipped'] = int(result.get('status', '').startswith('SKIPPED') or result.get('skipped', False))
    except Exception as exc:
        summary.update(status='FAIL', error=str(exc), conflicts=int('conflict' in str(exc).lower()))
        raise
    finally:
        summary['ended'] = dt.datetime.now(dt.timezone.utc).isoformat()
        evidence = Path(os.environ.get('RUNNER_TEMP', '.runtime-evidence')) / 'v4-runtime.json'
        evidence.parent.mkdir(parents=True, exist_ok=True)
        evidence.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
        if os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as f:
                f.write('# QimiaoDaily ' + kind + '\n\n```json\n' + json.dumps(summary, ensure_ascii=False, indent=2) + '\n```\n')
        print(json.dumps(summary, ensure_ascii=False))

if __name__ == '__main__':
    main()
