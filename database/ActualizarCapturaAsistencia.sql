START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030050649_PrecisionesGpsYCapturaLocal') THEN
    ALTER TABLE public.asistencias ADD precision_gps_entrada_metros numeric(10,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030050649_PrecisionesGpsYCapturaLocal') THEN
    ALTER TABLE public.asistencias ADD precision_gps_salida_metros numeric(10,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030050649_PrecisionesGpsYCapturaLocal') THEN
    ALTER TABLE public.asistencias ADD CONSTRAINT ck_asistencia_precision_gps CHECK (((precision_gps_entrada_metros IS NULL OR (precision_gps_entrada_metros >= 0 AND precision_gps_entrada_metros <= 99999999.99 AND latitud_entrada IS NOT NULL AND longitud_entrada IS NOT NULL)) AND (precision_gps_salida_metros IS NULL OR (precision_gps_salida_metros >= 0 AND precision_gps_salida_metros <= 99999999.99 AND latitud_salida IS NOT NULL AND longitud_salida IS NOT NULL AND salida_at IS NOT NULL))));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030050649_PrecisionesGpsYCapturaLocal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030050649_PrecisionesGpsYCapturaLocal', '10.0.14');
    END IF;
END $EF$;
COMMIT;
