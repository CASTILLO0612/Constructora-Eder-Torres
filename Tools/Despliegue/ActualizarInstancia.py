#!/usr/bin/env python3
"""Actualiza una instancia existente con respaldo, migración aditiva y retorno a la versión anterior."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tarfile
import time
import urllib.parse
import urllib.request


def huella(ruta):
    with ruta.open("rb") as archivo:
        return hashlib.file_digest(archivo, "sha256").hexdigest()


def ejecutar(argumentos, entrada=None):
    resultado = subprocess.run(argumentos, input=entrada, text=True, capture_output=True, check=False)
    if resultado.returncode:
        raise RuntimeError(f"Falló {Path(argumentos[0]).name} (código {resultado.returncode}).")
    return resultado.stdout.strip()


def miembros_seguros(paquete):
    miembros = paquete.getmembers()
    nombres = set()
    for miembro in miembros:
        ruta = PurePosixPath(miembro.name)
        if ruta.is_absolute() or ".." in ruta.parts or "\\" in miembro.name or not (miembro.isfile() or miembro.isdir()):
            raise ValueError("El paquete contiene rutas o elementos no admitidos.")
        if miembro.isfile():
            if ruta.as_posix() in nombres:
                raise ValueError("El paquete contiene archivos duplicados.")
            nombres.add(ruta.as_posix())
            if ruta.parts[0] not in {"Api", "Web", "Operacion", "manifiesto.json"}:
                raise ValueError("El paquete incluye un componente no permitido.")
            if ruta.suffix.lower() in {".pem", ".key", ".pfx", ".p12", ".dump"} or ruta.name.startswith(".env") or ruta.name in {"secrets.json", "appsettings.Development.json"}:
                raise ValueError("El paquete contiene material privado o configuración local.")
    if sum(miembro.size for miembro in miembros) > 512 * 1024 * 1024:
        raise ValueError("El paquete excede el tamaño de publicación permitido.")
    obligatorios = {"Api/ConstructoraEderTorres.Presentacion.dll", "Web/index.html", "Web/sw.js", "manifiesto.json"}
    if not obligatorios.issubset(nombres):
        raise ValueError("Faltan componentes de la publicación.")
    return miembros


def saludable(url):
    for _ in range(30):
        try:
            with urllib.request.urlopen(url, timeout=2) as respuesta:
                if respuesta.status == 200:
                    return True
        except (OSError, ValueError):
            pass
        time.sleep(0.5)
    return False


def principal():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--paquete", type=Path, required=True)
    parser.add_argument("--sha256", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--base", required=True)
    parser.add_argument("--propietario", required=True)
    parser.add_argument("--raiz", type=Path, required=True)
    parser.add_argument("--servicio", required=True)
    parser.add_argument("--respaldo", required=True)
    parser.add_argument("--salud", required=True)
    args = parser.parse_args()
    if os.geteuid() != 0 or not re.fullmatch(r"\d{8}T\d{6}Z-[a-f0-9]{8}", args.version):
        raise RuntimeError("Requiere root y un identificador de versión válido.")
    for identificador in (args.base, args.propietario):
        if not re.fullmatch(r"[a-z_][a-z0-9_]*", identificador):
            raise ValueError("Identificador PostgreSQL inválido.")
    for servicio in (args.servicio, args.respaldo):
        if not re.fullmatch(r"[a-z0-9-]+\.service", servicio):
            raise ValueError("Nombre de servicio inválido.")
    url = urllib.parse.urlparse(args.salud)
    if url.scheme != "https" or not url.hostname or url.username or url.password or url.query or url.fragment:
        raise ValueError("La comprobación requiere la dirección HTTPS de la aplicación, sin credenciales ni parámetros.")
    if huella(args.paquete) != args.sha256:
        raise RuntimeError("El paquete no coincide con su SHA-256.")
    raiz = args.raiz.resolve(strict=True)
    versiones = (raiz / "releases").resolve(strict=True)
    enlace = raiz / "actual"
    anterior = enlace.resolve(strict=True)
    if versiones.parent != raiz or anterior.parent != versiones or not enlace.is_symlink():
        raise RuntimeError("La instancia no tiene la estructura de publicaciones esperada.")
    destino = versiones / args.version
    temporal = raiz / (".actual-" + args.version)
    if destino.exists() or temporal.exists() or temporal.is_symlink():
        raise RuntimeError("El destino de la actualización está ocupado.")
    with tarfile.open(args.paquete, "r:gz") as paquete:
        miembros = miembros_seguros(paquete)
        ejecutar(["systemctl", "start", args.respaldo])
        if ejecutar(["systemctl", "show", args.respaldo, "--property=Result", "--value"]) != "success":
            raise RuntimeError("El respaldo previo no se completó.")
        destino.mkdir()
        for componente in ("Respaldos", "Operacion"):
            shutil.copytree(anterior / componente, destino / componente)
        paquete.extractall(destino, members=miembros, filter="data")
    manifiesto = json.loads((destino / "manifiesto.json").read_text(encoding="utf-8-sig"))
    if manifiesto["Version"] != args.version:
        raise RuntimeError("La versión no coincide con el manifiesto.")
    for archivo in manifiesto["Archivos"]:
        ruta = (destino / archivo["Ruta"]).resolve(strict=True)
        if not ruta.is_relative_to(destino) or ruta.stat().st_size != archivo["Bytes"] or huella(ruta) != archivo["Sha256"]:
            raise RuntimeError("Un archivo no coincide con su manifiesto.")
    migracion = destino / "Operacion" / "migracion.sql"
    if not migracion.is_file():
        raise RuntimeError("Falta la migración de la publicación.")
    # Las pestañas abiertas conservan acceso a sus chunks durante la actualización de la PWA.
    for archivo in (anterior / "Web" / "assets").iterdir():
        if archivo.is_file() and not (destino / "Web" / "assets" / archivo.name).exists():
            shutil.copyfile(archivo, destino / "Web" / "assets" / archivo.name)
    sql = f'SET ROLE "{args.propietario}"; SET lock_timeout = \'5s\'; SET statement_timeout = \'120s\';\n'
    sql += migracion.read_text(encoding="utf-8-sig")
    ejecutar(["runuser", "-u", "postgres", "--", "psql", "-X", "-q", "-v", "ON_ERROR_STOP=1", "-d", args.base], sql)
    manifiesto["VersionBase"] = anterior.name
    manifiesto["ActualizacionCompleta"] = True
    manifiesto["Archivos"] = []
    for carpeta, _, archivos in os.walk(destino):
        os.chmod(carpeta, 0o755)
        for nombre in sorted(archivos):
            archivo = Path(carpeta) / nombre
            os.chmod(archivo, 0o644)
            if archivo.name != "manifiesto.json":
                manifiesto["Archivos"].append({"Ruta": archivo.relative_to(destino).as_posix(), "Bytes": archivo.stat().st_size, "Sha256": huella(archivo)})
    (destino / "manifiesto.json").write_text(json.dumps(manifiesto, indent=2), encoding="utf-8")
    temporal.symlink_to(destino, target_is_directory=True)
    os.replace(temporal, enlace)
    try:
        ejecutar(["systemctl", "restart", args.servicio])
        if not saludable(args.salud):
            raise RuntimeError("La nueva versión no superó la comprobación de disponibilidad.")
    except RuntimeError:
        temporal.symlink_to(anterior, target_is_directory=True)
        os.replace(temporal, enlace)
        ejecutar(["systemctl", "restart", args.servicio])
        if not saludable(args.salud):
            raise RuntimeError("Se restauró el enlace anterior; el servicio requiere revisión.") from None
        raise RuntimeError("Se restableció la versión anterior. La migración aditiva se conserva.") from None
    print(json.dumps({"Version": args.version, "VersionAnterior": anterior.name, "Publicada": True, "Disponible": True}, indent=2))


if __name__ == "__main__":
    try:
        principal()
    except (OSError, RuntimeError, ValueError, KeyError, tarfile.TarError) as error:
        raise SystemExit(str(error))
