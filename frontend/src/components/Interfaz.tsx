import {
    useEffect,
    useRef,
    type ButtonHTMLAttributes,
    type ReactNode,
} from "react";
import {
    CircleCheck,
    Circle,
    Clock,
    TriangleAlert,
    X,
    LoaderCircle,
    Inbox,
    ArrowLeft,
    ChevronRight,
} from "lucide-react";
import { etiqueta } from "../app/formatos";
import { mensajeError, ErrorApi } from "../api/cliente";
import { EsqueletoCarga, type VarianteCarga } from "./EsqueletoCarga";
import { mensajeParaUsuario } from "../app/mensajes";

export function Boton({
    children,
    variante = "primario",
    ocupado,
    className = "",
    ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
    variante?: "primario" | "secundario" | "texto" | "peligro";
    ocupado?: boolean;
}) {
    return (
        <button
            {...props}
            disabled={props.disabled || ocupado}
            className={`boton boton-${variante} ${className}`}
            aria-busy={ocupado || undefined}
        >
            {ocupado && (
                <LoaderCircle className="girar" size={18} aria-hidden="true" />
            )}
            {children}
        </button>
    );
}
export function Estado({ valor }: { valor: unknown }) {
    const codigo = String(valor || "SIN_ESTADO");
    const Icono =
        /APROBADA|COMPLETA|REGISTRADO|CONFIRMADA|SINCRONIZADO|ACTIV[AO]|DISPONIBLE/.test(
            codigo,
        )
            ? CircleCheck
            : /RECHAZ|ATENCION|PERDID|DANAD|RETRAS/.test(codigo)
              ? TriangleAlert
              : /PENDIENTE|BORRADOR|ENVIANDO|EN_PROCESO/.test(codigo)
                ? Clock
                : Circle;
    return (
        <span
            className={`estado ${/PENDIENTE|BORRADOR/.test(codigo) ? "estado-pendiente" : ""}`}
        >
            <Icono size={13} aria-hidden="true" />
            {etiqueta(codigo)}
        </span>
    );
}
export function Cargando({
    texto = "Cargando información…",
    variante = "lista",
    filas,
}: {
    texto?: string;
    variante?: VarianteCarga;
    filas?: number;
}) {
    return <EsqueletoCarga texto={texto} variante={variante} filas={filas} />;
}
export function ErrorVisible({ error }: { error: unknown }) {
    return (
        <div className="mensaje mensaje-error" role="alert">
            <TriangleAlert size={19} aria-hidden="true" />
            <div>
                <strong>No se pudo completar la operación</strong>
                <p>{mensajeError(error)}</p>
                {error instanceof ErrorApi && error.problema.errors && (
                    <ul>
                        {Object.entries(error.problema.errors).map(
                            ([campo, mensajes]) => (
                                <li key={campo}>
                                    {mensajes
                                        .map((m) => mensajeParaUsuario(m))
                                        .join(" ")}
                                </li>
                            ),
                        )}
                    </ul>
                )}
            </div>
        </div>
    );
}
export function Vacio({
    titulo = "Todavía no hay registros",
    detalle = "Los registros aparecerán aquí cuando se incorporen.",
    accion,
}: {
    titulo?: string;
    detalle?: string;
    accion?: ReactNode;
}) {
    return (
        <div className="estado-vacio">
            <span className="vacio-icono">
                <Inbox size={28} />
            </span>
            <h3>{titulo}</h3>
            <p>{detalle}</p>
            {accion}
        </div>
    );
}
export function Encabezado({
    titulo,
    acciones,
}: {
    titulo: string;
    acciones?: ReactNode;
}) {
    return (
        <div className="encabezado-pagina">
            <div>
                <h1>{titulo}</h1>
            </div>
            {acciones && <div className="acciones-pagina">{acciones}</div>}
        </div>
    );
}
export function Modal({
    titulo,
    descripcion,
    children,
    cerrar,
    amplio = false,
}: {
    titulo: string;
    descripcion?: string;
    children: ReactNode;
    cerrar: () => void;
    amplio?: boolean;
}) {
    const referencia = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        const d = referencia.current!;
        d.showModal();
        const cancelar = (e: Event) => {
            e.preventDefault();
            cerrar();
        };
        d.addEventListener("cancel", cancelar);
        return () => {
            d.removeEventListener("cancel", cancelar);
            d.close();
        };
    }, [cerrar]);
    return (
        <dialog
            ref={referencia}
            className={`modal ${amplio ? "modal-amplio" : ""}`}
            aria-labelledby="titulo-modal"
        >
            <div className="cabecera-modal">
                <div>
                    <h2 id="titulo-modal">{titulo}</h2>
                    {descripcion && <p>{descripcion}</p>}
                </div>
                <button
                    className="boton-icono"
                    aria-label="Cerrar ventana"
                    onClick={cerrar}
                >
                    <X size={21} />
                </button>
            </div>
            <div className="cuerpo-modal">{children}</div>
        </dialog>
    );
}
export function Paginacion({
    pagina,
    siguiente,
    cambiar,
    cantidad,
}: {
    pagina: number;
    siguiente: boolean;
    cambiar: (p: number) => void;
    cantidad: number;
}) {
    return (
        <div className="paginacion">
            <span>
                {cantidad} {cantidad === 1 ? "registro" : "registros"} en esta
                página
            </span>
            <div>
                <Boton
                    variante="secundario"
                    disabled={pagina === 1}
                    onClick={() => cambiar(pagina - 1)}
                >
                    <ArrowLeft size={16} />
                    Anterior
                </Boton>
                <span className="numero-pagina" aria-label={`Página ${pagina}`}>
                    {pagina}
                </span>
                <Boton
                    variante="secundario"
                    disabled={!siguiente}
                    onClick={() => cambiar(pagina + 1)}
                >
                    Siguiente
                    <ChevronRight size={16} />
                </Boton>
            </div>
        </div>
    );
}
