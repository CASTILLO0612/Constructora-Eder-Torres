# Mapeos del modelo fisico

Este directorio contiene los 38 mapeos de negocio del esquema PostgreSQL conciliado el 28 de octubre de 2026 y el mapeo técnico de sesiones_usuario incorporado el 30 de octubre. Se organizan por modulo y se registran desde ContextoConstructora.

La referencia de partida es `database/EsquemaConciliadoPostgreSql.sql`; las migraciones posteriores incorporan geocerca, precisión GPS y sesiones revocables. El DDL original utilizado en pruebas se conserva en `Tests/Integracion/Datos/EsquemaOriginalPostgreSql.sql`. La columna `estado` de `incidencias_herramienta` y los 12 disparadores de control entre obras están reflejados en el modelo.

La preparación de la base y su evolución están documentadas en `database/README.md`. Las pruebas comparan los mapeos con un inventario independiente de las 38 tablas de negocio. La API no crea tablas ni aplica migraciones al iniciar.
