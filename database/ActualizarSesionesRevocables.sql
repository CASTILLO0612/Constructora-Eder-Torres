START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030210108_SesionesRevocables') THEN
    CREATE TABLE public.sesiones_usuario (
        id_sesion uuid NOT NULL,
        id_usuario integer NOT NULL,
        emitida_en_utc timestamp(6) with time zone NOT NULL,
        expira_en_utc timestamp(6) with time zone NOT NULL,
        revocada_en_utc timestamp(6) with time zone,
        CONSTRAINT sesiones_usuario_pkey PRIMARY KEY (id_sesion),
        CONSTRAINT ck_sesion_usuario_fechas CHECK (expira_en_utc > emitida_en_utc AND (revocada_en_utc IS NULL OR revocada_en_utc >= emitida_en_utc)),
        CONSTRAINT fk_sesion_usuario_usuario FOREIGN KEY (id_usuario) REFERENCES public.usuarios (id_usuario)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030210108_SesionesRevocables') THEN
    CREATE INDEX ix_sesiones_usuario_usuario_expira ON public.sesiones_usuario (id_usuario, expira_en_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030210108_SesionesRevocables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030210108_SesionesRevocables', '10.0.14');
    END IF;
END $EF$;
COMMIT;
