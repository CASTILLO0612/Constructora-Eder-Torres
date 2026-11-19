START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    ALTER TABLE public.obras ADD latitud numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    ALTER TABLE public.obras ADD longitud numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    ALTER TABLE public.obras ADD radio_asistencia_metros numeric(10,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    ALTER TABLE public.obras ADD CONSTRAINT ck_obra_geocerca CHECK ((((latitud IS NULL) AND (longitud IS NULL) AND (radio_asistencia_metros IS NULL)) OR ((latitud IS NOT NULL) AND (longitud IS NOT NULL) AND (radio_asistencia_metros IS NOT NULL) AND (latitud >= '-90'::numeric) AND (latitud <= 90::numeric) AND (longitud >= '-180'::numeric) AND (longitud <= 180::numeric) AND (radio_asistencia_metros > 0::numeric))));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    CREATE UNIQUE INDEX ux_asistencia_abierta_asignacion ON public.asistencias (id_asignacion) WHERE salida_at IS NULL AND estado <> 'ANULADA';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    INSERT INTO public.permisos(codigo, nombre, descripcion, activo) VALUES
      ('asistencia.propia.registrar', 'Registrar asistencia propia', 'Marcar exclusivamente la asistencia del trabajador vinculado a la sesion.', true),
      ('asistencia.propia.consultar', 'Consultar asistencia propia', 'Consultar exclusivamente asistencias y asignaciones propias.', true),
      ('sincronizacion.operar', 'Sincronizar operaciones offline', 'Enviar operaciones capturadas sin conexion.', true)
    ON CONFLICT (codigo) DO NOTHING;
    INSERT INTO public.roles(nombre, descripcion, activo)
    VALUES ('TRABAJADOR', 'Marcacion individual con GPS y consulta de asistencia propia.', true)
    ON CONFLICT (nombre) DO NOTHING;
    INSERT INTO public.roles_permiso(id_rol, id_permiso)
    SELECT r.id_rol, p.id_permiso FROM public.roles r CROSS JOIN public.permisos p
    WHERE r.nombre = 'TRABAJADOR'
      AND p.codigo IN ('asistencia.propia.registrar', 'asistencia.propia.consultar', 'sincronizacion.operar')
    ON CONFLICT DO NOTHING;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030034407_AsistenciaPropiaYGeocerca') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030034407_AsistenciaPropiaYGeocerca', '10.0.14');
    END IF;
END $EF$;
COMMIT;
