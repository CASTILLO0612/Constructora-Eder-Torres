# Mapeos del modelo fisico

Este directorio recibira los mapeos de EF Core de las 38 tablas PostgreSQL cuando el esquema de la instancia actual se haya cotejado con el SQL versionado.

La referencia disponible es `Documentacion/CORTE I/Modelo Fisico Constructora Eder Torres.sql`. El contexto registra que los cambios de `estado` en `incidencias_herramienta` y las restricciones entre obras requieren conciliacion con la base activa.

El contexto EF Core inicial no declara entidades ni genera tablas. Antes de crear una migracion se debe incorporar el esquema conciliado y acordar la linea de base de la base existente.
