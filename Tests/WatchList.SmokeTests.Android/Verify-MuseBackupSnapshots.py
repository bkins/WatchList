"""Compare the disposable emulator restore/restart snapshots, including every stored field."""

import json
from pathlib import Path
import sqlite3
import sys
import xml.etree.ElementTree as ElementTree


def read_snapshot(path):
    if not path.is_file():
        raise FileNotFoundError(path)
    with sqlite3.connect(path.as_uri() + "?mode=ro", uri=True) as database:
        assert database.execute("pragma integrity_check").fetchall() == [("ok",)]
        snapshot = {}
        for table in ("WatchItems", "MovieRecommendation"):
            snapshot[table] = database.execute(f'SELECT * FROM "{table}" ORDER BY Id').fetchall()
        orphan_count = database.execute(
            'SELECT COUNT(*) FROM MovieRecommendation AS source '
            'LEFT JOIN WatchItems AS item ON item.Id = source.WatchItemId '
            'WHERE item.Id IS NULL'
        ).fetchone()[0]
        assert orphan_count == 0, "A restored recommendation lost its WatchItem."
        return snapshot


def visible_text(path):
    return {node.attrib.get("text", "") for node in ElementTree.parse(path).iter("node")}


evidence_path = Path(sys.argv[1]).resolve()
backup = read_snapshot(evidence_path / "backup.db")
restored = read_snapshot(evidence_path / "restored-after-restart.db")
before = read_snapshot(evidence_path / "before.db")
reinstated = read_snapshot(evidence_path / "reinstated-after-restart.db")
assert backup == restored, "Restored records differ from the selected backup after restart."
assert before == reinstated, "The pre-verification test state was not fully reinstated."
assert len(backup["WatchItems"]) == len(backup["MovieRecommendation"]) == 1
assert len(before["WatchItems"]) == len(before["MovieRecommendation"]) == 2
assert before != backup, "The check must actually restore an earlier state."
restored_text = visible_text(evidence_path / "restored-ui.xml")
reinstated_text = visible_text(evidence_path / "reinstated-ui.xml")
assert "Dune" in restored_text and "Fringe" not in restored_text
assert {"Dune", "Fringe"}.issubset(reinstated_text)
print(json.dumps({
    "result": "PASS",
    "restored_watch_items": len(restored["WatchItems"]),
    "restored_recommendations": len(restored["MovieRecommendation"]),
    "reinstated_watch_items": len(reinstated["WatchItems"]),
    "reinstated_recommendations": len(reinstated["MovieRecommendation"]),
    "all_stored_fields_equal": True,
    "orphan_associations": 0,
    "all_integrity_checks": "ok",
    "native_restart_ui": "Dune restored; Dune and Fringe reinstated"
}, indent=2))
