\set ON_ERROR_STOP on
-- Ejecutar como propietario/migrador en la base elegida. Variables psql:
-- -v rol_ejecucion=NOMBRE_LOGIN -v rol_propietario=NOMBRE_PROPIETARIO
-- La contraseña se establece por separado con \password; nunca en este archivo.
SELECT (:'rol_ejecucion' = :'rol_propietario' OR :'rol_ejecucion' = current_user
    OR EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname = :'rol_ejecucion'
        AND (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls))) AS rol_inseguro \gset
\if :rol_inseguro
    \echo 'El rol de ejecución debe ser independiente y carecer de privilegios administrativos.'
    \quit 1
\endif
BEGIN;
SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS', :'rol_ejecucion')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'rol_ejecucion') \gexec
SELECT format('ALTER ROLE %I NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT', :'rol_ejecucion') \gexec
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SELECT format('REVOKE ALL ON SCHEMA public FROM %I', :'rol_ejecucion') \gexec
SELECT format('GRANT USAGE ON SCHEMA public TO %I', :'rol_ejecucion') \gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'rol_ejecucion') \gexec
SELECT format('REVOKE CREATE ON DATABASE %I FROM %I', current_database(), :'rol_ejecucion') \gexec
SELECT format('REVOKE ALL ON ALL TABLES IN SCHEMA public FROM %I', :'rol_ejecucion') \gexec
SELECT format('GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO %I', :'rol_ejecucion') \gexec
SELECT format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM %I', :'rol_ejecucion') \gexec
SELECT format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO %I', :'rol_ejecucion') \gexec
SELECT format('REVOKE INSERT, UPDATE ON public."__EFMigrationsHistory" FROM %I', :'rol_ejecucion') \gexec
SELECT format('REVOKE UPDATE ON public.auditorias FROM %I', :'rol_ejecucion') \gexec
SELECT format('GRANT DELETE ON public.roles_permiso TO %I', :'rol_ejecucion') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO %I',
    :'rol_propietario', :'rol_ejecucion') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO %I',
    :'rol_propietario', :'rol_ejecucion') \gexec
COMMIT;
