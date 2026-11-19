#!/usr/bin/env bash
# Inspección de solo lectura; no muestra secretos, variables de entorno ni contenidos de bases.
set -u
printf '\n== Sistema y recursos ==\n'
uname -m
cat /etc/os-release
free -m
df -h / /var
printf '\n== Servicios de aplicaciones ==\n'
systemctl list-units --type=service --state=running --no-pager --plain |
    grep -Ei 'nginx|apache|caddy|docker|containerd|postgres|dotnet|kestrel|ruta|constructora' || true
printf '\n== Puertos en escucha ==\n'
ss -lnt
printf '\n== Versiones disponibles ==\n'
command -v dotnet >/dev/null && dotnet --list-runtimes
command -v nginx >/dev/null && nginx -v
command -v psql >/dev/null && psql --version
command -v certbot >/dev/null && certbot --version
printf '\n== Nombres de sitios y aplicaciones ==\n'
find /etc/nginx/sites-enabled /opt /var/www -mindepth 1 -maxdepth 1 -printf '%p\n' 2>/dev/null || true
printf '\n== Certificados instalados (nombres) ==\n'
find /etc/letsencrypt/live -mindepth 1 -maxdepth 1 -type d -printf '%f\n' 2>/dev/null || true
printf '\n== Bases existentes (nombres y propietarios) ==\n'
if command -v psql >/dev/null; then
    sudo -n -u postgres psql -X -Atc 'SELECT datname, pg_get_userbyid(datdba) FROM pg_database WHERE NOT datistemplate;' 2>/dev/null || true
fi
