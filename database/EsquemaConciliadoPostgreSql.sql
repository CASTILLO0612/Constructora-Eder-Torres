--
-- PostgreSQL database dump
--

\restrict r5CtQkL1GDNy2ZelKyaR4YTZBe6T7sdHsXmFxh8KA0EVaS7cpcELABom2x9H1K1

-- Dumped from database version 16.10
-- Dumped by pg_dump version 16.10

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: pgcrypto; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;


--
-- Name: EXTENSION pgcrypto; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION pgcrypto IS 'cryptographic functions';


--
-- Name: fn_impedir_cambio_obra(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_impedir_cambio_obra() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.id_obra IS DISTINCT FROM OLD.id_obra THEN
        RAISE EXCEPTION USING
            ERRCODE = '23514',
            MESSAGE = format(
                'No se permite cambiar la obra de un registro existente en la tabla %s. Debe anularse el registro y crear uno nuevo.',
                TG_TABLE_NAME
            );
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: fn_validar_asociacion_misma_obra(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_validar_asociacion_misma_obra() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_asociacion_incorrecta BOOLEAN := FALSE;
BEGIN
    CASE TG_TABLE_NAME

        -- El responsable debe estar asignado a la misma obra
        -- a la que pertenece la actividad.
        WHEN 'actividades' THEN

            IF NEW.id_asignacion_responsable IS NULL THEN
                RETURN NEW;
            END IF;

            SELECT EXISTS (
                SELECT 1
                FROM asignaciones_trabajador_obra ato
                WHERE ato.id_asignacion =
                      NEW.id_asignacion_responsable
                  AND ato.id_obra <> NEW.id_obra
            )
            INTO v_asociacion_incorrecta;

        -- El trabajador debe pertenecer a la misma obra
        -- de la actividad.
        WHEN 'trabajadores_actividad' THEN

            SELECT EXISTS (
                SELECT 1
                FROM actividades a
                JOIN asignaciones_trabajador_obra ato
                    ON ato.id_asignacion = NEW.id_asignacion
                WHERE a.id_actividad = NEW.id_actividad
                  AND a.id_obra <> ato.id_obra
            )
            INTO v_asociacion_incorrecta;

        -- El trabajador y la cuadrilla deben estar
        -- asignados a la misma obra.
        WHEN 'trabajadores_cuadrilla' THEN

            SELECT EXISTS (
                SELECT 1
                FROM asignaciones_trabajador_obra ato
                JOIN cuadrillas_obra co
                    ON co.id_cuadrilla_obra =
                       NEW.id_cuadrilla_obra
                WHERE ato.id_asignacion = NEW.id_asignacion
                  AND ato.id_obra <> co.id_obra
            )
            INTO v_asociacion_incorrecta;

        -- La incidencia y la actividad afectada
        -- deben pertenecer a la misma obra.
        WHEN 'actividades_incidencia' THEN

            SELECT EXISTS (
                SELECT 1
                FROM actividades a
                JOIN incidencias i
                    ON i.id_incidencia = NEW.id_incidencia
                WHERE a.id_actividad = NEW.id_actividad
                  AND a.id_obra <> i.id_obra
            )
            INTO v_asociacion_incorrecta;

        -- El trabajador participante debe estar
        -- asignado a la obra del acta.
        WHEN 'trabajadores_acta' THEN

            SELECT EXISTS (
                SELECT 1
                FROM actas ac
                JOIN asignaciones_trabajador_obra ato
                    ON ato.id_asignacion = NEW.id_asignacion
                WHERE ac.id_acta = NEW.id_acta
                  AND ac.id_obra <> ato.id_obra
            )
            INTO v_asociacion_incorrecta;

        -- El avance incluido debe pertenecer
        -- a la misma obra del acta.
        WHEN 'actas_avance' THEN

            SELECT EXISTS (
                SELECT 1
                FROM actas ac
                JOIN avances av
                    ON av.id_avance = NEW.id_avance
                JOIN actividades a
                    ON a.id_actividad = av.id_actividad
                WHERE ac.id_acta = NEW.id_acta
                  AND ac.id_obra <> a.id_obra
            )
            INTO v_asociacion_incorrecta;

        -- La incidencia incluida debe pertenecer
        -- a la misma obra del acta.
        WHEN 'actas_incidencia' THEN

            SELECT EXISTS (
                SELECT 1
                FROM actas ac
                JOIN incidencias i
                    ON i.id_incidencia = NEW.id_incidencia
                WHERE ac.id_acta = NEW.id_acta
                  AND ac.id_obra <> i.id_obra
            )
            INTO v_asociacion_incorrecta;

        ELSE
            RAISE EXCEPTION
                'La función de validación no está configurada para la tabla %.',
                TG_TABLE_NAME;
    END CASE;

    IF v_asociacion_incorrecta THEN
        RAISE EXCEPTION USING
            ERRCODE = '23514',
            MESSAGE = format(
                'No se permite asociar registros pertenecientes a obras diferentes en la tabla %s.',
                TG_TABLE_NAME
            );
    END IF;

    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: actas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.actas (
    id_acta integer NOT NULL,
    id_obra integer NOT NULL,
    numero_acta character varying(30) NOT NULL,
    periodo_inicio date NOT NULL,
    periodo_fin date NOT NULL,
    observaciones character varying(1000),
    mejoras_propuestas character varying(500),
    solicitudes_cambio character varying(500),
    estado character varying(20) NOT NULL,
    id_usuario_creador integer NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT ck_acta_estado CHECK (((estado)::text = ANY ((ARRAY['BORRADOR'::character varying, 'PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_acta_periodo CHECK ((periodo_fin >= periodo_inicio))
);


--
-- Name: actas_avance; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.actas_avance (
    id_acta integer NOT NULL,
    id_avance bigint NOT NULL
);


--
-- Name: actas_id_acta_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.actas ALTER COLUMN id_acta ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.actas_id_acta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: actas_incidencia; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.actas_incidencia (
    id_acta integer NOT NULL,
    id_incidencia bigint NOT NULL
);


--
-- Name: actividades; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.actividades (
    id_actividad integer NOT NULL,
    id_obra integer NOT NULL,
    id_asignacion_responsable integer,
    codigo character varying(30) NOT NULL,
    nombre character varying(120) NOT NULL,
    descripcion character varying(500),
    unidad_medida character varying(20) NOT NULL,
    cantidad_planificada numeric(12,2) NOT NULL,
    fecha_inicio_plan date NOT NULL,
    fecha_fin_plan date NOT NULL,
    estado character varying(20) NOT NULL,
    CONSTRAINT ck_actividad_cantidad CHECK ((cantidad_planificada >= (0)::numeric)),
    CONSTRAINT ck_actividad_estado CHECK (((estado)::text = ANY ((ARRAY['PLANIFICADA'::character varying, 'EN_PROCESO'::character varying, 'COMPLETA'::character varying, 'RETRASADA'::character varying, 'CANCELADA'::character varying])::text[]))),
    CONSTRAINT ck_actividad_fechas CHECK ((fecha_fin_plan >= fecha_inicio_plan))
);


--
-- Name: actividades_id_actividad_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.actividades ALTER COLUMN id_actividad ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.actividades_id_actividad_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: actividades_incidencia; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.actividades_incidencia (
    id_incidencia bigint NOT NULL,
    id_actividad integer NOT NULL
);


--
-- Name: aprobaciones_acta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aprobaciones_acta (
    id_aprobacion integer NOT NULL,
    id_acta integer NOT NULL,
    id_usuario integer NOT NULL,
    decision character varying(15) NOT NULL,
    comentario character varying(500),
    fecha_hora timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT ck_aprobacion_acta_decision CHECK (((decision)::text = ANY ((ARRAY['APROBADA'::character varying, 'RECHAZADA'::character varying])::text[])))
);


--
-- Name: aprobaciones_acta_id_aprobacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.aprobaciones_acta ALTER COLUMN id_aprobacion ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.aprobaciones_acta_id_aprobacion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asignaciones_herramienta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.asignaciones_herramienta (
    id_asignacion_herramienta bigint NOT NULL,
    id_herramienta integer NOT NULL,
    id_asignacion_trabajador_obra integer NOT NULL,
    id_usuario_entrega integer NOT NULL,
    entregada_en timestamp(0) with time zone NOT NULL,
    condicion_entrega character varying(20) NOT NULL,
    devuelta_en timestamp(0) with time zone,
    condicion_devolucion character varying(20),
    estado character varying(20) NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    sincronizado_en timestamp(0) with time zone,
    CONSTRAINT ck_ah_estado CHECK (((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'DEVUELTA'::character varying, 'PERDIDA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_ah_fecha CHECK (((devuelta_en IS NULL) OR (devuelta_en >= entregada_en)))
);


--
-- Name: asignaciones_herramienta_id_asignacion_herramienta_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.asignaciones_herramienta ALTER COLUMN id_asignacion_herramienta ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.asignaciones_herramienta_id_asignacion_herramienta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asignaciones_trabajador_obra; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.asignaciones_trabajador_obra (
    id_asignacion integer NOT NULL,
    id_trabajador integer NOT NULL,
    id_obra integer NOT NULL,
    id_especialidad integer,
    tipo_contratacion character varying(20) NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date,
    es_responsable boolean DEFAULT false NOT NULL,
    estado character varying(15) NOT NULL,
    CONSTRAINT ck_ato_estado CHECK (((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'FINALIZADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_ato_fechas CHECK (((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio))),
    CONSTRAINT ck_ato_tipo CHECK (((tipo_contratacion)::text = ANY ((ARRAY['FIJO'::character varying, 'SUBCONTRATADO'::character varying, 'TEMPORAL'::character varying])::text[])))
);


--
-- Name: asignaciones_trabajador_obra_id_asignacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.asignaciones_trabajador_obra ALTER COLUMN id_asignacion ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.asignaciones_trabajador_obra_id_asignacion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asistencias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.asistencias (
    id_asistencia bigint NOT NULL,
    id_asignacion integer NOT NULL,
    fecha_jornada date NOT NULL,
    entrada_at timestamp(0) with time zone NOT NULL,
    salida_at timestamp(0) with time zone,
    latitud_entrada numeric(9,6),
    longitud_entrada numeric(9,6),
    latitud_salida numeric(9,6),
    longitud_salida numeric(9,6),
    estado character varying(15) NOT NULL,
    id_usuario_registro integer NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    sincronizado_en timestamp(0) with time zone,
    CONSTRAINT ck_asistencia_estado CHECK (((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'COMPLETA'::character varying, 'CORREGIDA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_asistencia_horas CHECK (((salida_at IS NULL) OR (salida_at >= entrada_at))),
    CONSTRAINT ck_asistencia_lat_entrada CHECK (((latitud_entrada IS NULL) OR ((latitud_entrada >= ('-90'::integer)::numeric) AND (latitud_entrada <= (90)::numeric)))),
    CONSTRAINT ck_asistencia_lat_salida CHECK (((latitud_salida IS NULL) OR ((latitud_salida >= ('-90'::integer)::numeric) AND (latitud_salida <= (90)::numeric)))),
    CONSTRAINT ck_asistencia_lon_entrada CHECK (((longitud_entrada IS NULL) OR ((longitud_entrada >= ('-180'::integer)::numeric) AND (longitud_entrada <= (180)::numeric)))),
    CONSTRAINT ck_asistencia_lon_salida CHECK (((longitud_salida IS NULL) OR ((longitud_salida >= ('-180'::integer)::numeric) AND (longitud_salida <= (180)::numeric))))
);


--
-- Name: asistencias_id_asistencia_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.asistencias ALTER COLUMN id_asistencia ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.asistencias_id_asistencia_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auditorias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auditorias (
    id_auditoria bigint NOT NULL,
    id_usuario integer,
    entidad character varying(60) NOT NULL,
    id_registro character varying(50) NOT NULL,
    accion character varying(20) NOT NULL,
    campo character varying(60),
    valor_anterior character varying(1000),
    valor_nuevo character varying(1000),
    fecha_hora timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ip_origen inet,
    uuid_operacion uuid DEFAULT gen_random_uuid() NOT NULL,
    CONSTRAINT ck_auditoria_accion CHECK (((accion)::text = ANY ((ARRAY['CREAR'::character varying, 'MODIFICAR'::character varying, 'ANULAR'::character varying, 'APROBAR'::character varying, 'RECHAZAR'::character varying])::text[])))
);


--
-- Name: auditorias_id_auditoria_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auditorias ALTER COLUMN id_auditoria ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.auditorias_id_auditoria_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: avances; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.avances (
    id_avance bigint NOT NULL,
    id_actividad integer NOT NULL,
    fecha_avance date NOT NULL,
    cantidad_ejecutada numeric(12,2) NOT NULL,
    observaciones character varying(500),
    estado character varying(20) NOT NULL,
    id_usuario_registro integer NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    sincronizado_en timestamp(0) with time zone,
    CONSTRAINT ck_avance_cantidad CHECK ((cantidad_ejecutada >= (0)::numeric)),
    CONSTRAINT ck_avance_estado CHECK (((estado)::text = ANY ((ARRAY['BORRADOR'::character varying, 'REGISTRADO'::character varying, 'ANULADO'::character varying])::text[])))
);


--
-- Name: avances_id_avance_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.avances ALTER COLUMN id_avance ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.avances_id_avance_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cargos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cargos (
    id_cargo integer NOT NULL,
    nombre character varying(60) NOT NULL,
    descripcion character varying(200),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: cargos_id_cargo_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.cargos ALTER COLUMN id_cargo ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.cargos_id_cargo_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: categorias_herramienta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.categorias_herramienta (
    id_categoria integer NOT NULL,
    nombre character varying(80) NOT NULL,
    descripcion character varying(250),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: categorias_herramienta_id_categoria_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.categorias_herramienta ALTER COLUMN id_categoria ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.categorias_herramienta_id_categoria_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: correcciones_asistencia; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.correcciones_asistencia (
    id_correccion bigint NOT NULL,
    id_asistencia bigint NOT NULL,
    id_usuario_solicita integer NOT NULL,
    id_usuario_aprueba integer,
    entrada_anterior timestamp(0) with time zone,
    salida_anterior timestamp(0) with time zone,
    entrada_nueva timestamp(0) with time zone,
    salida_nueva timestamp(0) with time zone,
    motivo character varying(300) NOT NULL,
    estado character varying(15) NOT NULL,
    solicitado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    resuelto_en timestamp(0) with time zone,
    CONSTRAINT ck_ca_estado CHECK (((estado)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_ca_horas CHECK (((salida_nueva IS NULL) OR (entrada_nueva IS NULL) OR (salida_nueva >= entrada_nueva)))
);


--
-- Name: correcciones_asistencia_id_correccion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.correcciones_asistencia ALTER COLUMN id_correccion ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.correcciones_asistencia_id_correccion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cuadrillas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cuadrillas (
    id_cuadrilla integer NOT NULL,
    codigo character varying(20) NOT NULL,
    nombre character varying(80) NOT NULL,
    descripcion character varying(200),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: cuadrillas_id_cuadrilla_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.cuadrillas ALTER COLUMN id_cuadrilla ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.cuadrillas_id_cuadrilla_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cuadrillas_obra; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cuadrillas_obra (
    id_cuadrilla_obra integer NOT NULL,
    id_cuadrilla integer NOT NULL,
    id_obra integer NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date,
    estado character varying(15) NOT NULL,
    CONSTRAINT ck_co_estado CHECK (((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'FINALIZADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_co_fechas CHECK (((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio)))
);


--
-- Name: cuadrillas_obra_id_cuadrilla_obra_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.cuadrillas_obra ALTER COLUMN id_cuadrilla_obra ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.cuadrillas_obra_id_cuadrilla_obra_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: entregas_epp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.entregas_epp (
    id_entrega_epp bigint NOT NULL,
    id_epp integer NOT NULL,
    id_asignacion integer NOT NULL,
    id_usuario_entrega integer NOT NULL,
    entregado_en timestamp(0) with time zone NOT NULL,
    condicion_entrega character varying(20) NOT NULL,
    devuelto_en timestamp(0) with time zone,
    condicion_devolucion character varying(20),
    estado character varying(20) NOT NULL,
    ruta_constancia character varying(500),
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    sincronizado_en timestamp(0) with time zone,
    CONSTRAINT ck_entrega_epp_estado CHECK (((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'DEVUELTA'::character varying, 'PERDIDA'::character varying, 'REEMPLAZADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_entrega_epp_fecha CHECK (((devuelto_en IS NULL) OR (devuelto_en >= entregado_en)))
);


--
-- Name: entregas_epp_id_entrega_epp_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.entregas_epp ALTER COLUMN id_entrega_epp ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.entregas_epp_id_entrega_epp_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: epps; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.epps (
    id_epp integer NOT NULL,
    id_tipo_epp integer NOT NULL,
    codigo_interno character varying(30) NOT NULL,
    numero_serie character varying(60),
    fecha_adquisicion date NOT NULL,
    estado character varying(20) NOT NULL,
    condicion character varying(20) NOT NULL,
    CONSTRAINT ck_epp_condicion CHECK (((condicion)::text = ANY ((ARRAY['NUEVO'::character varying, 'BUENO'::character varying, 'DESGASTADO'::character varying, 'DANADO'::character varying])::text[]))),
    CONSTRAINT ck_epp_estado CHECK (((estado)::text = ANY ((ARRAY['DISPONIBLE'::character varying, 'ENTREGADO'::character varying, 'EN_REVISION'::character varying, 'PERDIDO'::character varying, 'RETIRADO'::character varying])::text[])))
);


--
-- Name: epps_id_epp_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.epps ALTER COLUMN id_epp ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.epps_id_epp_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: especialidades; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.especialidades (
    id_especialidad integer NOT NULL,
    nombre character varying(60) NOT NULL,
    descripcion character varying(200),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: especialidades_id_especialidad_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.especialidades ALTER COLUMN id_especialidad ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.especialidades_id_especialidad_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: evidencias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.evidencias (
    id_evidencia bigint NOT NULL,
    id_avance bigint NOT NULL,
    tipo_evidencia character varying(20) NOT NULL,
    ruta_archivo character varying(500) NOT NULL,
    hash_archivo character(64) NOT NULL,
    capturada_en timestamp(0) with time zone NOT NULL,
    latitud numeric(9,6),
    longitud numeric(9,6),
    id_usuario_registro integer NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    sincronizado_en timestamp(0) with time zone,
    CONSTRAINT ck_evidencia_lat CHECK (((latitud IS NULL) OR ((latitud >= ('-90'::integer)::numeric) AND (latitud <= (90)::numeric)))),
    CONSTRAINT ck_evidencia_lon CHECK (((longitud IS NULL) OR ((longitud >= ('-180'::integer)::numeric) AND (longitud <= (180)::numeric)))),
    CONSTRAINT ck_evidencia_tipo CHECK (((tipo_evidencia)::text = ANY ((ARRAY['FOTOGRAFIA'::character varying, 'BITACORA'::character varying, 'DOCUMENTO'::character varying])::text[])))
);


--
-- Name: evidencias_id_evidencia_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.evidencias ALTER COLUMN id_evidencia ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.evidencias_id_evidencia_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: herramientas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.herramientas (
    id_herramienta integer NOT NULL,
    id_tipo_herramienta integer NOT NULL,
    codigo_interno character varying(30) NOT NULL,
    numero_serie character varying(60),
    marca character varying(60),
    modelo character varying(60),
    fecha_adquisicion date NOT NULL,
    estado character varying(20) NOT NULL,
    condicion character varying(20) NOT NULL,
    CONSTRAINT ck_herramienta_condicion CHECK (((condicion)::text = ANY ((ARRAY['NUEVA'::character varying, 'BUENA'::character varying, 'DESGASTADA'::character varying, 'DANADA'::character varying])::text[]))),
    CONSTRAINT ck_herramienta_estado CHECK (((estado)::text = ANY ((ARRAY['DISPONIBLE'::character varying, 'ASIGNADA'::character varying, 'EN_REVISION'::character varying, 'PERDIDA'::character varying, 'RETIRADA'::character varying])::text[])))
);


--
-- Name: herramientas_id_herramienta_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.herramientas ALTER COLUMN id_herramienta ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.herramientas_id_herramienta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: incidencias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.incidencias (
    id_incidencia bigint NOT NULL,
    id_obra integer NOT NULL,
    tipo_incidencia character varying(30) NOT NULL,
    inicio_en timestamp(0) with time zone NOT NULL,
    fin_en timestamp(0) with time zone,
    horas_perdidas numeric(8,2),
    descripcion character varying(500) NOT NULL,
    justificacion character varying(500),
    estado character varying(20) NOT NULL,
    id_usuario_registro integer NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    sincronizado_en timestamp(0) with time zone,
    CONSTRAINT ck_incidencia_estado CHECK (((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'RESUELTA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_incidencia_fechas CHECK (((fin_en IS NULL) OR (fin_en >= inicio_en))),
    CONSTRAINT ck_incidencia_horas CHECK (((horas_perdidas IS NULL) OR (horas_perdidas >= (0)::numeric))),
    CONSTRAINT ck_incidencia_tipo CHECK (((tipo_incidencia)::text = ANY ((ARRAY['CLIMA'::character varying, 'MATERIAL_IMPORTADO'::character varying, 'TECNICA'::character varying, 'SEGURIDAD'::character varying, 'OTRA'::character varying])::text[])))
);


--
-- Name: incidencias_herramienta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.incidencias_herramienta (
    id_incidencia_herramienta bigint NOT NULL,
    id_asignacion_herramienta bigint NOT NULL,
    tipo_incidencia character varying(25) NOT NULL,
    fecha_hora timestamp(0) with time zone NOT NULL,
    descripcion character varying(500) NOT NULL,
    resolucion character varying(500),
    costo_estimado numeric(12,2),
    id_usuario_registro integer NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    estado character varying(20) DEFAULT 'ABIERTA'::character varying NOT NULL,
    CONSTRAINT ck_ih_costo CHECK (((costo_estimado IS NULL) OR (costo_estimado >= (0)::numeric))),
    CONSTRAINT ck_ih_estado CHECK (((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'EN_PROCESO'::character varying, 'RESUELTA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_ih_tipo CHECK (((tipo_incidencia)::text = ANY ((ARRAY['DANO'::character varying, 'PERDIDA'::character varying, 'DEMORA'::character varying, 'DEVOLUCION'::character varying, 'OTRA'::character varying])::text[])))
);


--
-- Name: incidencias_herramienta_id_incidencia_herramienta_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.incidencias_herramienta ALTER COLUMN id_incidencia_herramienta ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.incidencias_herramienta_id_incidencia_herramienta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: incidencias_id_incidencia_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.incidencias ALTER COLUMN id_incidencia ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.incidencias_id_incidencia_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: movimientos_stock_epp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.movimientos_stock_epp (
    id_movimiento_epp bigint NOT NULL,
    id_epp integer NOT NULL,
    id_entrega_epp bigint,
    tipo_movimiento character varying(25) NOT NULL,
    fecha_hora timestamp(0) with time zone NOT NULL,
    id_usuario integer NOT NULL,
    observacion character varying(300),
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    CONSTRAINT ck_movimiento_epp_tipo CHECK (((tipo_movimiento)::text = ANY ((ARRAY['ENTRADA'::character varying, 'ENTREGA'::character varying, 'DEVOLUCION'::character varying, 'PERDIDA'::character varying, 'RETIRO'::character varying, 'AJUSTE'::character varying])::text[])))
);


--
-- Name: movimientos_stock_epp_id_movimiento_epp_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.movimientos_stock_epp ALTER COLUMN id_movimiento_epp ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.movimientos_stock_epp_id_movimiento_epp_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: obras; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.obras (
    id_obra integer NOT NULL,
    codigo character varying(20) NOT NULL,
    nombre character varying(120) NOT NULL,
    descripcion character varying(500),
    direccion character varying(200) NOT NULL,
    fecha_inicio_plan date NOT NULL,
    fecha_fin_plan date NOT NULL,
    fecha_inicio_real date,
    fecha_fin_real date,
    estado character varying(20) NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT ck_obra_estado CHECK (((estado)::text = ANY ((ARRAY['PLANIFICADA'::character varying, 'ACTIVA'::character varying, 'SUSPENDIDA'::character varying, 'FINALIZADA'::character varying])::text[]))),
    CONSTRAINT ck_obra_fechas_plan CHECK ((fecha_fin_plan >= fecha_inicio_plan)),
    CONSTRAINT ck_obra_fechas_real CHECK (((fecha_fin_real IS NULL) OR (fecha_inicio_real IS NULL) OR (fecha_fin_real >= fecha_inicio_real)))
);


--
-- Name: obras_id_obra_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.obras ALTER COLUMN id_obra ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.obras_id_obra_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: permisos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.permisos (
    id_permiso integer NOT NULL,
    codigo character varying(60) NOT NULL,
    nombre character varying(80) NOT NULL,
    descripcion character varying(200),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: permisos_id_permiso_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.permisos ALTER COLUMN id_permiso ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.permisos_id_permiso_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: reposiciones_epp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.reposiciones_epp (
    id_reposicion_epp bigint NOT NULL,
    id_entrega_origen bigint NOT NULL,
    id_entrega_nueva bigint,
    motivo character varying(30) NOT NULL,
    descripcion character varying(300) NOT NULL,
    equipo_devuelto boolean DEFAULT false NOT NULL,
    id_usuario_solicita integer NOT NULL,
    solicitada_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    id_usuario_aprueba integer,
    resuelta_en timestamp(0) with time zone,
    estado character varying(20) NOT NULL,
    uuid_registro uuid DEFAULT gen_random_uuid() NOT NULL,
    CONSTRAINT ck_reposicion_estado CHECK (((estado)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'EJECUTADA'::character varying, 'ANULADA'::character varying])::text[]))),
    CONSTRAINT ck_reposicion_fechas CHECK (((resuelta_en IS NULL) OR (resuelta_en >= solicitada_en))),
    CONSTRAINT ck_reposicion_motivo CHECK (((motivo)::text = ANY ((ARRAY['DANO'::character varying, 'PERDIDA'::character varying, 'DESGASTE'::character varying, 'VENCIMIENTO'::character varying, 'OTRO'::character varying])::text[])))
);


--
-- Name: reposiciones_epp_id_reposicion_epp_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.reposiciones_epp ALTER COLUMN id_reposicion_epp ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.reposiciones_epp_id_reposicion_epp_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.roles (
    id_rol integer NOT NULL,
    nombre character varying(50) NOT NULL,
    descripcion character varying(200),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: roles_id_rol_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.roles ALTER COLUMN id_rol ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.roles_id_rol_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: roles_permiso; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.roles_permiso (
    id_rol integer NOT NULL,
    id_permiso integer NOT NULL
);


--
-- Name: tipos_epp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_epp (
    id_tipo_epp integer NOT NULL,
    nombre character varying(80) NOT NULL,
    descripcion character varying(250),
    vida_util_dias integer,
    stock_minimo integer DEFAULT 0 NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT ck_tipo_epp_stock CHECK ((stock_minimo >= 0)),
    CONSTRAINT ck_tipo_epp_vida CHECK (((vida_util_dias IS NULL) OR (vida_util_dias > 0)))
);


--
-- Name: tipos_epp_id_tipo_epp_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.tipos_epp ALTER COLUMN id_tipo_epp ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.tipos_epp_id_tipo_epp_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tipos_herramienta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_herramienta (
    id_tipo_herramienta integer NOT NULL,
    id_categoria integer NOT NULL,
    nombre character varying(100) NOT NULL,
    descripcion character varying(250),
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: tipos_herramienta_id_tipo_herramienta_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.tipos_herramienta ALTER COLUMN id_tipo_herramienta ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.tipos_herramienta_id_tipo_herramienta_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: trabajadores; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.trabajadores (
    id_trabajador integer NOT NULL,
    id_cargo integer NOT NULL,
    cedula character varying(20) NOT NULL,
    primer_nombre character varying(50) NOT NULL,
    segundo_nombre character varying(50),
    primer_apellido character varying(50) NOT NULL,
    segundo_apellido character varying(50),
    telefono character varying(20),
    correo character varying(100),
    direccion character varying(200),
    fecha_ingreso date NOT NULL,
    estado character varying(15) NOT NULL,
    CONSTRAINT ck_trabajador_estado CHECK (((estado)::text = ANY ((ARRAY['ACTIVO'::character varying, 'INACTIVO'::character varying])::text[])))
);


--
-- Name: trabajadores_acta; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.trabajadores_acta (
    id_acta integer NOT NULL,
    id_asignacion integer NOT NULL,
    tipo_participacion character varying(30) NOT NULL,
    ruta_firma character varying(500),
    firmado_en timestamp(0) with time zone
);


--
-- Name: trabajadores_actividad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.trabajadores_actividad (
    id_actividad integer NOT NULL,
    id_asignacion integer NOT NULL,
    rol_participacion character varying(50)
);


--
-- Name: trabajadores_cuadrilla; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.trabajadores_cuadrilla (
    id_trabajador_cuadrilla integer NOT NULL,
    id_asignacion integer NOT NULL,
    id_cuadrilla_obra integer NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date,
    CONSTRAINT ck_tc_fechas CHECK (((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio)))
);


--
-- Name: trabajadores_cuadrilla_id_trabajador_cuadrilla_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.trabajadores_cuadrilla ALTER COLUMN id_trabajador_cuadrilla ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.trabajadores_cuadrilla_id_trabajador_cuadrilla_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: trabajadores_especialidad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.trabajadores_especialidad (
    id_trabajador integer NOT NULL,
    id_especialidad integer NOT NULL,
    nivel character varying(20),
    fecha_certificacion date,
    fecha_vencimiento date,
    CONSTRAINT ck_te_fechas CHECK (((fecha_vencimiento IS NULL) OR (fecha_certificacion IS NULL) OR (fecha_vencimiento >= fecha_certificacion))),
    CONSTRAINT ck_te_nivel CHECK (((nivel IS NULL) OR ((nivel)::text = ANY ((ARRAY['BASICO'::character varying, 'INTERMEDIO'::character varying, 'AVANZADO'::character varying, 'CERTIFICADO'::character varying])::text[]))))
);


--
-- Name: trabajadores_id_trabajador_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.trabajadores ALTER COLUMN id_trabajador ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.trabajadores_id_trabajador_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuarios; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuarios (
    id_usuario integer NOT NULL,
    id_trabajador integer,
    nombre_usuario character varying(60) NOT NULL,
    password_hash character varying(255) NOT NULL,
    estado character varying(15) NOT NULL,
    creado_en timestamp(0) with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ultimo_acceso timestamp(0) with time zone,
    CONSTRAINT ck_usuario_estado CHECK (((estado)::text = ANY ((ARRAY['ACTIVO'::character varying, 'BLOQUEADO'::character varying, 'INACTIVO'::character varying])::text[])))
);


--
-- Name: usuarios_id_usuario_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuarios ALTER COLUMN id_usuario ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.usuarios_id_usuario_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuarios_rol; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuarios_rol (
    id_usuario_rol integer NOT NULL,
    id_usuario integer NOT NULL,
    id_rol integer NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date,
    CONSTRAINT ck_usuario_rol_fechas CHECK (((fecha_fin IS NULL) OR (fecha_fin >= fecha_inicio)))
);


--
-- Name: usuarios_rol_id_usuario_rol_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuarios_rol ALTER COLUMN id_usuario_rol ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.usuarios_rol_id_usuario_rol_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: actas actas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas
    ADD CONSTRAINT actas_pkey PRIMARY KEY (id_acta);


--
-- Name: actividades actividades_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades
    ADD CONSTRAINT actividades_pkey PRIMARY KEY (id_actividad);


--
-- Name: aprobaciones_acta aprobaciones_acta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aprobaciones_acta
    ADD CONSTRAINT aprobaciones_acta_pkey PRIMARY KEY (id_aprobacion);


--
-- Name: asignaciones_herramienta asignaciones_herramienta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_herramienta
    ADD CONSTRAINT asignaciones_herramienta_pkey PRIMARY KEY (id_asignacion_herramienta);


--
-- Name: asignaciones_trabajador_obra asignaciones_trabajador_obra_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_trabajador_obra
    ADD CONSTRAINT asignaciones_trabajador_obra_pkey PRIMARY KEY (id_asignacion);


--
-- Name: asistencias asistencias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asistencias
    ADD CONSTRAINT asistencias_pkey PRIMARY KEY (id_asistencia);


--
-- Name: auditorias auditorias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditorias
    ADD CONSTRAINT auditorias_pkey PRIMARY KEY (id_auditoria);


--
-- Name: avances avances_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.avances
    ADD CONSTRAINT avances_pkey PRIMARY KEY (id_avance);


--
-- Name: cargos cargos_nombre_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cargos
    ADD CONSTRAINT cargos_nombre_key UNIQUE (nombre);


--
-- Name: cargos cargos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cargos
    ADD CONSTRAINT cargos_pkey PRIMARY KEY (id_cargo);


--
-- Name: categorias_herramienta categorias_herramienta_nombre_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categorias_herramienta
    ADD CONSTRAINT categorias_herramienta_nombre_key UNIQUE (nombre);


--
-- Name: categorias_herramienta categorias_herramienta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categorias_herramienta
    ADD CONSTRAINT categorias_herramienta_pkey PRIMARY KEY (id_categoria);


--
-- Name: correcciones_asistencia correcciones_asistencia_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.correcciones_asistencia
    ADD CONSTRAINT correcciones_asistencia_pkey PRIMARY KEY (id_correccion);


--
-- Name: cuadrillas cuadrillas_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuadrillas
    ADD CONSTRAINT cuadrillas_codigo_key UNIQUE (codigo);


--
-- Name: cuadrillas_obra cuadrillas_obra_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuadrillas_obra
    ADD CONSTRAINT cuadrillas_obra_pkey PRIMARY KEY (id_cuadrilla_obra);


--
-- Name: cuadrillas cuadrillas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuadrillas
    ADD CONSTRAINT cuadrillas_pkey PRIMARY KEY (id_cuadrilla);


--
-- Name: entregas_epp entregas_epp_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.entregas_epp
    ADD CONSTRAINT entregas_epp_pkey PRIMARY KEY (id_entrega_epp);


--
-- Name: epps epps_codigo_interno_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.epps
    ADD CONSTRAINT epps_codigo_interno_key UNIQUE (codigo_interno);


--
-- Name: epps epps_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.epps
    ADD CONSTRAINT epps_pkey PRIMARY KEY (id_epp);


--
-- Name: especialidades especialidades_nombre_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.especialidades
    ADD CONSTRAINT especialidades_nombre_key UNIQUE (nombre);


--
-- Name: especialidades especialidades_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.especialidades
    ADD CONSTRAINT especialidades_pkey PRIMARY KEY (id_especialidad);


--
-- Name: evidencias evidencias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencias
    ADD CONSTRAINT evidencias_pkey PRIMARY KEY (id_evidencia);


--
-- Name: herramientas herramientas_codigo_interno_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.herramientas
    ADD CONSTRAINT herramientas_codigo_interno_key UNIQUE (codigo_interno);


--
-- Name: herramientas herramientas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.herramientas
    ADD CONSTRAINT herramientas_pkey PRIMARY KEY (id_herramienta);


--
-- Name: incidencias_herramienta incidencias_herramienta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.incidencias_herramienta
    ADD CONSTRAINT incidencias_herramienta_pkey PRIMARY KEY (id_incidencia_herramienta);


--
-- Name: incidencias incidencias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.incidencias
    ADD CONSTRAINT incidencias_pkey PRIMARY KEY (id_incidencia);


--
-- Name: movimientos_stock_epp movimientos_stock_epp_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimientos_stock_epp
    ADD CONSTRAINT movimientos_stock_epp_pkey PRIMARY KEY (id_movimiento_epp);


--
-- Name: obras obras_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.obras
    ADD CONSTRAINT obras_codigo_key UNIQUE (codigo);


--
-- Name: obras obras_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.obras
    ADD CONSTRAINT obras_pkey PRIMARY KEY (id_obra);


--
-- Name: permisos permisos_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT permisos_codigo_key UNIQUE (codigo);


--
-- Name: permisos permisos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT permisos_pkey PRIMARY KEY (id_permiso);


--
-- Name: actas_avance pk_acta_avance; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas_avance
    ADD CONSTRAINT pk_acta_avance PRIMARY KEY (id_acta, id_avance);


--
-- Name: actas_incidencia pk_acta_incidencia; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas_incidencia
    ADD CONSTRAINT pk_acta_incidencia PRIMARY KEY (id_acta, id_incidencia);


--
-- Name: actividades_incidencia pk_actividad_incidencia; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades_incidencia
    ADD CONSTRAINT pk_actividad_incidencia PRIMARY KEY (id_incidencia, id_actividad);


--
-- Name: roles_permiso pk_rol_permiso; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles_permiso
    ADD CONSTRAINT pk_rol_permiso PRIMARY KEY (id_rol, id_permiso);


--
-- Name: trabajadores_acta pk_trabajador_acta; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_acta
    ADD CONSTRAINT pk_trabajador_acta PRIMARY KEY (id_acta, id_asignacion);


--
-- Name: trabajadores_actividad pk_trabajador_actividad; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_actividad
    ADD CONSTRAINT pk_trabajador_actividad PRIMARY KEY (id_actividad, id_asignacion);


--
-- Name: trabajadores_especialidad pk_trabajador_especialidad; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_especialidad
    ADD CONSTRAINT pk_trabajador_especialidad PRIMARY KEY (id_trabajador, id_especialidad);


--
-- Name: reposiciones_epp reposiciones_epp_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reposiciones_epp
    ADD CONSTRAINT reposiciones_epp_pkey PRIMARY KEY (id_reposicion_epp);


--
-- Name: roles roles_nombre_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_nombre_key UNIQUE (nombre);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id_rol);


--
-- Name: tipos_epp tipos_epp_nombre_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_epp
    ADD CONSTRAINT tipos_epp_nombre_key UNIQUE (nombre);


--
-- Name: tipos_epp tipos_epp_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_epp
    ADD CONSTRAINT tipos_epp_pkey PRIMARY KEY (id_tipo_epp);


--
-- Name: tipos_herramienta tipos_herramienta_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_herramienta
    ADD CONSTRAINT tipos_herramienta_pkey PRIMARY KEY (id_tipo_herramienta);


--
-- Name: trabajadores trabajadores_cedula_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores
    ADD CONSTRAINT trabajadores_cedula_key UNIQUE (cedula);


--
-- Name: trabajadores_cuadrilla trabajadores_cuadrilla_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_cuadrilla
    ADD CONSTRAINT trabajadores_cuadrilla_pkey PRIMARY KEY (id_trabajador_cuadrilla);


--
-- Name: trabajadores trabajadores_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores
    ADD CONSTRAINT trabajadores_pkey PRIMARY KEY (id_trabajador);


--
-- Name: actas uq_acta_numero; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas
    ADD CONSTRAINT uq_acta_numero UNIQUE (id_obra, numero_acta);


--
-- Name: actividades uq_actividad_codigo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades
    ADD CONSTRAINT uq_actividad_codigo UNIQUE (id_obra, codigo);


--
-- Name: asignaciones_trabajador_obra uq_ato_periodo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_trabajador_obra
    ADD CONSTRAINT uq_ato_periodo UNIQUE (id_trabajador, id_obra, fecha_inicio);


--
-- Name: avances uq_avance_dia; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.avances
    ADD CONSTRAINT uq_avance_dia UNIQUE (id_actividad, fecha_avance);


--
-- Name: cuadrillas_obra uq_co_periodo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuadrillas_obra
    ADD CONSTRAINT uq_co_periodo UNIQUE (id_cuadrilla, id_obra, fecha_inicio);


--
-- Name: trabajadores_cuadrilla uq_tc_periodo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_cuadrilla
    ADD CONSTRAINT uq_tc_periodo UNIQUE (id_asignacion, id_cuadrilla_obra, fecha_inicio);


--
-- Name: tipos_herramienta uq_tipo_herramienta; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_herramienta
    ADD CONSTRAINT uq_tipo_herramienta UNIQUE (id_categoria, nombre);


--
-- Name: usuarios_rol uq_usuario_rol_periodo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_rol
    ADD CONSTRAINT uq_usuario_rol_periodo UNIQUE (id_usuario, id_rol, fecha_inicio);


--
-- Name: usuarios usuarios_nombre_usuario_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT usuarios_nombre_usuario_key UNIQUE (nombre_usuario);


--
-- Name: usuarios usuarios_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT usuarios_pkey PRIMARY KEY (id_usuario);


--
-- Name: usuarios_rol usuarios_rol_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_rol
    ADD CONSTRAINT usuarios_rol_pkey PRIMARY KEY (id_usuario_rol);


--
-- Name: ux_acta_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_acta_uuid ON public.actas USING btree (uuid_registro);


--
-- Name: ux_asignacion_herramienta_activa; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_asignacion_herramienta_activa ON public.asignaciones_herramienta USING btree (id_herramienta) WHERE ((estado)::text = 'ACTIVA'::text);


--
-- Name: ux_asignacion_herramienta_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_asignacion_herramienta_uuid ON public.asignaciones_herramienta USING btree (uuid_registro);


--
-- Name: ux_asistencia_marcacion; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_asistencia_marcacion ON public.asistencias USING btree (id_asignacion, entrada_at);


--
-- Name: ux_asistencia_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_asistencia_uuid ON public.asistencias USING btree (uuid_registro);


--
-- Name: ux_ato_activa; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_ato_activa ON public.asignaciones_trabajador_obra USING btree (id_trabajador, id_obra) WHERE ((fecha_fin IS NULL) AND ((estado)::text = 'ACTIVA'::text));


--
-- Name: ux_auditoria_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_auditoria_uuid ON public.auditorias USING btree (uuid_operacion);


--
-- Name: ux_avance_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_avance_uuid ON public.avances USING btree (uuid_registro);


--
-- Name: ux_cuadrilla_obra_activa; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_cuadrilla_obra_activa ON public.cuadrillas_obra USING btree (id_cuadrilla, id_obra) WHERE ((fecha_fin IS NULL) AND ((estado)::text = 'ACTIVA'::text));


--
-- Name: ux_entrega_epp_activa; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_entrega_epp_activa ON public.entregas_epp USING btree (id_epp) WHERE ((estado)::text = 'ACTIVA'::text);


--
-- Name: ux_entrega_epp_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_entrega_epp_uuid ON public.entregas_epp USING btree (uuid_registro);


--
-- Name: ux_epp_numero_serie; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_epp_numero_serie ON public.epps USING btree (numero_serie) WHERE (numero_serie IS NOT NULL);


--
-- Name: ux_evidencia_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_evidencia_uuid ON public.evidencias USING btree (uuid_registro);


--
-- Name: ux_herramienta_serie; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_herramienta_serie ON public.herramientas USING btree (numero_serie) WHERE (numero_serie IS NOT NULL);


--
-- Name: ux_incidencia_herramienta_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_incidencia_herramienta_uuid ON public.incidencias_herramienta USING btree (uuid_registro);


--
-- Name: ux_incidencia_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_incidencia_uuid ON public.incidencias USING btree (uuid_registro);


--
-- Name: ux_movimiento_epp_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_movimiento_epp_uuid ON public.movimientos_stock_epp USING btree (uuid_registro);


--
-- Name: ux_reposicion_entrega_nueva; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_reposicion_entrega_nueva ON public.reposiciones_epp USING btree (id_entrega_nueva) WHERE (id_entrega_nueva IS NOT NULL);


--
-- Name: ux_reposicion_uuid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_reposicion_uuid ON public.reposiciones_epp USING btree (uuid_registro);


--
-- Name: ux_trabajador_correo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_trabajador_correo ON public.trabajadores USING btree (correo) WHERE (correo IS NOT NULL);


--
-- Name: ux_trabajador_cuadrilla_activa; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_trabajador_cuadrilla_activa ON public.trabajadores_cuadrilla USING btree (id_asignacion) WHERE (fecha_fin IS NULL);


--
-- Name: ux_usuario_rol_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_usuario_rol_activo ON public.usuarios_rol USING btree (id_usuario, id_rol) WHERE (fecha_fin IS NULL);


--
-- Name: ux_usuario_trabajador; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_usuario_trabajador ON public.usuarios USING btree (id_trabajador) WHERE (id_trabajador IS NOT NULL);


--
-- Name: actas trg_impedir_cambio_obra_acta; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_impedir_cambio_obra_acta BEFORE UPDATE OF id_obra ON public.actas FOR EACH ROW EXECUTE FUNCTION public.fn_impedir_cambio_obra();


--
-- Name: actividades trg_impedir_cambio_obra_actividad; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_impedir_cambio_obra_actividad BEFORE UPDATE OF id_obra ON public.actividades FOR EACH ROW EXECUTE FUNCTION public.fn_impedir_cambio_obra();


--
-- Name: asignaciones_trabajador_obra trg_impedir_cambio_obra_asignacion; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_impedir_cambio_obra_asignacion BEFORE UPDATE OF id_obra ON public.asignaciones_trabajador_obra FOR EACH ROW EXECUTE FUNCTION public.fn_impedir_cambio_obra();


--
-- Name: cuadrillas_obra trg_impedir_cambio_obra_cuadrilla; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_impedir_cambio_obra_cuadrilla BEFORE UPDATE OF id_obra ON public.cuadrillas_obra FOR EACH ROW EXECUTE FUNCTION public.fn_impedir_cambio_obra();


--
-- Name: incidencias trg_impedir_cambio_obra_incidencia; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_impedir_cambio_obra_incidencia BEFORE UPDATE OF id_obra ON public.incidencias FOR EACH ROW EXECUTE FUNCTION public.fn_impedir_cambio_obra();


--
-- Name: actas_avance trg_validar_acta_avance_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_acta_avance_obra BEFORE INSERT OR UPDATE OF id_acta, id_avance ON public.actas_avance FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: actas_incidencia trg_validar_acta_incidencia_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_acta_incidencia_obra BEFORE INSERT OR UPDATE OF id_acta, id_incidencia ON public.actas_incidencia FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: actividades_incidencia trg_validar_actividad_incidencia_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_actividad_incidencia_obra BEFORE INSERT OR UPDATE OF id_incidencia, id_actividad ON public.actividades_incidencia FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: actividades trg_validar_responsable_actividad_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_responsable_actividad_obra BEFORE INSERT OR UPDATE OF id_obra, id_asignacion_responsable ON public.actividades FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: trabajadores_acta trg_validar_trabajador_acta_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_trabajador_acta_obra BEFORE INSERT OR UPDATE OF id_acta, id_asignacion ON public.trabajadores_acta FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: trabajadores_actividad trg_validar_trabajador_actividad_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_trabajador_actividad_obra BEFORE INSERT OR UPDATE OF id_actividad, id_asignacion ON public.trabajadores_actividad FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: trabajadores_cuadrilla trg_validar_trabajador_cuadrilla_obra; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_validar_trabajador_cuadrilla_obra BEFORE INSERT OR UPDATE OF id_asignacion, id_cuadrilla_obra ON public.trabajadores_cuadrilla FOR EACH ROW EXECUTE FUNCTION public.fn_validar_asociacion_misma_obra();


--
-- Name: actas_avance fk_acta_avance_acta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas_avance
    ADD CONSTRAINT fk_acta_avance_acta FOREIGN KEY (id_acta) REFERENCES public.actas(id_acta);


--
-- Name: actas_avance fk_acta_avance_avance; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas_avance
    ADD CONSTRAINT fk_acta_avance_avance FOREIGN KEY (id_avance) REFERENCES public.avances(id_avance);


--
-- Name: actas_incidencia fk_acta_incidencia_acta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas_incidencia
    ADD CONSTRAINT fk_acta_incidencia_acta FOREIGN KEY (id_acta) REFERENCES public.actas(id_acta);


--
-- Name: actas_incidencia fk_acta_incidencia_incidencia; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas_incidencia
    ADD CONSTRAINT fk_acta_incidencia_incidencia FOREIGN KEY (id_incidencia) REFERENCES public.incidencias(id_incidencia);


--
-- Name: actas fk_acta_obra; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas
    ADD CONSTRAINT fk_acta_obra FOREIGN KEY (id_obra) REFERENCES public.obras(id_obra);


--
-- Name: actas fk_acta_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actas
    ADD CONSTRAINT fk_acta_usuario FOREIGN KEY (id_usuario_creador) REFERENCES public.usuarios(id_usuario);


--
-- Name: actividades fk_actividad_obra; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades
    ADD CONSTRAINT fk_actividad_obra FOREIGN KEY (id_obra) REFERENCES public.obras(id_obra);


--
-- Name: actividades fk_actividad_responsable; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades
    ADD CONSTRAINT fk_actividad_responsable FOREIGN KEY (id_asignacion_responsable) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: asignaciones_herramienta fk_ah_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_herramienta
    ADD CONSTRAINT fk_ah_asignacion FOREIGN KEY (id_asignacion_trabajador_obra) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: asignaciones_herramienta fk_ah_herramienta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_herramienta
    ADD CONSTRAINT fk_ah_herramienta FOREIGN KEY (id_herramienta) REFERENCES public.herramientas(id_herramienta);


--
-- Name: asignaciones_herramienta fk_ah_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_herramienta
    ADD CONSTRAINT fk_ah_usuario FOREIGN KEY (id_usuario_entrega) REFERENCES public.usuarios(id_usuario);


--
-- Name: actividades_incidencia fk_ai_actividad; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades_incidencia
    ADD CONSTRAINT fk_ai_actividad FOREIGN KEY (id_actividad) REFERENCES public.actividades(id_actividad);


--
-- Name: actividades_incidencia fk_ai_incidencia; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.actividades_incidencia
    ADD CONSTRAINT fk_ai_incidencia FOREIGN KEY (id_incidencia) REFERENCES public.incidencias(id_incidencia);


--
-- Name: aprobaciones_acta fk_aprobacion_acta_acta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aprobaciones_acta
    ADD CONSTRAINT fk_aprobacion_acta_acta FOREIGN KEY (id_acta) REFERENCES public.actas(id_acta);


--
-- Name: aprobaciones_acta fk_aprobacion_acta_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aprobaciones_acta
    ADD CONSTRAINT fk_aprobacion_acta_usuario FOREIGN KEY (id_usuario) REFERENCES public.usuarios(id_usuario);


--
-- Name: asistencias fk_asistencia_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asistencias
    ADD CONSTRAINT fk_asistencia_asignacion FOREIGN KEY (id_asignacion) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: asistencias fk_asistencia_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asistencias
    ADD CONSTRAINT fk_asistencia_usuario FOREIGN KEY (id_usuario_registro) REFERENCES public.usuarios(id_usuario);


--
-- Name: asignaciones_trabajador_obra fk_ato_especialidad; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_trabajador_obra
    ADD CONSTRAINT fk_ato_especialidad FOREIGN KEY (id_trabajador, id_especialidad) REFERENCES public.trabajadores_especialidad(id_trabajador, id_especialidad);


--
-- Name: asignaciones_trabajador_obra fk_ato_obra; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_trabajador_obra
    ADD CONSTRAINT fk_ato_obra FOREIGN KEY (id_obra) REFERENCES public.obras(id_obra);


--
-- Name: asignaciones_trabajador_obra fk_ato_trabajador; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.asignaciones_trabajador_obra
    ADD CONSTRAINT fk_ato_trabajador FOREIGN KEY (id_trabajador) REFERENCES public.trabajadores(id_trabajador);


--
-- Name: auditorias fk_auditoria_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditorias
    ADD CONSTRAINT fk_auditoria_usuario FOREIGN KEY (id_usuario) REFERENCES public.usuarios(id_usuario);


--
-- Name: avances fk_avance_actividad; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.avances
    ADD CONSTRAINT fk_avance_actividad FOREIGN KEY (id_actividad) REFERENCES public.actividades(id_actividad);


--
-- Name: avances fk_avance_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.avances
    ADD CONSTRAINT fk_avance_usuario FOREIGN KEY (id_usuario_registro) REFERENCES public.usuarios(id_usuario);


--
-- Name: correcciones_asistencia fk_ca_aprueba; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.correcciones_asistencia
    ADD CONSTRAINT fk_ca_aprueba FOREIGN KEY (id_usuario_aprueba) REFERENCES public.usuarios(id_usuario);


--
-- Name: correcciones_asistencia fk_ca_asistencia; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.correcciones_asistencia
    ADD CONSTRAINT fk_ca_asistencia FOREIGN KEY (id_asistencia) REFERENCES public.asistencias(id_asistencia);


--
-- Name: correcciones_asistencia fk_ca_solicita; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.correcciones_asistencia
    ADD CONSTRAINT fk_ca_solicita FOREIGN KEY (id_usuario_solicita) REFERENCES public.usuarios(id_usuario);


--
-- Name: cuadrillas_obra fk_co_cuadrilla; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuadrillas_obra
    ADD CONSTRAINT fk_co_cuadrilla FOREIGN KEY (id_cuadrilla) REFERENCES public.cuadrillas(id_cuadrilla);


--
-- Name: cuadrillas_obra fk_co_obra; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cuadrillas_obra
    ADD CONSTRAINT fk_co_obra FOREIGN KEY (id_obra) REFERENCES public.obras(id_obra);


--
-- Name: entregas_epp fk_entrega_epp_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.entregas_epp
    ADD CONSTRAINT fk_entrega_epp_asignacion FOREIGN KEY (id_asignacion) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: entregas_epp fk_entrega_epp_epp; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.entregas_epp
    ADD CONSTRAINT fk_entrega_epp_epp FOREIGN KEY (id_epp) REFERENCES public.epps(id_epp);


--
-- Name: entregas_epp fk_entrega_epp_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.entregas_epp
    ADD CONSTRAINT fk_entrega_epp_usuario FOREIGN KEY (id_usuario_entrega) REFERENCES public.usuarios(id_usuario);


--
-- Name: epps fk_epp_tipo; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.epps
    ADD CONSTRAINT fk_epp_tipo FOREIGN KEY (id_tipo_epp) REFERENCES public.tipos_epp(id_tipo_epp);


--
-- Name: evidencias fk_evidencia_avance; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencias
    ADD CONSTRAINT fk_evidencia_avance FOREIGN KEY (id_avance) REFERENCES public.avances(id_avance);


--
-- Name: evidencias fk_evidencia_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.evidencias
    ADD CONSTRAINT fk_evidencia_usuario FOREIGN KEY (id_usuario_registro) REFERENCES public.usuarios(id_usuario);


--
-- Name: herramientas fk_herramienta_tipo; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.herramientas
    ADD CONSTRAINT fk_herramienta_tipo FOREIGN KEY (id_tipo_herramienta) REFERENCES public.tipos_herramienta(id_tipo_herramienta);


--
-- Name: incidencias_herramienta fk_ih_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.incidencias_herramienta
    ADD CONSTRAINT fk_ih_asignacion FOREIGN KEY (id_asignacion_herramienta) REFERENCES public.asignaciones_herramienta(id_asignacion_herramienta);


--
-- Name: incidencias_herramienta fk_ih_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.incidencias_herramienta
    ADD CONSTRAINT fk_ih_usuario FOREIGN KEY (id_usuario_registro) REFERENCES public.usuarios(id_usuario);


--
-- Name: incidencias fk_incidencia_obra; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.incidencias
    ADD CONSTRAINT fk_incidencia_obra FOREIGN KEY (id_obra) REFERENCES public.obras(id_obra);


--
-- Name: incidencias fk_incidencia_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.incidencias
    ADD CONSTRAINT fk_incidencia_usuario FOREIGN KEY (id_usuario_registro) REFERENCES public.usuarios(id_usuario);


--
-- Name: movimientos_stock_epp fk_movimiento_epp_entrega; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimientos_stock_epp
    ADD CONSTRAINT fk_movimiento_epp_entrega FOREIGN KEY (id_entrega_epp) REFERENCES public.entregas_epp(id_entrega_epp);


--
-- Name: movimientos_stock_epp fk_movimiento_epp_epp; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimientos_stock_epp
    ADD CONSTRAINT fk_movimiento_epp_epp FOREIGN KEY (id_epp) REFERENCES public.epps(id_epp);


--
-- Name: movimientos_stock_epp fk_movimiento_epp_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.movimientos_stock_epp
    ADD CONSTRAINT fk_movimiento_epp_usuario FOREIGN KEY (id_usuario) REFERENCES public.usuarios(id_usuario);


--
-- Name: reposiciones_epp fk_reposicion_aprueba; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reposiciones_epp
    ADD CONSTRAINT fk_reposicion_aprueba FOREIGN KEY (id_usuario_aprueba) REFERENCES public.usuarios(id_usuario);


--
-- Name: reposiciones_epp fk_reposicion_nueva; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reposiciones_epp
    ADD CONSTRAINT fk_reposicion_nueva FOREIGN KEY (id_entrega_nueva) REFERENCES public.entregas_epp(id_entrega_epp);


--
-- Name: reposiciones_epp fk_reposicion_origen; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reposiciones_epp
    ADD CONSTRAINT fk_reposicion_origen FOREIGN KEY (id_entrega_origen) REFERENCES public.entregas_epp(id_entrega_epp);


--
-- Name: reposiciones_epp fk_reposicion_solicita; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.reposiciones_epp
    ADD CONSTRAINT fk_reposicion_solicita FOREIGN KEY (id_usuario_solicita) REFERENCES public.usuarios(id_usuario);


--
-- Name: roles_permiso fk_rol_permiso_permiso; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles_permiso
    ADD CONSTRAINT fk_rol_permiso_permiso FOREIGN KEY (id_permiso) REFERENCES public.permisos(id_permiso);


--
-- Name: roles_permiso fk_rol_permiso_rol; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles_permiso
    ADD CONSTRAINT fk_rol_permiso_rol FOREIGN KEY (id_rol) REFERENCES public.roles(id_rol);


--
-- Name: trabajadores_actividad fk_ta_actividad; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_actividad
    ADD CONSTRAINT fk_ta_actividad FOREIGN KEY (id_actividad) REFERENCES public.actividades(id_actividad);


--
-- Name: trabajadores_actividad fk_ta_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_actividad
    ADD CONSTRAINT fk_ta_asignacion FOREIGN KEY (id_asignacion) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: trabajadores_cuadrilla fk_tc_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_cuadrilla
    ADD CONSTRAINT fk_tc_asignacion FOREIGN KEY (id_asignacion) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: trabajadores_cuadrilla fk_tc_cuadrilla_obra; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_cuadrilla
    ADD CONSTRAINT fk_tc_cuadrilla_obra FOREIGN KEY (id_cuadrilla_obra) REFERENCES public.cuadrillas_obra(id_cuadrilla_obra);


--
-- Name: trabajadores_especialidad fk_te_especialidad; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_especialidad
    ADD CONSTRAINT fk_te_especialidad FOREIGN KEY (id_especialidad) REFERENCES public.especialidades(id_especialidad);


--
-- Name: trabajadores_especialidad fk_te_trabajador; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_especialidad
    ADD CONSTRAINT fk_te_trabajador FOREIGN KEY (id_trabajador) REFERENCES public.trabajadores(id_trabajador);


--
-- Name: tipos_herramienta fk_tipo_herramienta_categoria; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_herramienta
    ADD CONSTRAINT fk_tipo_herramienta_categoria FOREIGN KEY (id_categoria) REFERENCES public.categorias_herramienta(id_categoria);


--
-- Name: trabajadores_acta fk_trabajador_acta_acta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_acta
    ADD CONSTRAINT fk_trabajador_acta_acta FOREIGN KEY (id_acta) REFERENCES public.actas(id_acta);


--
-- Name: trabajadores_acta fk_trabajador_acta_asignacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores_acta
    ADD CONSTRAINT fk_trabajador_acta_asignacion FOREIGN KEY (id_asignacion) REFERENCES public.asignaciones_trabajador_obra(id_asignacion);


--
-- Name: trabajadores fk_trabajador_cargo; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trabajadores
    ADD CONSTRAINT fk_trabajador_cargo FOREIGN KEY (id_cargo) REFERENCES public.cargos(id_cargo);


--
-- Name: usuarios_rol fk_usuario_rol_rol; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_rol
    ADD CONSTRAINT fk_usuario_rol_rol FOREIGN KEY (id_rol) REFERENCES public.roles(id_rol);


--
-- Name: usuarios_rol fk_usuario_rol_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_rol
    ADD CONSTRAINT fk_usuario_rol_usuario FOREIGN KEY (id_usuario) REFERENCES public.usuarios(id_usuario);


--
-- Name: usuarios fk_usuario_trabajador; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT fk_usuario_trabajador FOREIGN KEY (id_trabajador) REFERENCES public.trabajadores(id_trabajador);


--
-- PostgreSQL database dump complete
--

\unrestrict r5CtQkL1GDNy2ZelKyaR4YTZBe6T7sdHsXmFxh8KA0EVaS7cpcELABom2x9H1K1
