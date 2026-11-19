export type VarianteCarga =
    | "pagina"
    | "lista"
    | "tabla"
    | "detalle"
    | "formulario"
    | "grafico"
    | "tarjetas"
    | "opciones"
    | "imagen"
    | "mapa";

function Linea({ clase = "" }: { clase?: string }) {
    return <span className={`esqueleto-bloque ${clase}`} />;
}

export function EsqueletoCarga({
    variante = "lista",
    texto = "Cargando información…",
    filas = 4,
}: {
    variante?: VarianteCarga;
    texto?: string;
    filas?: number;
}) {
    const lista = (tabla = false) => (
        <div className={tabla ? "esqueleto-tabla" : "esqueleto-lista"}>
            {tabla && (
                <div className="esqueleto-fila esqueleto-fila-cabecera">
                    {Array.from({ length: 4 }, (_, i) => (
                        <Linea key={i} />
                    ))}
                </div>
            )}
            {Array.from({ length: filas }, (_, i) => (
                <div
                    className={`esqueleto-fila ${tabla ? "esqueleto-fila-tabla" : ""}`}
                    key={i}
                >
                    {tabla ? (
                        Array.from({ length: 4 }, (_, j) => <Linea key={j} />)
                    ) : (
                        <>
                            <Linea clase="esqueleto-avatar" />
                            <div className="esqueleto-textos">
                                <Linea />
                                <Linea clase="esqueleto-corta" />
                            </div>
                            <Linea clase="esqueleto-insignia" />
                        </>
                    )}
                </div>
            ))}
        </div>
    );
    let contenido;
    switch (variante) {
        case "pagina":
            contenido = (
                <>
                    <Linea clase="esqueleto-titulo" />
                    <div className="esqueleto-controles">
                        <Linea />
                        <Linea />
                        <Linea />
                    </div>
                    {lista(true)}
                </>
            );
            break;
        case "tabla":
            contenido = lista(true);
            break;
        case "detalle":
        case "formulario":
            contenido = (
                <div className="esqueleto-campos">
                    {Array.from({ length: 6 }, (_, i) => (
                        <div className="esqueleto-textos" key={i}>
                            <Linea clase="esqueleto-corta" />
                            <Linea
                                clase={
                                    variante === "formulario"
                                        ? "esqueleto-control"
                                        : "esqueleto-media"
                                }
                            />
                        </div>
                    ))}
                </div>
            );
            break;
        case "grafico":
            contenido = (
                <>
                    <div className="esqueleto-grafico">
                        {[35, 65, 50, 85, 60, 95, 45].map((alto, i) => (
                            <span
                                className="esqueleto-bloque"
                                key={i}
                                style={{ height: `${alto}%` }}
                            />
                        ))}
                    </div>
                    <Linea clase="esqueleto-media" />
                </>
            );
            break;
        case "tarjetas":
            contenido = (
                <div className="esqueleto-tarjetas">
                    {Array.from({ length: 4 }, (_, i) => (
                        <div className="esqueleto-tarjeta" key={i}>
                            <Linea clase="esqueleto-media" />
                            <Linea clase="esqueleto-numero" />
                            <Linea />
                        </div>
                    ))}
                </div>
            );
            break;
        case "opciones":
            contenido = (
                <div className="esqueleto-textos">
                    {Array.from({ length: 3 }, (_, i) => (
                        <Linea clase="esqueleto-control" key={i} />
                    ))}
                </div>
            );
            break;
        case "mapa":
            contenido = <Linea clase="esqueleto-mapa" />;
            break;
        case "imagen":
            contenido = <Linea clase="esqueleto-imagen" />;
            break;
        default:
            contenido = lista();
    }
    return (
        <div
            className={`esqueleto-carga carga-${variante}`}
            role="status"
            aria-busy="true"
        >
            <span className="visualmente-oculto">{texto}</span>
            <div className="esqueleto-estructura" aria-hidden="true">
                {contenido}
            </div>
        </div>
    );
}
