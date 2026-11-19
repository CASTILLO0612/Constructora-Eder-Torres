const positivo = (valor: string | undefined, respaldo: number) => {
    const numero = Number(valor);
    return Number.isFinite(numero) && numero > 0 ? numero : respaldo;
};

export const configuracion = {
    apiBase: (import.meta.env.VITE_API_BASE || "").replace(/\/$/, ""),
    zonaHoraria: import.meta.env.VITE_ZONA_HORARIA || "America/Managua",
    inactividadMs:
        positivo(import.meta.env.VITE_SESION_INACTIVIDAD_MINUTOS, 30) * 60_000,
    duracionSesionMs:
        positivo(import.meta.env.VITE_SESION_DURACION_HORAS, 8) * 3_600_000,
    esperaGpsMs: positivo(import.meta.env.VITE_GPS_TIEMPO_ESPERA_MS, 20_000),
    esperaSolicitudMs: positivo(
        import.meta.env.VITE_SOLICITUD_TIEMPO_ESPERA_MS,
        30_000,
    ),
    reintentoSesionMs: positivo(
        import.meta.env.VITE_SESION_REINTENTO_MS,
        15_000,
    ),
    archivoMaximoBytes:
        positivo(import.meta.env.VITE_ARCHIVO_MAXIMO_MIB, 15) * 1024 * 1024,
    mapaGoogleClave: (import.meta.env.VITE_GOOGLE_MAPS_API_KEY || "").trim(),
};
