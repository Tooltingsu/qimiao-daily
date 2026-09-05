"""Stage only approved output paths; rebase/retry without force or conflict resolution."""
import subprocess
import time

ALLOWED = {
    'collect': ('collected/',),
    'calculate': ('generated/',),
    # The generator also refreshes the static Pages dashboard.  It is a
    # generated view, rather than user-maintained web source, so it is an
    # explicitly allow-listed write target.
    'generate': ('reports/', 'generated/', 'web/data/'),
    'lock': ('reports/',),
    'publish': ('reports/', 'publish-log/'),
    'republish': ('reports/', 'publish-log/', 'generated/', 'web/data/'),
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

    # A tool can create a final allowed projection while its first write set is
    # being staged (notably the generated Pages dashboard).  Fold only an
    # explicitly allow-listed residual set into the same immutable writeback
    # commit before rebasing; never stash, discard, or stage arbitrary files.
    residual = git('diff', '--name-only', 'HEAD', cwd=cwd).stdout.splitlines()
    residual += git('ls-files', '--others', '--exclude-standard', cwd=cwd).stdout.splitlines()
    illegal_residual = [p for p in residual if not p.startswith(roots)]
    if illegal_residual:
        raise RuntimeError('Write-path violation after staging: ' + ', '.join(illegal_residual))
    if residual:
        git('add', '--', *sorted(set(residual)), cwd=cwd)
        git('commit', '--amend', '--no-edit', cwd=cwd)

    for attempt in range(3):
        git('fetch', 'origin', branch, cwd=cwd)
        rebased = git('rebase', 'origin/' + branch, cwd=cwd, check=False)
        if rebased.returncode:
            # `rebase --abort` is only valid after Git has entered a rebase
            # state.  Preserve the original failure details even when setup
            # itself failed before that point (for example a repository state
            # problem), while still never resolving/overwriting remotely.
            git('rebase', '--abort', cwd=cwd, check=False)
            detail = (rebased.stderr or rebased.stdout).strip().replace('\n', ' ')
            dirty = git('status', '--porcelain', cwd=cwd, check=False).stdout.replace('\n', '; ')
            raise RuntimeError('Git rebase failed; remote not overwritten. ' + detail + ' residual=' + (dirty or '<none>'))
        pushed = git('push', 'origin', 'HEAD:' + branch, cwd=cwd, check=False)
        if not pushed.returncode:
            return {'status': 'COMMITTED', 'files': len(set(changed)), 'commit': git('rev-parse', 'HEAD', cwd=cwd).stdout.strip(), 'attempt': attempt + 1}
        time.sleep(attempt + 1)
    raise RuntimeError('Git write-back failed after 3 fetch/rebase/push attempts; no force used.')
