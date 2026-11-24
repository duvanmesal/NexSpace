"""Run against an isolated SQLite snapshot, including a real Windows .lnk."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--pilot", type=Path, default=Path(__file__).resolve().parents[3] / "artifacts/tauri-nexcore/B")
parser.add_argument("--open-folder", action="store_true", help="Abrir la carpeta de prueba con su asociación de Windows.")
options = parser.parse_args()
base = options.pilot.resolve(strict=True)
home = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-contract-"))
env = dict(os.environ, NEXSPACE_FLOATING_TEST_DIR=str(home))
source = sqlite3.connect((base/"pilot-data/floating.db").as_uri()+"?mode=ro",uri=True)
copy = sqlite3.connect(home/"floating.db")
source.backup(copy)
copy.close()
source.close()

def launch():
    return subprocess.Popen([base/"core/NexSpace.CorePilot.exe","--floating"],env=env,
        stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding="utf-8",
        creationflags=subprocess.CREATE_NO_WINDOW)

proc=launch()
sequence=0
profile=None
def request(op,error=None,**args):
    global sequence
    sequence+=1
    body=dict(v=2,id=sequence,op=op,profile=profile)
    body.update(args)
    proc.stdin.write(json.dumps(body)+"\n")
    proc.stdin.flush()
    reply=json.loads(proc.stdout.readline())
    assert (reply["id"],reply["v"],reply["error"])==(sequence,2,error),(op,reply["error"])
    return reply["data"]

def stop():
    proc.stdin.close()
    assert proc.wait(6)==0

def hash_tree(root):
    return {p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest()
            if p.is_file() else "folder" for p in root.rglob("*")}

files=home/"recursos"
files.mkdir()
(files/"Original.txt").write_text("NexSpace · prueba",encoding="utf-8")
(files/"Documento.pdf").write_bytes(b"%PDF-1.4\n% NexSpace isolated reference test\n%%EOF\n")
(files/"Carpeta").mkdir()
subprocess.run(["pwsh","-NoProfile","-Command",
    "$s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $pwd 'Acceso.lnk'));"
    "$s.TargetPath=(Join-Path $pwd 'Original.txt');$s.Save()"],cwd=files,check=True)
before=hash_tree(files)
try:
    manifest=request("manifest")
    profile=manifest["profile"]
    group=request("createGroup",name="Prueba de paridad")
    request("renameGroup",target=group,name="Trabajo")
    for path in files.iterdir():
        request("add",target=group,path=str(path))
    request("add",target=group,path=str(files/"Original.txt"),error="duplicate-reference")
    assert request("page",target=group)["total"]==4
    rows=request("page",target=group)["rows"]
    assert {r["kind"] for r in rows}=={"Shortcut","File","Folder"}
    if options.open_folder:
        folder = next(r for r in rows if r["kind"] == "Folder")
        request("open", target=group, item=folder["id"])
        verify = """
        $sh=New-Object -ComObject Shell.Application
        $timer=[Diagnostics.Stopwatch]::StartNew()
        do {
            foreach($view in $sh.Windows()) {
                try { if($view.Document.Folder.Self.Path -eq $env:NEXSPACE_EXPECTED_FOLDER){exit 0} } catch {}
            }
            Start-Sleep -Milliseconds 100
        } while($timer.Elapsed.TotalSeconds -lt 10)
        exit 1
        """
        subprocess.run(["pwsh", "-NoProfile", "-Command", verify], check=True,
                       env=dict(env, NEXSPACE_EXPECTED_FOLDER=str(files/"Carpeta")))
        print("PASS: Explorer abrió el destino aislado solicitado por identificador.")
    request("rename",target=group,item=rows[0]["id"],name="Primera")
    request("moveItem",target=group,item=rows[0]["id"],offset=1)
    request("placement",target=group,placement=dict(monitorKey="primary",x=92,y=45,width=401,height=274))
    request("lock",target=group,flag=True)
    request("placement",target=group,placement=dict(monitorKey="primary",x=200,y=100,width=422,height=300))
    for op in ("listView","collapse"):request(op,target=group,flag=True)
    request("createCategory",name="Vinculada",link=group)
    request("createCategory",name="Propia")
    linked,own=request("manifest")["categories"][-2:]
    assert request("page",target=linked["id"],category=True)["rows"][1]["name"]=="Primera"
    request("add",target=own["id"],category=True,path=str(files/"Documento.pdf"))
    request("moveCategory",target=own["id"],offset=-1)
    request("vertical",flag=True)
    duplicate=request("createProfile",name="Duplicado",flag=True)
    blank=request("createProfile",name="Vacío",flag=False)
    request("switchProfile",target=duplicate)
    request("remove",target=group,item=rows[0]["id"],error="stale-profile")
    profile=duplicate
    request("renameGroup",target=group,name="Solo en duplicado")
    request("deleteCategory",target=own["id"])
    request("switchProfile",target=blank)
    profile=blank
    assert not request("manifest")["groups"] and not request("manifest")["categories"]
    request("renameProfile",target=blank,name="Personal")
    request("switchProfile",target=manifest["profile"])
    profile=manifest["profile"]
    stop()
    proc=launch()
    current=request("manifest")
    actual=next(g for g in current["groups"] if g["id"]==group)
    assert actual["name"]=="Trabajo" and actual["shown"] and actual["isPositionLocked"] and actual["isCollapsed"]
    assert (actual["placement"]["x"],actual["placement"]["width"])==(92,422)
    assert current["vertical"] and current["categories"][-2]["id"]==own["id"]
    assert request("page",target=group)["list"]
    assert request("page",target=own["id"],category=True)["total"]==1
    # Introduce a reference which disappears, exclusively in this test directory.
    transient=home/"temporal.txt"
    transient.touch()
    request("add",target=group,path=str(transient))
    transient.unlink()
    missing=next(r for r in request("page",target=group)["rows"] if r["name"]=="temporal.txt")
    request("open",target=group,item=missing["id"],error="missing")
    request("remove",target=group,item=missing["id"])
    request("deleteGroup",target=group)
    assert not any(g["id"]==group for g in request("manifest")["groups"])
    assert not request("page",target=linked["id"],category=True)["rows"]
    assert before==hash_tree(files)
    print("PASS: contenedores, referencias, categorías, perfiles, orden, geometría, sesión, original intacto y reinicio.")
finally:
    if proc.poll() is None: stop()
