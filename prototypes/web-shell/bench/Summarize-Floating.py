"""Summarize the four private process-tree traces. CPU is a sampled lower bound."""
import json
from pathlib import Path

evidence = Path(__file__).resolve().parents[3] / "artifacts/tauri-nexcore/evidence/paridad"
snapshots = set()
for count in (100, 300):
    for engine in ("WinUI", "Tauri"):
        data = json.loads((evidence / f"{engine}-{count}.json").read_text(encoding="utf-8-sig"))
        assert data["cycles"] == count and data["exit"] == 0 and not data["stop"]
        assert not data["afterThree"], "Quedan procesos observados"
        snapshots.add(data["snapshot"])
        tree = data["samples"]
        totals = []
        cpu = {}
        for sample in tree:
            rows = sample["rows"]
            for row in rows:
                assert row["composition"] >= 0 and row["events"] >= 0, "Contador incompleto"
                cpu[row["pid"]] = max(cpu.get(row["pid"], 0), row["cpu"])
            totals.append({key: sum(row[key] for row in rows)
                           for key in ("private", "handles", "gdi", "user", "events", "composition")})
        last = totals[-1]
        print(engine, count, "MiB pico/final",
              round(max(t["private"] for t in totals) / 2**20), round(last["private"] / 2**20),
              "CPU s", round(sum(cpu.values()), 1), "máx procesos", max(len(s["rows"]) for s in tree),
              "handles/GDI/USER/Event/Composition al final",
              *(last[key] for key in ("handles", "gdi", "user", "events", "composition")))
assert len(snapshots) == 1, "Las bibliotecas de partida difieren"
print("SHA-256 inicial común", snapshots.pop())
