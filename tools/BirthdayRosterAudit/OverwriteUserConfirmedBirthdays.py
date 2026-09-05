import json, sqlite3, uuid
from datetime import datetime, timezone
from pathlib import Path

root = Path(r"C:\Users\Administrator\Desktop\qimiaobotv2")
source = root / "artifacts/manual-data-pivot/birthday-cover-import.json"
user_script = root / "tools/BirthdayRosterAudit/ApplyUserBirthdayList.ps1"
db_path = Path(r"C:\Users\Administrator\AppData\Local\QimiaoDaily\data\qimiao.db")
audit_path = root / "artifacts/birthday-roster-review-20260820/user-birthday-overwrite-audit-20260820.json"

payload = json.loads(source.read_text(encoding="utf-8-sig"))
rows = [dict(x) for x in payload["birthdays"] if x["game"] == "GENSHIN" and x["character"] != "派蒙"]
script_text = user_script.read_text(encoding="utf-8")
def parse_block(marker):
    start = script_text.index(marker) + len(marker)
    end = script_text.index("'@", start)
    result = []
    for line in script_text[start:end].splitlines():
        line = line.strip()
        if not line: continue
        game, character, month, day = line.split('|')
        result.append({"game": game, "character": character, "month": int(month), "day": int(day), "aliases": character})
    return result
rows.extend(parse_block("$hi3 = ParseKnown @'"))
rows.extend(parse_block("$nte = ParseKnown @'"))
rows.extend(parse_block("$genshin = ParseKnown @'"))
# The user list marks Varka's slot as 法尔伽 / unpublished; remove the
# legacy 瓦尔卡 birthday instead of retaining old data.
rows = [x for x in rows if not (x.get('game') == 'GENSHIN' and x.get('character') == '瓦尔卡')]

# The supplied list is authoritative. Normalize the user's later Chinese
# corrections over the legacy import names, and remove records not in the list.
rename = {
    ("GENSHIN", "劳玛"): "菈乌玛",
    ("GENSHIN", "妮赫"): "奈芙尔",
    ("GENSHIN", "雅霍达"): "雅珂达",
    ("GENSHIN", "达莉娅"): "塔利雅",
    ("GENSHIN", "普茹"): "布伦妮",
    ("GENSHIN", "琳妮"): "莉奈娅",
    ("GENSHIN", "妮可"): "尼可",
}
date_override = {
    ("GENSHIN", "哥伦比娅"): (3, 7),
    ("GENSHIN", "杜林"): (0, 0),
    ("GENSHIN", "莉奈娅"): (0, 0),
}
drop = {("GENSHIN", "派蒙")}
# The user list names Varka's slot as 法尔伽 and marks it unpublished; the
# legacy 瓦尔卡 birthday must therefore not survive the overwrite.
drop.add(("GENSHIN", "瓦尔卡"))
normalized = []
for row in rows:
    key = (row["game"], row["character"])
    if key in drop:
        continue
    if key in rename:
        row["character"] = rename[key]
        key = (row["game"], row["character"])
    if key in date_override:
        row["month"], row["day"] = date_override[key]
    row["id"] = f"birthday-{row['game'].lower()}-{row['character']}"
    normalized.append(row)

# Remove duplicate names while keeping the last user-confirmed value.
unique = {}
for row in normalized:
    unique[(row["game"], row["character"])] = row
normalized = list(unique.values())
unknown = [r for r in normalized if not (1 <= r["month"] <= 12 and 1 <= r["day"] <= 31)]
unknown.append({"game":"GENSHIN","character":"法尔伽","month":0,"day":0,"aliases":"Varka","status":"UNKNOWN/PENDING_REVIEW","reason":"用户标记暂未公开"})
known = [r for r in normalized if r not in unknown]

now = datetime.now(timezone.utc).isoformat()
conn = sqlite3.connect(db_path)
try:
    conn.execute("BEGIN IMMEDIATE")
    before = conn.execute("SELECT COUNT(*) FROM birthdays").fetchone()[0]
    conn.execute("DELETE FROM birthdays")
    for row in known:
        conn.execute(
            """INSERT INTO birthdays
            (Id, Character, Franchise, Month, Day, Source, SourceUrl, Evidence,
             VerificationStatus, VerifiedAt, Enabled, Aliases,
             CanonicalCharacterNameZhCn, SourceTier, DataOrigin, OriginTrace, UserConfirmed)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (str(uuid.uuid4()), row["character"], row["game"], row["month"], row["day"],
             "user-confirmed-2026-08-20", "manual://user-birthday-list-20260820",
             "用户提供并确认的生日清单；人工确认日期", "VerifiedOfficial", now, 1,
             row.get("aliases", ""), row["character"], "manual", "Imported",
             "USER_CONFIRMED_BIRTHDAY_OVERWRITE_20260820", 1),
        )
    conn.commit()
    after = conn.execute("SELECT COUNT(*) FROM birthdays").fetchone()[0]
finally:
    conn.close()

audit = {
    "operation": "USER_CONFIRMED_BIRTHDAY_OVERWRITE",
    "at": now,
    "database": str(db_path),
    "before_count": before,
    "after_count": after,
    "known_written": len(known),
    "unknown_not_written": unknown,
    "source_file": str(source),
    "backup_file": str(db_path) + ".bak-before-user-birthday-overwrite-20260820",
}
audit_path.write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding="utf-8")
import_path = root / "artifacts/manual-data-pivot/birthday-user-confirmed-import-20260820.json"
import_path.write_text(json.dumps({"schemaVersion": 1, "sourceName": "用户确认生日清单 2026-08-20", "events": [], "banners": [], "versions": [], "birthdays": known, "anniversaries": []}, ensure_ascii=False, indent=2), encoding="utf-8")
unknown_path = root / "artifacts/birthday-roster-review-20260820/birthday-user-unknown-review.json"
unknown_path.write_text(json.dumps(unknown, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"before": before, "after": after, "known": len(known), "unknown": len(unknown)}, ensure_ascii=False))
