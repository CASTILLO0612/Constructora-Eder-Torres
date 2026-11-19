import { useState } from "react";
import { Download } from "lucide-react";
import { api, construirRuta } from "../api/cliente";
import { guardarConsulta } from "../offline/almacenamiento";
import { useSesion } from "../app/Sesion";
import { permisos as p } from "../api/permisos";
import { useAviso } from "../app/Avisos";
import { Boton, ErrorVisible } from "./Interfaz";

export function PrepararObra({ idObra }: { idObra: number }) {
    const { perfil, puede } = useSesion();
    const aviso = useAviso();
    const [ocupado, setOcupado] = useState(false);
    const [avance, setAvance] = useState("");
    const [error, setError] = useState<unknown>();
    const preparar = async () => {
        setOcupado(true);
        setError(undefined);
        try {
            if (!navigator.onLine)
                throw new Error(
                    "Conecta el dispositivo para preparar los catálogos de la obra.",
                );
            const persistir = async (ruta: string, datos: unknown) => {
                if (!(await guardarConsulta(perfil!.idUsuario, ruta, datos)))
                    throw new Error(
                        "No se pudo guardar el contexto local. Revisa el espacio y los permisos de almacenamiento del navegador.",
                    );
            };
            const pasos: {
                ruta: string;
                parametros?: Record<string, unknown>;
            }[] = [{ ruta: "/api/obras" }];
            if (puede(p.consultarTrabajadores, p.gestionarTrabajadores))
                pasos.push(
                    ...[
                        "trabajadores",
                        "cargos",
                        "especialidades",
                        "cuadrillas",
                    ].map((n) => ({ ruta: `/api/personal/${n}` })),
                    {
                        ruta: `/api/personal/obras/${idObra}/asignaciones`,
                        parametros: { soloVigentes: true },
                    },
                    { ruta: `/api/personal/obras/${idObra}/cuadrillas` },
                    {
                        ruta: `/api/personal/obras/${idObra}/miembros-cuadrilla`,
                    },
                );
            if (puede(p.registrarAsistencia))
                pasos.push({
                    ruta: "/api/asistencias",
                    parametros: { idObra, estado: "ABIERTA" },
                });
            if (
                puede(
                    p.registrarAvances,
                    p.registrarEvidencias,
                    p.registrarIncidenciasObra,
                    p.elaborarActas,
                )
            )
                pasos.push(
                    ...["actividades", "avances", "incidencias"].map((n) => ({
                        ruta: `/api/seguimiento/obras/${idObra}/${n}`,
                    })),
                );
            if (puede(p.gestionarInventarioEpp))
                pasos.push(
                    { ruta: "/api/epp/tipos" },
                    {
                        ruta: "/api/epp/existencias",
                        parametros: { estado: "DISPONIBLE" },
                    },
                );
            if (puede(p.registrarEntregasEpp, p.gestionarReposicionesEpp))
                pasos.push({
                    ruta: "/api/epp/entregas",
                    parametros: { idObra, estado: "ACTIVA" },
                });
            if (puede(p.gestionarInventarioHerramientas))
                pasos.push({
                    ruta: "/api/herramientas",
                    parametros: { estado: "DISPONIBLE" },
                });
            if (puede(p.gestionarAsignacionesHerramientas))
                pasos.push({
                    ruta: "/api/herramientas/asignaciones",
                    parametros: { idObra, estado: "ACTIVA" },
                });
            const obra = await api.leer(`/api/obras/${idObra}`);
            await persistir(construirRuta(`/api/obras/${idObra}`), obra);
            for (let i = 0; i < pasos.length; i++) {
                const paso = pasos[i];
                const completa: unknown[] = [];
                setAvance(`${i + 1}/${pasos.length}`);
                for (let pagina = 1; ; pagina++) {
                    const parametros = {
                        ...paso.parametros,
                        pagina,
                        tamanoPagina: 100,
                    };
                    const datos = await api.leer<unknown[]>(
                        paso.ruta,
                        parametros,
                    );
                    completa.push(...datos);
                    await persistir(
                        construirRuta(paso.ruta, parametros),
                        datos,
                    );
                    if (pagina === 1)
                        await persistir(
                            construirRuta(paso.ruta, {
                                ...paso.parametros,
                                pagina: 1,
                                tamanoPagina: 25,
                            }),
                            datos.slice(0, 25),
                        );
                    if (datos.length < 100) break;
                }
                await persistir(
                    construirRuta(paso.ruta, {
                        ...paso.parametros,
                        pagina: 1,
                        tamanoPagina: 100,
                    }) + "#lista-completa",
                    completa,
                );
            }
            aviso(
                "La obra y sus catálogos se prepararon para capturas sin conexión. Los cambios se volverán a validar al sincronizar.",
            );
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
            setAvance("");
        }
    };
    return (
        <div className="preparacion-obra">
            <Boton
                variante="secundario"
                ocupado={ocupado}
                onClick={preparar}
                disabled={!navigator.onLine}
            >
                <Download size={16} />
                {ocupado ? `Preparando ${avance}` : "Preparar obra offline"}
            </Boton>
            {error !== undefined && <ErrorVisible error={error} />}
        </div>
    );
}
