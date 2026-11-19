import { useQuery } from "@tanstack/react-query";
import { api, construirRuta, ErrorApi } from "../api/cliente";
import { guardarConsulta, leerConsulta } from "../offline/almacenamiento";
import { useSesion } from "../app/Sesion";
import { buscarConsultaGuardada } from "../offline/busquedaLocal";

export function useDatos<T>(
    ruta: string | null,
    parametros: Record<string, unknown> = {},
    todasLasPaginas = false,
) {
    const { perfil } = useSesion();
    const clave = ruta
        ? construirRuta(ruta, parametros) +
          (todasLasPaginas ? "#lista-completa" : "")
        : "";
    const consulta = useQuery({
        queryKey: ["datos", perfil?.idUsuario, clave],
        enabled: !!perfil && !!ruta,
        queryFn: async ({ signal }) => {
            try {
                if (!navigator.onLine)
                    throw new Error("Consulta sin conexión.");
                let datos: T = await api.leer<T>(ruta!, parametros, signal);
                if (todasLasPaginas && Array.isArray(datos)) {
                    const acumulados: unknown[] = [...datos];
                    const tamano = Number(parametros.tamanoPagina) || 100;
                    let pagina = Number(parametros.pagina) || 1;
                    let ultimaCantidad = datos.length;
                    while (ultimaCantidad === tamano) {
                        const siguientes = await api.leer<unknown[]>(
                            ruta!,
                            {
                                ...parametros,
                                pagina: ++pagina,
                                tamanoPagina: tamano,
                            },
                            signal,
                        );
                        ultimaCantidad = siguientes.length;
                        acumulados.push(...siguientes);
                    }
                    datos = acumulados as T;
                }
                if (!parametros.buscar)
                    await guardarConsulta(perfil!.idUsuario, clave, datos);
                return {
                    datos,
                    cache: false,
                    guardadoEn: new Date().toISOString(),
                };
            } catch (e) {
                if (e instanceof ErrorApi || signal.aborted) throw e;
                const local =
                    (await leerConsulta(perfil!.idUsuario, clave)) ||
                    (parametros.pagina
                        ? await buscarConsultaGuardada(
                              perfil!.idUsuario,
                              ruta!,
                              parametros,
                          )
                        : undefined);
                if (!local)
                    throw !navigator.onLine
                        ? new Error(
                              "Esta consulta no está guardada en el dispositivo. Abre esta vista o prepara la obra con conexión antes de usarla sin conexión.",
                          )
                        : e;
                return {
                    datos: local.datos as T,
                    cache: true,
                    guardadoEn: local.guardadoEn,
                };
            }
        },
        staleTime: 45_000,
        gcTime: parametros.buscar ? 2 * 60_000 : 10 * 60_000,
        retry: false,
        refetchOnWindowFocus: false,
        networkMode: "always",
    });
    return {
        ...consulta,
        datos: consulta.data?.datos,
        cache: consulta.data?.cache,
        guardadoEn: consulta.data?.guardadoEn,
    };
}
