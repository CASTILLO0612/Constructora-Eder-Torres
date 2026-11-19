#!/usr/bin/env python3
"""Publica el frontend mediante una nueva versión y un cambio atómico del enlace."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import tarfile


def huella(ruta):
    with ruta.open("rb") as archivo:
        return hashlib.file_digest(archivo, "sha256").hexdigest()


def principal():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--paquete", type=Path, required=True)
    parser.add_argument("--sha256", required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    if os.geteuid() != 0 or not re.fullmatch(r"\d{8}T\d{6}Z-[a-f0-9]{8}", args.version):
        raise RuntimeError("Requiere root y un identificador de versión válido.")
    if huella(args.paquete) != args.sha256:
        raise RuntimeError("El paquete no coincide con su SHA-256.")
    raiz = Path("/opt/constructora-eder-torres")
    enlace = raiz / "actual"
    anterior = enlace.resolve(strict=True)
    versiones = (raiz / "releases").resolve(strict=True)
    if anterior.parent != versiones:
        raise RuntimeError("La versión activa está fuera del directorio de publicaciones.")
    destino = versiones / args.version
    temporal = raiz / ".actual-siguiente"
    if destino.exists() or temporal.exists() or temporal.is_symlink():
        raise RuntimeError("El destino de la actualización está ocupado.")
    with tarfile.open(args.paquete, "r:gz") as paquete:
        miembros = paquete.getmembers()
        if any(m.issym() or m.islnk() or m.name.startswith("/") or ".." in Path(m.name).parts for m in miembros):
            raise RuntimeError("El paquete contiene rutas no admitidas.")
        if not {"index.html", "sw.js"}.issubset({m.name.removeprefix("./") for m in miembros}):
            raise RuntimeError("El paquete no contiene una PWA compilada.")
        shutil.copytree(anterior, destino)
        # Conserva los chunks anteriores para las pestañas que ya estaban abiertas.
        paquete.extractall(destino / "Web", filter="data")
    manifiesto = json.loads((anterior / "manifiesto.json").read_text(encoding="utf-8-sig"))
    manifiesto["VersionBase"] = manifiesto["Version"]
    manifiesto["Version"] = args.version
    manifiesto["ActualizacionFrontend"] = True
    manifiesto["Archivos"] = []
    for carpeta, subcarpetas, archivos in os.walk(destino):
        os.chmod(carpeta, 0o755)
        for nombre in sorted(archivos):
            archivo = Path(carpeta) / nombre
            os.chmod(archivo, 0o644)
            if archivo == destino / "manifiesto.json":
                continue
            manifiesto["Archivos"].append({"Ruta": archivo.relative_to(destino).as_posix(),
                                           "Bytes": archivo.stat().st_size, "Sha256": huella(archivo)})
    (destino / "manifiesto.json").write_text(json.dumps(manifiesto, indent=2), encoding="utf-8")
    for componente in ("Api", "Respaldos"):
        originales = {p.relative_to(anterior / componente): huella(p) for p in (anterior / componente).rglob("*") if p.is_file()}
        actuales = {p.relative_to(destino / componente): huella(p) for p in (destino / componente).rglob("*") if p.is_file()}
        if originales != actuales:
            raise RuntimeError("Se detectó un cambio fuera del frontend.")
    temporal.symlink_to(destino, target_is_directory=True)
    os.replace(temporal, enlace)
    print(json.dumps({"Version": args.version, "VersionAnterior": anterior.name,
                      "FrontendPublicado": True, "ApiSinCambios": True}, indent=2))


if __name__ == "__main__":
    try:
        principal()
    except (OSError, RuntimeError, ValueError, KeyError) as error:
        raise SystemExit(str(error))
