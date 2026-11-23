"""Prueba el protocolo publicado contra la biblioteca privada preparada por Build-Pilot."""
from pathlib import Path
import hashlib
import json
import subprocess

base = Path(__file__).resolve().parents[3] / "artifacts/tauri-nexcore/B"
exe = base / "core/NexSpace.CorePilot.exe"
counter = 0

def launch():
    return subprocess.Popen([exe], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW)

def ask(proc, op, *, version=1, error=None, **fields):
    global counter
    counter += 1
    msg = dict(v=version, id=counter, op=op, **fields)
    proc.stdin.write(json.dumps(msg) + "\n")
    proc.stdin.flush()
    result = json.loads(proc.stdout.readline())
    assert (result["id"], result["v"], result["error"]) == (counter, 1, error), result.get("error")
    return result["data"]

def stop(proc):
    proc.stdin.close()
    assert proc.wait(5) == 0

def digest():
    return {f.name: hashlib.sha256(f.read_bytes()).digest() for f in (base / "pilot-data").glob("*.txt")}

if __name__ == "__main__":
    before = digest()
    proc = launch()
    state = ask(proc, "load")
    rows = state["items"]
    first = rows[0]
    assert first["exists"] and first["icon"].startswith("data:image/png;base64,")
    assert any(c["id"] == state["id"] for c in ask(proc,"list"))
    try:
        ask(proc, "load", version=2, error="invalid-envelope")
        ask(proc, "unknown", error="unsupported-operation")
        ask(proc, "open", item="00000000-0000-0000-0000-000000000000", error="unknown-reference")
        ask(proc, "open", item=next(r["id"] for r in rows if not r["exists"]), error="missing")
        ask(proc, "rename", item=first["id"], name="Persistencia del piloto")
        ask(proc, "reorder", item=first["id"], offset=1)
        ask(proc, "placement", x=139,y=93,width=420,height=300)
        ask(proc, "collapse", collapsed=True)
        stop(proc)
        proc = launch()
        restored = ask(proc,"load")
        assert restored["items"][1]["name"] == "Persistencia del piloto"
        assert restored["placement"] == dict(monitorKey="primary",x=139,y=93,width=420,height=300)
        assert restored["isCollapsed"]
        # Una acción de prueba explícita abre solamente el TXT de la biblioteca aislada.
        ask(proc,"open",item=first["id"])
    finally:
        if proc.poll() is not None: proc = launch()
        ask(proc,"rename",item=first["id"],name=first["name"])
        if ask(proc,"load")["items"][0]["id"] != first["id"]: ask(proc,"reorder",item=first["id"],offset=-1)
        p=state["placement"]
        ask(proc,"placement",**{k:p[k] for k in ("x","y","width","height")})
        ask(proc,"collapse",collapsed=state["isCollapsed"])
        stop(proc)
    assert digest() == before
    print("OK: SQLite, icono Shell, IDs, errores, reapertura, orden, nombre, posición, contracción, EOF y hash de fichas.")
