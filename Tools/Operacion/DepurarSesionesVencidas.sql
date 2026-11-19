\set ON_ERROR_STOP on
-- Mantenimiento técnico con una cuenta autorizada para DELETE en sesiones_usuario.
-- Variable psql obligatoria: -v limite_sesiones=NUMERO_POSITIVO
-- No elimina auditorías, cuentas ni sesiones cuya expiración absoluta siga vigente.
\if :{?limite_sesiones}
\else
    \echo 'Debe indicar limite_sesiones como entero positivo.'
    \quit 1
\endif
SELECT (:'limite_sesiones'::integer > 0) AS limite_valido \gset
\if :limite_valido
\else
    \echo 'limite_sesiones debe ser positivo.'
    \quit 1
\endif

BEGIN;
WITH vencidas AS (
    SELECT id_sesion
    FROM public.sesiones_usuario
    WHERE expira_en_utc <= CURRENT_TIMESTAMP
    ORDER BY id_usuario, expira_en_utc
    LIMIT :'limite_sesiones'::integer
    FOR UPDATE SKIP LOCKED
), eliminadas AS (
    DELETE FROM public.sesiones_usuario AS sesion
    USING vencidas
    WHERE sesion.id_sesion = vencidas.id_sesion
    RETURNING sesion.id_sesion
)
SELECT COUNT(*) AS sesiones_vencidas_eliminadas FROM eliminadas;
COMMIT;
