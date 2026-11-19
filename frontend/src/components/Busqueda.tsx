import { useId } from "react";
import { Search, X } from "lucide-react";
import { limitesBusqueda } from "../api/busqueda";

export function Busqueda({
    valor,
    cambiar,
    nombre,
    id,
    ayuda = "",
    esperando = false,
    opciones = false,
}: {
    valor: string;
    cambiar: (valor: string) => void;
    nombre: string;
    id?: string;
    ayuda?: string;
    esperando?: boolean;
    opciones?: boolean;
}) {
    const idAyuda = useId();
    return (
        <>
            <div className={opciones ? "busqueda-selector" : "buscar-tabla"}>
                <Search size={18} aria-hidden="true" />
                <input
                    id={id}
                    aria-label={nombre}
                    placeholder={
                        opciones ? "Buscar opciones…" : "Buscar registros…"
                    }
                    value={valor}
                    onChange={(e) => cambiar(e.target.value)}
                    maxLength={limitesBusqueda.longitudMaxima}
                    autoComplete="off"
                    enterKeyHint="search"
                    aria-describedby={ayuda || esperando ? idAyuda : undefined}
                />
                {!!valor && (
                    <button
                        type="button"
                        className="limpiar-busqueda"
                        aria-label={`Limpiar búsqueda: ${nombre.replace(/^Buscar\s+/i, "").toLocaleLowerCase()}`}
                        onClick={() => cambiar("")}
                    >
                        <X size={16} aria-hidden="true" />
                    </button>
                )}
            </div>
            <span
                id={idAyuda}
                className="ayuda-campo estado-busqueda"
                role="status"
                aria-live="polite"
            >
                {ayuda || (esperando ? "Buscando…" : "")}
            </span>
        </>
    );
}
