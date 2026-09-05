"""Stage only approved output paths; rebase/retry without force or conflict resolution."""
import subprocess
import time

ALLOWED = {
    'collect': ('collected/',),
    'calculate': ('generated/',),
    'generate': ('reports/', 'generated/'),
    'lock': ('reports/',),
    'publish': ('reports/', 'publish-log/'),
    'republish': ('reports/', 'publish-log/', 'generated/'),
}

def git(*args, cwd=None, check=True):
    return subprocess.run(['git', *args], cwd=cwd, check=check, capture_output=True, text=True)

def persist(kind, message, cwd=None, branch='main'):
    roots = ALLOWED[kind]
    changed = git('diff', '--name-only', 'HEAD', cwd=cwd).stdout.splitlines()
    changed += git('ls-files', '--others', '--exclude-standard', cwd=cwd).stdout.splitlines()
    illegal = [p for p in changed if not p.startswith(roots)]
    if illegal:
        raise RuntimeError('Write-path violation: ' + ', '.join(illegal))
    if not changed:
        return {'status': 'UNCHANGED', 'files': 0}
    git('config', 'user.name', 'qimiao-automation[bot]', cwd=cwd)
    git('config', 'user.email', 'qimiao-automation[bot]@users.noreply.github.com', cwd=cwd)
    git('add', '--', *sorted(set(changed)), cwd=cwd)
    git('commit', '-m', message, cwd=cwd)
    for attempt in range(3):
        git('fetch', 'origin', branch, cwd=cwd)
        rebased = git('rebase', 'origin/' + branch, cwd=cwd, check=False)
        if rebased.returncode:
            git('rebase', '--abort', cwd=cwd)
            raise RuntimeError('Git conflict: rebase aborted, remote not overwritten.')
        pushed = git('push', 'origin', 'HEAD:' + branch, cwd=cwd, check=False)
        if not pushed.returncode:
            return {'status': 'COMMITTED', 'files': len(set(changed)), 'commit': git('rev-parse', 'HEAD', cwd=cwd).stdout.strip(), 'attempt': attempt + 1}
        time.sleep(attempt + 1)
    raise RuntimeError('Git write-back failed after 3 fetch/rebase/push attempts; no force used.')
