#!/usr/bin/env python3
"""Copia coordinada de base, evidencias y material de recuperación de sesiones."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess


def herramienta(ruta, argumentos):
    resultado = subprocess.run(["/usr/bin/dotnet", str(ruta), *argumentos], capture_output=True, text=True)
    if resultado.returncode:
        raise RuntimeError("La herramienta de respaldo no terminó correctamente; revisar el conjunto pendiente.")
    return json.loads(resultado.stdout)


def principal():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True)
    parser.add_argument("--retencion-dias", type=int, default=7)
    parser.add_argument("--instalar", action="store_true")
    args = parser.parse_args()
    if os.geteuid() != 0 or not 1 <= args.retencion_dias <= 365 or not re.fullmatch(r"[a-z][a-z0-9_]{0,62}", args.base):
        raise RuntimeError("Requiere root y una retención entre 1 y 365 días.")
    if args.instalar:
        fuente = Path(__file__).resolve().parent
        destino_script = Path("/usr/local/libexec/cet-respaldar.py")
        destino_script.parent.mkdir(mode=0o755, parents=True, exist_ok=True)
        shutil.copyfile(Path(__file__), destino_script)
        destino_script.chmod(0o750)
        Path("/var/backups/constructora-eder-torres").mkdir(mode=0o700, parents=True, exist_ok=True)
        for extension in ("service", "timer"):
            nombre = f"constructora-eder-torres-respaldo.{extension}"
            contenido = (fuente / nombre).read_text(encoding="utf-8-sig").replace("@@BASE@@", args.base)
            destino_unidad = Path("/etc/systemd/system") / nombre
            if destino_unidad.exists():
                raise RuntimeError("La unidad de respaldo ya existe; revisar antes de actualizar.")
            destino_unidad.write_text(contenido, encoding="utf-8")
            destino_unidad.chmod(0o644)
        for orden in (["systemctl", "daemon-reload"],
                      ["systemctl", "start", "constructora-eder-torres-respaldo.service"],
                      ["systemctl", "enable", "--now", "constructora-eder-torres-respaldo.timer"]):
            resultado = subprocess.run(orden, capture_output=True, text=True)
            if resultado.returncode:
                raise RuntimeError("No se completó la instalación/primera ejecución del respaldo.")
        print("Respaldo inicial verificado y temporizador diario habilitado.")
        return
    if not os.environ.get("ConnectionStrings__Respaldo"):
        raise RuntimeError("Falta el entorno privado de respaldos.")
    os.umask(0o077)
    aplicacion = Path("/opt/constructora-eder-torres/actual/Respaldos/ConstructoraEderTorres.Respaldos.dll")
    datos = Path("/var/lib/constructora-eder-torres")
    destino = Path("/var/backups/constructora-eder-torres")
    destino.mkdir(mode=0o700, parents=True, exist_ok=True)
    resultado = herramienta(aplicacion, ["crear", "--destino", str(destino), "--evidencias", str(datos / "evidencias"),
                                         "--binarios", "/usr/bin", "--base-esperada", args.base])
    copia = Path(resultado["Carpeta"]).resolve()
    if copia.parent != destino.resolve() or not copia.name.startswith("respaldo-"):
        raise RuntimeError("El destino devuelto está fuera de la carpeta de respaldos.")
    herramienta(aplicacion, ["verificar", "--carpeta", str(copia), "--binarios", "/usr/bin"])
    seguridad = copia / "seguridad"
    seguridad.mkdir(mode=0o700)
    shutil.copyfile("/etc/constructora-eder-torres/sesiones.pfx", seguridad / "sesiones.pfx")
    shutil.copytree(datos / "claves", seguridad / "claves")
    inventario = []
    for archivo in sorted(seguridad.rglob("*")):
        if archivo.is_file():
            archivo.chmod(0o600)
            with archivo.open("rb") as flujo:
                sha256 = hashlib.file_digest(flujo, "sha256").hexdigest()
            inventario.append({"Ruta": archivo.relative_to(seguridad).as_posix(), "Sha256": sha256})
    (seguridad / "manifiesto-seguridad.json").write_text(json.dumps(inventario, indent=2), encoding="utf-8")
    herramienta(aplicacion, ["depurar", "--destino", str(destino), "--retencion-dias", str(args.retencion_dias),
                            "--conservar-minimo", "2", "--binarios", "/usr/bin"])
    print(json.dumps({"Respaldo": copia.name, "Base": args.base, "Evidencias": resultado["Evidencias"],
                      "MaterialSesionIncluido": True, "RetencionDias": args.retencion_dias}, indent=2))


if __name__ == "__main__":
    try:
        principal()
    except (OSError, RuntimeError, ValueError, KeyError) as error:
        raise SystemExit(str(error))
