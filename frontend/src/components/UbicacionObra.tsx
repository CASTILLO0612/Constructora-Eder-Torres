import { useState } from "react";
import { LocateFixed, MapPin } from "lucide-react";
import { obtenerGps } from "../features/Asistencia/geocerca";
import { Boton, ErrorVisible } from "./Interfaz";
import type { Fila } from "../features/Modulos/catalogo";

export function UbicacionObra({
    cambiar,
}: {
    cambiar: (campos: Fila) => void;
}) {
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const localizar = async () => {
        setOcupado(true);
        setError(undefined);
        try {
            const lectura = await obtenerGps();
            cambiar({ latitud: lectura.latitud, longitud: lectura.longitud });
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
        }
    };
    return (
        <div className="contexto-formulario">
            <div className="mensaje">
                <MapPin size={18} />
                <span>
                    Define el centro real de la obra y su radio en metros. Usa
                    tu ubicación actual solo si estás en ese centro; también
                    puedes introducir las coordenadas conocidas.
                </span>
            </div>
            <Boton
                type="button"
                variante="secundario"
                ocupado={ocupado}
                onClick={localizar}
            >
                <LocateFixed size={17} />
                Usar mi ubicación para el centro
            </Boton>
            {error !== undefined && <ErrorVisible error={error} />}
        </div>
    );
}
