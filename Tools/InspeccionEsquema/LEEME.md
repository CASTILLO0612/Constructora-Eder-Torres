# Inspección de esquema PostgreSQL

Herramienta de desarrollo para CET-10. Usa Npgsql y la misma configuración local de conexión de Presentación; no forma parte de los puntos de acceso de la API.

Desde la raíz del repositorio:

```powershell
dotnet run --project Tools/InspeccionEsquema -- --esquema public --salida artifacts/Conciliacion/esquema-postgresql.json
```

El esquema y la ruta se reciben como argumentos. La conexión se toma de `ConnectionStrings:Constructora` en secretos de usuario; `ConnectionStrings__Constructora` en el entorno tiene precedencia. Los tiempos de conexión y consulta se pueden establecer en esa cadena mediante las opciones oficiales de Npgsql. La herramienta no acepta contraseñas en sus argumentos ni las imprime en sus errores.

La consulta lee catálogos `pg_catalog`, definiciones de columnas, restricciones, índices, disparadores, funciones asociadas, secuencias y vistas. No lee filas de las tablas de negocio ni valores actuales de secuencias. Impone `default_transaction_read_only=on`, una transacción `REPEATABLE READ` con `READ ONLY` y finaliza con rollback. El esquema es un parámetro SQL; no se concatena como código. `search_path=pg_catalog` evita resolver funciones de catálogo a través del esquema inspeccionado.

El inventario se publica únicamente después de una lectura satisfactoria y se rechazan rutas existentes. El archivo temporal se escribe en la misma carpeta y se mueve sin sobrescribir. `artifacts/` está excluido de Git.

Un inventario satisfactorio no certifica la integridad de todos los datos ni demuestra por sí solo 3FN. La conciliación requiere comparar las definiciones y revisar las reglas entre obras. Tampoco sustituye un respaldo completo con datos, permisos y procedimiento de restauración.

## Verificación reproducible

Después de compilar la solución en Debug, ejecutar en Windows con la carpeta de binarios de una instalación PostgreSQL compatible:

```powershell
./Tests/Integracion/VerificarPersistenciaPostgreSql.ps1 -BinariosPostgreSql '<carpeta de PostgreSQL>/bin'
```

La verificación crea su propio clúster dentro de `artifacts/VerificacionInspeccion/`, en un puerto libre enlazado a `127.0.0.1`. Carga allí el DDL académico y utiliza un rol sin acceso a filas de negocio. Comprueba el conteo de 38 tablas, claves compuestas, índices parciales, la incorporación posterior de `estado`, una restricción sin validar, un disparador deshabilitado y su función. También comprueba que no se pueda sobrescribir una evidencia y que un nombre de esquema con contenido SQL se trate como un valor. Detiene su clúster al finalizar; no se conecta a `constructora_eder`.

También restaura el esquema conciliado en otra base vacía del mismo clúster y comprueba que registrar dos veces la línea base de EF deja una sola marca en el historial y conserva las tablas de negocio. La prueba usa autenticación local sin contraseña únicamente para esa instancia con información sintética. No cambia la configuración de autenticación de PostgreSQL instalado como servicio.

## Referencias

- [PostgreSQL 16: transacciones de solo lectura](https://www.postgresql.org/docs/16/sql-set-transaction.html).
- [Funciones de información del catálogo](https://www.postgresql.org/docs/16/functions-info.html).
- [Npgsql: conexiones, transacciones y parámetros](https://www.npgsql.org/doc/basic-usage.html).
