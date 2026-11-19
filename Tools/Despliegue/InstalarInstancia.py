#!/usr/bin/env python3
"""Primera instalación aislada en una VM Ubuntu ya inspeccionada.

No sustituye sitios existentes ni restaura encima de una base poblada.
Los secretos se generan en el servidor y nunca se reciben por argumentos.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import secrets
import shutil
import subprocess
import tarfile


def ejecutar(argumentos, entrada=None, entorno=None):
    resultado = subprocess.run(argumentos, input=entrada, text=True,
                               capture_output=True, env=entorno, check=False)
    if resultado.returncode:
        # El error bruto de psql u otro proceso puede contener datos privados.
        raise RuntimeError(f"Falló {Path(argumentos[0]).name} (código {resultado.returncode}).")
    return resultado.stdout.strip()


def sql(consulta, base="postgres"):
    return ejecutar(["runuser", "-u", "postgres", "--", "psql", "-X", "-q", "-A", "-t",
                     "-v", "ON_ERROR_STOP=1", "-d", base], entrada=consulta)


def huella(ruta):
    with ruta.open("rb") as archivo:
        return hashlib.file_digest(archivo, "sha256").hexdigest()


def privado(ruta, contenido, modo=0o600, grupo=0):
    descriptor = os.open(ruta, os.O_WRONLY | os.O_CREAT | os.O_EXCL, modo)
    with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as archivo:
        archivo.write(contenido)
    os.chown(ruta, 0, grupo)


def ruta_hija(raiz, relativa):
    destino = (raiz / relativa).resolve()
    if not destino.is_relative_to(raiz.resolve()) or destino == raiz.resolve():
        raise RuntimeError("Ruta de archivo fuera de la carpeta autorizada.")
    return destino


def principal():
    opciones = argparse.ArgumentParser(description=__doc__)
    opciones.add_argument("--paquete", type=Path, required=True)
    opciones.add_argument("--sha256", required=True)
    opciones.add_argument("--respaldo", type=Path, required=True)
    opciones.add_argument("--dominio", required=True)
    opciones.add_argument("--puerto-api", type=int, default=5180)
    opciones.add_argument("--base", default="constructora_eder_demo")
    args = opciones.parse_args()
    if os.geteuid() != 0:
        raise RuntimeError("Ejecutar con sudo.")
    if not re.fullmatch(r"[a-z0-9][a-z0-9.-]{0,251}[a-z0-9]", args.dominio):
        raise RuntimeError("Dominio inválido.")
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,62}", args.base) or not 1024 <= args.puerto_api <= 65535:
        raise RuntimeError("Base o puerto inválidos.")
    paquete = args.paquete.resolve(strict=True)
    respaldo = args.respaldo.resolve(strict=True)
    version = paquete.name.removesuffix(".tar.gz")
    if not re.fullmatch(r"\d{8}T\d{6}Z-[a-f0-9]{8}", version) or huella(paquete) != args.sha256:
        raise RuntimeError("Nombre o SHA-256 del paquete incorrectos.")
    raiz = Path("/opt/constructora-eder-torres")
    configuracion = Path("/etc/constructora-eder-torres")
    datos = Path("/var/lib/constructora-eder-torres")
    if any(r.exists() for r in (configuracion, datos, raiz / "actual")):
        raise RuntimeError("Ya hay una instalación o preparación de CET. Revisar antes de continuar.")
    if sql(f"SELECT count(*) FROM pg_database WHERE datname = '{args.base}';") != "0":
        raise RuntimeError("La base de destino ya existe.")
    if sql("SELECT count(*) FROM pg_roles WHERE rolname IN ('cet_propietario','cet_api','cet_respaldo');") != "0":
        raise RuntimeError("Ya existe una identidad PostgreSQL del despliegue.")
    if ejecutar(["ss", "-H", "-lnt", f"sport = :{args.puerto_api}"]):
        raise RuntimeError("El puerto interno elegido está ocupado.")
    runtimes = ejecutar(["/usr/bin/dotnet", "--list-runtimes"])
    if "Microsoft.AspNetCore.App 10.0." not in runtimes:
        raise RuntimeError("Falta el runtime ASP.NET Core 10.")

    release = raiz / "releases" / version
    release.mkdir(parents=True)
    with tarfile.open(paquete, "r:gz") as comprimido:
        if any(m.issym() or m.islnk() for m in comprimido.getmembers()):
            raise RuntimeError("No se admiten enlaces en el paquete.")
        comprimido.extractall(release, filter="data")
    manifiesto = json.loads((release / "manifiesto.json").read_text(encoding="utf-8-sig"))
    for archivo in manifiesto["Archivos"]:
        destino = ruta_hija(release, archivo["Ruta"])
        if destino.stat().st_size != archivo["Bytes"] or huella(destino) != archivo["Sha256"]:
            raise RuntimeError("Un archivo de publicación no coincide con el manifiesto.")
    for carpeta, subcarpetas, archivos in os.walk(release):
        os.chmod(carpeta, 0o755)
        for nombre in archivos:
            os.chmod(Path(carpeta) / nombre, 0o644)
    operacion = release / "Operacion"
    ejecutar(["dotnet", str(release / "Respaldos/ConstructoraEderTorres.Respaldos.dll"), "verificar",
              "--carpeta", str(respaldo), "--binarios", "/usr/bin"])

    ejecutar(["useradd", "--system", "--user-group", "--home-dir", "/nonexistent", "--no-create-home",
              "--shell", "/usr/sbin/nologin", "cet-api"])
    identidad = pwd.getpwnam("cet-api")
    configuracion.mkdir(mode=0o750)
    os.chown(configuracion, 0, identidad.pw_gid)
    datos.mkdir(mode=0o755)
    for nombre in ("evidencias", "claves"):
        carpeta = datos / nombre
        carpeta.mkdir(mode=0o700)
        os.chown(carpeta, identidad.pw_uid, identidad.pw_gid)
    (datos / "acme/.well-known/acme-challenge").mkdir(parents=True, mode=0o755)

    clave_api = secrets.token_hex(32)
    clave_respaldo = secrets.token_hex(32)
    sql("CREATE ROLE cet_propietario NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;\n"
        f"CREATE ROLE cet_api LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 25 PASSWORD '{clave_api}';\n"
        f"CREATE ROLE cet_respaldo LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS CONNECTION LIMIT 5 PASSWORD '{clave_respaldo}';\n"
        f"CREATE DATABASE {args.base} OWNER cet_propietario ENCODING 'UTF8' TEMPLATE template0;\n"
        f"REVOKE ALL ON DATABASE {args.base} FROM PUBLIC;\n")
    sql("ALTER SCHEMA public OWNER TO cet_propietario;", args.base)
    # Restaurar como propietario dedicado evita reasignar objetos del usuario postgres de otras bases.
    with (respaldo / "base.dump").open("rb") as archivo:
        resultado = subprocess.run(["runuser", "-u", "postgres", "--", "pg_restore", "--dbname", args.base,
                                   "--role", "cet_propietario", "--single-transaction", "--exit-on-error",
                                   "--no-owner", "--no-acl"], stdin=archivo, capture_output=True)
        if resultado.returncode:
            raise RuntimeError("No se pudo restaurar en la base nueva.")
    ejecutar(["runuser", "-u", "postgres", "--", "psql", "-X", "-q", "-v", "ON_ERROR_STOP=1",
              "-v", "rol_ejecucion=cet_api", "-v", "rol_propietario=cet_propietario", "-d", args.base],
             entrada=(operacion / "ConfigurarRolEjecucion.sql").read_text(encoding="utf-8-sig"))
    sql("GRANT CONNECT ON DATABASE " + args.base + " TO cet_respaldo;\n"
        "GRANT USAGE ON SCHEMA public TO cet_respaldo;\n"
        "GRANT SELECT ON ALL TABLES IN SCHEMA public TO cet_respaldo;\n"
        "GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO cet_respaldo;\n"
        "ALTER DEFAULT PRIVILEGES FOR ROLE cet_propietario IN SCHEMA public GRANT SELECT ON TABLES TO cet_respaldo;\n"
        "ALTER DEFAULT PRIVILEGES FOR ROLE cet_propietario IN SCHEMA public GRANT SELECT ON SEQUENCES TO cet_respaldo;\n"
        "UPDATE public.sesiones_usuario SET revocada_en_utc = GREATEST(now(), emitida_en_utc) WHERE revocada_en_utc IS NULL;",
        args.base)
    copia = json.loads((respaldo / "manifiesto.json").read_text(encoding="utf-8-sig"))
    for evidencia in copia["Evidencias"]:
        origen = ruta_hija(respaldo / "evidencias", evidencia["RutaRelativa"])
        destino = ruta_hija(datos / "evidencias", evidencia["RutaRelativa"])
        destino.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        shutil.copyfile(origen, destino)
        os.chmod(destino, 0o600)
        if huella(destino) != evidencia["HuellaSha256"]:
            raise RuntimeError("Una fotografía no coincide con su huella.")
    for carpeta, subcarpetas, archivos in os.walk(datos / "evidencias"):
        os.chown(carpeta, identidad.pw_uid, identidad.pw_gid)
        for nombre in archivos:
            os.chown(Path(carpeta) / nombre, identidad.pw_uid, identidad.pw_gid)

    cert_clave = configuracion / "sesiones.key"
    cert_publico = configuracion / "sesiones.crt"
    cert_pfx = configuracion / "sesiones.pfx"
    ejecutar(["openssl", "req", "-x509", "-newkey", "rsa:3072", "-sha256", "-days", "1095", "-nodes",
              "-subj", "/CN=ConstructoraEderTorresDemo-DataProtection", "-keyout", str(cert_clave), "-out", str(cert_publico)])
    ejecutar(["openssl", "pkcs12", "-export", "-out", str(cert_pfx), "-inkey", str(cert_clave),
              "-in", str(cert_publico), "-passout", "pass:"])
    os.chmod(cert_pfx, 0o640)
    os.chown(cert_pfx, 0, identidad.pw_gid)
    cert_clave.unlink()
    cert_publico.unlink()

    conexion = f"Host=127.0.0.1;Port=5432;Database={args.base};Username=cet_api;Password={clave_api};Maximum Pool Size=20;Timeout=10;Command Timeout=30"
    entorno = (operacion / "entorno.example").read_text(encoding="utf-8-sig")
    entorno = entorno.replace("@@DOMINIO@@", args.dominio).replace("@@PUERTO_API@@", str(args.puerto_api)).replace("@@CONEXION@@", conexion)
    privado(configuracion / "entorno", entorno)
    conexion_respaldo = f"Host=127.0.0.1;Port=5432;Database={args.base};Username=cet_respaldo;Password={clave_respaldo};Maximum Pool Size=3;Timeout=10;Command Timeout=120"
    privado(configuracion / "respaldo", f'ConnectionStrings__Respaldo="{conexion_respaldo}"\n')
    shutil.copyfile(operacion / "cabeceras-web.conf", configuracion / "cabeceras-web.conf")
    os.chmod(configuracion / "cabeceras-web.conf", 0o644)
    # Nginx necesita atravesar este directorio para leer exclusivamente el fragmento público.
    os.chmod(configuracion, 0o755)
    shutil.copyfile(operacion / "constructora-eder-torres.service", "/etc/systemd/system/constructora-eder-torres.service")
    (raiz / "actual").symlink_to(release, target_is_directory=True)
    ejecutar(["systemctl", "daemon-reload"])
    ejecutar(["systemctl", "enable", "--now", "constructora-eder-torres.service"])
    print(json.dumps({"Version": version, "Base": args.base, "PuertoInterno": args.puerto_api,
                      "ArchivosVerificados": len(manifiesto["Archivos"]), "FotografiasRestauradas": len(copia["Evidencias"]),
                      "ServicioCreado": True, "SitioPublicoConfigurado": False}, indent=2))


if __name__ == "__main__":
    try:
        principal()
    except (OSError, RuntimeError, ValueError, KeyError) as error:
        raise SystemExit(str(error))
