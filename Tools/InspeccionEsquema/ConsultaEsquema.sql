WITH tablas AS (
    SELECT relacion.oid, relacion.relname, relacion.relkind,
           relacion.relispartition, relacion.relrowsecurity, relacion.relforcerowsecurity
    FROM pg_catalog.pg_class AS relacion
    JOIN pg_catalog.pg_namespace AS espacio ON espacio.oid = relacion.relnamespace
    WHERE espacio.nspname = $1 AND relacion.relkind IN ('r', 'p')
)
SELECT pg_catalog.jsonb_build_object(
    'versionFormato', 1,
    'capturadoEnUtc', pg_catalog.timezone('UTC', CURRENT_TIMESTAMP),
    'baseDatos', pg_catalog.current_database(),
    'versionPostgreSql', pg_catalog.current_setting('server_version'),
    'esquema', $1,
    'soloLectura', pg_catalog.current_setting('transaction_read_only') = 'on',
    'esquemaExiste', EXISTS (
        SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = $1
    ),
    'puedeUsarEsquema', COALESCE((
        SELECT pg_catalog.has_schema_privilege(oid, 'USAGE')
        FROM pg_catalog.pg_namespace WHERE nspname = $1
    ), false),
    'extensiones', COALESCE((
        SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
            'nombre', extension.extname, 'version', extension.extversion
        ) ORDER BY extension.extname)
        FROM pg_catalog.pg_extension AS extension
    ), '[]'::pg_catalog.jsonb),
    'tablas', COALESCE((
        SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
            'nombre', tabla.relname,
            'particionada', tabla.relkind = 'p',
            'esParticion', tabla.relispartition,
            'seguridadPorFila', tabla.relrowsecurity,
            'seguridadPorFilaForzada', tabla.relforcerowsecurity,
            'columnas', COALESCE((
                SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
                    'nombre', atributo.attname,
                    'posicion', atributo.attnum,
                    'tipo', pg_catalog.format_type(atributo.atttypid, atributo.atttypmod),
                    'obligatoria', atributo.attnotnull,
                    'identidad', atributo.attidentity::pg_catalog.text,
                    'generada', atributo.attgenerated::pg_catalog.text,
                    'valorPredeterminado', pg_catalog.pg_get_expr(predeterminado.adbin, predeterminado.adrelid)
                ) ORDER BY atributo.attnum)
                FROM pg_catalog.pg_attribute AS atributo
                LEFT JOIN pg_catalog.pg_attrdef AS predeterminado
                    ON predeterminado.adrelid = atributo.attrelid AND predeterminado.adnum = atributo.attnum
                WHERE atributo.attrelid = tabla.oid AND atributo.attnum > 0 AND NOT atributo.attisdropped
            ), '[]'::pg_catalog.jsonb),
            'restricciones', COALESCE((
                SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
                    'nombre', restriccion.conname,
                    'tipo', restriccion.contype::pg_catalog.text,
                    'definicion', pg_catalog.pg_get_constraintdef(restriccion.oid, false),
                    'validada', restriccion.convalidated,
                    'diferible', restriccion.condeferrable,
                    'diferidaInicialmente', restriccion.condeferred,
                    'tablaReferenciada', CASE WHEN restriccion.confrelid <> 0
                        THEN restriccion.confrelid::pg_catalog.regclass::pg_catalog.text END,
                    'columnas', COALESCE((
                        SELECT pg_catalog.jsonb_agg(atributo.attname ORDER BY clave.orden)
                        FROM pg_catalog.unnest(restriccion.conkey) WITH ORDINALITY AS clave(numero, orden)
                        JOIN pg_catalog.pg_attribute AS atributo
                            ON atributo.attrelid = restriccion.conrelid AND atributo.attnum = clave.numero
                    ), '[]'::pg_catalog.jsonb),
                    'columnasReferenciadas', COALESCE((
                        SELECT pg_catalog.jsonb_agg(atributo.attname ORDER BY clave.orden)
                        FROM pg_catalog.unnest(restriccion.confkey) WITH ORDINALITY AS clave(numero, orden)
                        JOIN pg_catalog.pg_attribute AS atributo
                            ON atributo.attrelid = restriccion.confrelid AND atributo.attnum = clave.numero
                    ), '[]'::pg_catalog.jsonb)
                ) ORDER BY restriccion.conname)
                FROM pg_catalog.pg_constraint AS restriccion WHERE restriccion.conrelid = tabla.oid
            ), '[]'::pg_catalog.jsonb),
            'indices', COALESCE((
                SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
                    'nombre', relacionIndice.relname,
                    'definicion', pg_catalog.pg_get_indexdef(indice.indexrelid),
                    'unico', indice.indisunique,
                    'primario', indice.indisprimary,
                    'valido', indice.indisvalid,
                    'listo', indice.indisready
                ) ORDER BY relacionIndice.relname)
                FROM pg_catalog.pg_index AS indice
                JOIN pg_catalog.pg_class AS relacionIndice ON relacionIndice.oid = indice.indexrelid
                WHERE indice.indrelid = tabla.oid
            ), '[]'::pg_catalog.jsonb),
            'disparadores', COALESCE((
                SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
                    'nombre', disparador.tgname,
                    'habilitacion', disparador.tgenabled::pg_catalog.text,
                    'definicion', pg_catalog.pg_get_triggerdef(disparador.oid, false),
                    'funcion', disparador.tgfoid::pg_catalog.regprocedure::pg_catalog.text
                ) ORDER BY disparador.tgname)
                FROM pg_catalog.pg_trigger AS disparador
                WHERE disparador.tgrelid = tabla.oid AND NOT disparador.tgisinternal
            ), '[]'::pg_catalog.jsonb)
        ) ORDER BY tabla.relname)
        FROM tablas AS tabla
    ), '[]'::pg_catalog.jsonb),
    'funcionesDisparador', COALESCE((
        SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
            'nombre', funcion.oid::pg_catalog.regprocedure::pg_catalog.text,
            'definicion', pg_catalog.pg_get_functiondef(funcion.oid)
        ) ORDER BY funcion.oid::pg_catalog.regprocedure::pg_catalog.text)
        FROM pg_catalog.pg_proc AS funcion
        WHERE funcion.oid IN (
            SELECT disparador.tgfoid FROM pg_catalog.pg_trigger AS disparador
            JOIN tablas AS tabla ON tabla.oid = disparador.tgrelid WHERE NOT disparador.tgisinternal
        )
    ), '[]'::pg_catalog.jsonb),
    'secuencias', COALESCE((
        SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
            'nombre', relacion.relname,
            'tipo', pg_catalog.format_type(secuencia.seqtypid, NULL),
            'inicio', secuencia.seqstart,
            'incremento', secuencia.seqincrement,
            'minimo', secuencia.seqmin,
            'maximo', secuencia.seqmax,
            'cache', secuencia.seqcache,
            'ciclica', secuencia.seqcycle
        ) ORDER BY relacion.relname)
        FROM pg_catalog.pg_sequence AS secuencia
        JOIN pg_catalog.pg_class AS relacion ON relacion.oid = secuencia.seqrelid
        JOIN pg_catalog.pg_namespace AS espacio ON espacio.oid = relacion.relnamespace
        WHERE espacio.nspname = $1
    ), '[]'::pg_catalog.jsonb),
    'vistas', COALESCE((
        SELECT pg_catalog.jsonb_agg(pg_catalog.jsonb_build_object(
            'nombre', relacion.relname,
            'materializada', relacion.relkind = 'm',
            'definicion', pg_catalog.pg_get_viewdef(relacion.oid, false)
        ) ORDER BY relacion.relname)
        FROM pg_catalog.pg_class AS relacion
        JOIN pg_catalog.pg_namespace AS espacio ON espacio.oid = relacion.relnamespace
        WHERE espacio.nspname = $1 AND relacion.relkind IN ('v', 'm')
    ), '[]'::pg_catalog.jsonb)
);
