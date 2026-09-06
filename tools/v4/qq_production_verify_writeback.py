"""Promote one submitted production forum post only after read-back proves it visible."""
import datetime as dt
import json
import os
from pathlib import Path

root = Path.cwd()
date = os.environ['INPUT_REPORT_DATE']
revision = int(os.environ['INPUT_REVISION'])
result = json.loads(Path(os.environ['FORUM_VERIFY_JSON']).read_text(encoding='utf-8'))
matches = result.get('matches') or []
if not matches:
    raise RuntimeError('Forum read-back found no visible production thread; PublishLog remains pending.')

log_path = root / 'publish-log' / f'{date}.json'
log = json.loads(log_path.read_text(encoding='utf-8'))
attempt = next((item for item in reversed(log.get('attempts', []))
                if item.get('revision') == revision and item.get('status') == 'SUBMITTED_VISIBILITY_PENDING'), None)
if attempt is None:
    raise RuntimeError('No matching submitted production attempt is available to promote.')
attempt['status'] = 'PUBLISHED'
attempt['publishedAt'] = dt.datetime.now(dt.timezone.utc).isoformat().replace('+00:00', 'Z')
attempt['qqPostId'] = matches[0].get('threadId') or attempt.get('qqPostId')
log_path.write_text(json.dumps(log, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
