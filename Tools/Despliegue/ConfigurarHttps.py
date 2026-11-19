#!/usr/bin/env python3
"""Configura exclusivamente el sitio Nginx de Constructora Eder Torres."""
import argparse
import os
from pathlib import Path
import re
import subprocess

MARCA = "# Sitio administrado para Constructora Eder Torres\n"
SITIO = Path("/etc/nginx/sites-available/constructora-eder-torres")
ENLACE = Path("/etc/nginx/sites-enabled/constructora-eder-torres")
ACME = Path("/var/lib/constructora-eder-torres/acme")


def comando(args):
    resultado = subprocess.run(args, capture_output=True, text=True)
    if resultado.returncode:
        raise RuntimeError(f"Falló {args[0]}: {resultado.stderr.strip()}")
    return resultado


def validar_nginx():
    resultado = subprocess.run(["nginx", "-t"], capture_output=True, text=True)
    if resultado.returncode and "server_names_hash_bucket_size" in resultado.stderr:
        ajuste = Path("/etc/nginx/conf.d/constructora-eder-torres-nombres.conf")
        if not ajuste.exists():
            ajuste.write_text("server_names_hash_bucket_size 128;\n", encoding="utf-8")
    comando(["nginx", "-t"])


def principal():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fase", choices=("bootstrap", "activar"), required=True)
    parser.add_argument("--dominio", required=True)
    parser.add_argument("--puerto-api", type=int, default=5180)
    args = parser.parse_args()
    if os.geteuid() != 0:
        raise RuntimeError("Ejecutar con sudo.")
    if not re.fullmatch(r"[a-z0-9][a-z0-9.-]{0,251}[a-z0-9]", args.dominio) or not 1024 <= args.puerto_api <= 65535:
        raise RuntimeError("Dominio o puerto inválidos.")
    os.umask(0o022)
    if SITIO.exists() and not SITIO.read_text().startswith(MARCA):
        raise RuntimeError("El sitio existente no pertenece a este instalador.")
    if ENLACE.is_symlink() and ENLACE.resolve() != SITIO:
        raise RuntimeError("El enlace existente apunta a otro sitio.")
    if ENLACE.exists() and not ENLACE.is_symlink():
        raise RuntimeError("La configuración activa existente no es el enlace esperado.")
    if args.fase == "bootstrap":
        if SITIO.exists():
            raise RuntimeError("El sitio ya existe. Revisar antes de sobrescribir.")
        if not ACME.is_dir():
            raise RuntimeError("La instalación de la API debe completarse primero.")
        contenido = MARCA + """server {
    listen 80;
    server_name @@DOMINIO@@;
    access_log off;
    location ^~ /.well-known/acme-challenge/ {
        root /var/lib/constructora-eder-torres/acme;
        default_type text/plain;
        try_files $uri =404;
    }
    location / { return 503; }
}
"""
        SITIO.write_text(contenido.replace("@@DOMINIO@@", args.dominio), encoding="utf-8")
        ENLACE.symlink_to(SITIO)
        try:
            validar_nginx()
        except RuntimeError:
            ENLACE.unlink()
            SITIO.unlink()
            raise
        comando(["systemctl", "reload", "nginx"])
        prueba = ACME / ".well-known/acme-challenge/comprobacion-cet"
        prueba.write_text("constructora-eder-torres-acme-ok\n", encoding="ascii")
        print("Sitio HTTP preparado para comprobar el desafío ACME.")
        return

    if not SITIO.exists() or not ENLACE.is_symlink():
        raise RuntimeError("Falta la configuración HTTP inicial.")
    comando(["certbot", "certonly", "--non-interactive", "--webroot", "--webroot-path", str(ACME),
             "--domain", args.dominio, "--cert-name", args.dominio, "--keep-until-expiring"])
    plantilla = Path("/opt/constructora-eder-torres/actual/Operacion/nginx.conf.template")
    contenido = MARCA + plantilla.read_text(encoding="utf-8-sig").replace("@@DOMINIO@@", args.dominio).replace("@@PUERTO_API@@", str(args.puerto_api))
    anterior = SITIO.read_text()
    SITIO.write_text(contenido, encoding="utf-8")
    try:
        validar_nginx()
    except RuntimeError:
        SITIO.write_text(anterior, encoding="utf-8")
        raise
    comando(["systemctl", "reload", "nginx"])
    renovacion = Path("/etc/letsencrypt/renewal-hooks/deploy/constructora-eder-torres-recargar-nginx")
    if not renovacion.exists():
        renovacion.write_text("#!/bin/sh\n/usr/sbin/nginx -t && /usr/bin/systemctl reload nginx\n", encoding="ascii")
        renovacion.chmod(0o750)
    comando(["systemctl", "enable", "--now", "certbot.timer"])
    prueba = ACME / ".well-known/acme-challenge/comprobacion-cet"
    prueba.unlink(missing_ok=True)
    print("Sitio HTTPS activado con certificado y renovación programada.")


if __name__ == "__main__":
    try:
        principal()
    except (OSError, RuntimeError) as error:
        raise SystemExit(str(error))
