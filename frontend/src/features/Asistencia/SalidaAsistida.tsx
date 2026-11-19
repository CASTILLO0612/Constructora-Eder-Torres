import { useState } from "react";
import { LocateFixed, LogOut } from "lucide-react";
import { Modal, Boton, ErrorVisible } from "../../components/Interfaz";
import { api } from "../../api/cliente";
import type { RespuestaObra } from "../../api/contratos";
import { leerConsulta } from "../../offline/almacenamiento";
import { obtenerGps, validarLectura } from "./geocerca";
import { encolar, sincronizar } from "../../offline/sincronizacion";
import { useSesion } from "../../app/Sesion";
import { useAviso } from "../../app/Avisos";
import { fecha } from "../../app/formatos";
import type { Fila } from "../Modulos/catalogo";

export default function SalidaAsistida({
    fila,
    cerrar,
}: {
    fila: Fila;
    cerrar: () => void;
}) {
    const { perfil } = useSesion();
    const aviso = useAviso();
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const registrar = async () => {
        setOcupado(true);
        setError(undefined);
        try {
            let obra: RespuestaObra;
            try {
                obra = await api.leer<RespuestaObra>(
                    `/api/obras/${fila.idObra}`,
                );
            } catch (e) {
                const cache = await leerConsulta(
                    perfil!.idUsuario,
                    `/api/obras/${fila.idObra}`,
                );
                if (!cache) throw e;
                obra = cache.datos as RespuestaObra;
            }
            if (
                obra.latitud == null ||
                obra.longitud == null ||
                obra.radioAsistenciaMetros == null
            )
                throw new Error("La obra no tiene geocerca configurada.");
            const gps = await obtenerGps();
            if (
                !validarLectura(
                    gps,
                    obra.latitud,
                    obra.longitud,
                    obra.radioAsistenciaMetros,
                ).habilitada
            )
                throw new Error(
                    "La ubicación y precisión GPS no cumplen el radio de la obra.",
                );
            await encolar(
                perfil!.idUsuario,
                "salidasAsistencia",
                `Salida · ${fila.nombreCompletoTrabajador}`,
                {
                    idAsistencia: fila.idAsistencia,
                    uuidRegistro: fila.uuidRegistro,
                    salidaEn: new Date().toISOString(),
                    latitudSalida: gps.latitud,
                    longitudSalida: gps.longitud,
                    precisionGpsSalidaMetros: gps.precision,
                },
            );
            aviso("Salida guardada en el dispositivo para confirmación.");
            void sincronizar(perfil!.idUsuario).catch(() => undefined);
            cerrar();
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
        }
    };
    return (
        <Modal titulo="Registrar salida asistida" cerrar={cerrar}>
            <p>
                <strong>{String(fila.nombreCompletoTrabajador)}</strong>
            </p>
            <p>Entrada: {fecha(fila.entradaEn, true)}</p>
            <div className="mensaje">
                <LocateFixed size={18} />
                Se obtiene la ubicación del dispositivo del Residente y se
                valida contra el radio de la obra.
            </div>
            {error !== undefined && <ErrorVisible error={error} />}
            <div className="pie-formulario">
                <Boton variante="secundario" onClick={cerrar}>
                    Cancelar
                </Boton>
                <Boton ocupado={ocupado} onClick={registrar}>
                    <LogOut size={17} />
                    Obtener GPS y registrar salida
                </Boton>
            </div>
        </Modal>
    );
}
