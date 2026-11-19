import { useState } from "react";
import { Link } from "react-router-dom";
import { Clock } from "lucide-react";
import { useDatos } from "../../hooks/usoDatos";
import {
    Encabezado,
    Cargando,
    ErrorVisible,
    Vacio,
    Paginacion,
} from "../../components/Interfaz";
import { TablaDatos } from "../../components/TablaDatos";
import type { Fila } from "../Modulos/catalogo";

export default function PaginaHistorialPropio() {
    const [pagina, setPagina] = useState(1);
    const [desde, setDesde] = useState("");
    const [hasta, setHasta] = useState("");
    const registros = useDatos<Fila[]>("/api/asistencias", {
        fechaDesde: desde,
        fechaHasta: hasta,
        pagina,
        tamanoPagina: 25,
    });
    return (
        <>
            <Encabezado
                titulo="Mi historial"
                acciones={
                    <Link to="/" className="boton boton-primario">
                        <Clock size={17} />
                        Marcar asistencia
                    </Link>
                }
            />
            <section className="panel-registros">
                <div className="barra-filtros">
                    <label className="campo">
                        Desde
                        <input
                            type="date"
                            value={desde}
                            onChange={(e) => {
                                setDesde(e.target.value);
                                setPagina(1);
                            }}
                        />
                    </label>
                    <label className="campo">
                        Hasta
                        <input
                            type="date"
                            value={hasta}
                            onChange={(e) => {
                                setHasta(e.target.value);
                                setPagina(1);
                            }}
                        />
                    </label>
                </div>
                {registros.isLoading ? (
                    <Cargando variante="tabla" />
                ) : registros.error ? (
                    <ErrorVisible error={registros.error} />
                ) : !registros.datos?.length ? (
                    <Vacio
                        titulo="No hay registros para este período"
                        detalle="Tus marcaciones confirmadas aparecerán aquí."
                    />
                ) : (
                    <>
                        <TablaDatos
                            filas={registros.datos}
                            columnas={[
                                "fechaJornada",
                                "entradaEn",
                                "salidaEn",
                                "estado",
                            ]}
                            identidad="idAsistencia"
                        />
                        <Paginacion
                            pagina={pagina}
                            siguiente={registros.datos.length === 25}
                            cambiar={setPagina}
                            cantidad={registros.datos.length}
                        />
                    </>
                )}
            </section>
        </>
    );
}
