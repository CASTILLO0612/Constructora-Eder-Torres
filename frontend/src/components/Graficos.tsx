import { useId, useState, type PointerEvent } from "react";
import { numero, fecha } from "../app/formatos";
import { porcentajeActividad } from "../features/Inicio/datosGraficos";
import type { PuntoAsistencia } from "../features/Inicio/datosGraficos";

type Grupo = { codigo: string; nombre: string; valor: number };
export function GraficoDistribucion({
    grupos,
    seleccion,
    cambiar,
}: {
    grupos: Grupo[];
    seleccion: string | null;
    cambiar: (codigo: string | null) => void;
}) {
    const total = grupos.reduce((s, g) => s + g.valor, 0),
        circunferencia = 2 * Math.PI * 72;
    let recorrido = 0;
    const actual = grupos.find((g) => g.codigo === seleccion);
    return (
        <div className="distribucion-interactiva">
            <div className="anillo-grafico">
                <svg viewBox="0 0 190 190" aria-hidden="true">
                    <circle
                        cx="95"
                        cy="95"
                        r="72"
                        fill="none"
                        stroke="var(--borde)"
                        strokeWidth="21"
                    />
                    {grupos.map((g, i) => {
                        const longitud = total
                            ? (g.valor / total) * circunferencia
                            : 0;
                        const inicio = recorrido;
                        recorrido += longitud;
                        return (
                            <circle
                                key={g.codigo}
                                cx="95"
                                cy="95"
                                r="72"
                                fill="none"
                                stroke={`var(--grafico-${i + 1})`}
                                strokeWidth={seleccion === g.codigo ? 26 : 21}
                                strokeDasharray={`${Math.max(0, longitud - 3)} ${circunferencia}`}
                                strokeDashoffset={-inicio}
                                transform="rotate(-90 95 95)"
                                opacity={
                                    seleccion && seleccion !== g.codigo
                                        ? 0.28
                                        : 1
                                }
                                className="segmento-anillo"
                            />
                        );
                    })}
                </svg>
                <div className="centro-anillo">
                    <strong>{actual ? actual.valor : total}</strong>
                    <span>{actual ? actual.nombre : "obras"}</span>
                </div>
            </div>
            <div
                className="leyenda-grafico"
                aria-label="Filtrar obras por estado"
            >
                {grupos.map((g, i) => (
                    <button
                        type="button"
                        key={g.codigo}
                        aria-pressed={seleccion === g.codigo}
                        onClick={() =>
                            cambiar(seleccion === g.codigo ? null : g.codigo)
                        }
                    >
                        <i style={{ background: `var(--grafico-${i + 1})` }} />
                        <span>{g.nombre}</span>
                        <strong>{g.valor}</strong>
                        <small>
                            {numero(total ? (g.valor / total) * 100 : 0, 0)}%
                        </small>
                    </button>
                ))}
            </div>
            <p className="lectura-grafico" aria-live="polite">
                {actual
                    ? `${actual.valor} ${actual.nombre.toLowerCase()} en esta consulta. Pulsa de nuevo para ver todas.`
                    : "Selecciona un estado para explorar sus obras."}
            </p>
        </div>
    );
}

export function GraficoEvolucion({
    puntos,
    medida,
    diaSeleccionado,
    seleccionarDia,
}: {
    puntos: PuntoAsistencia[];
    medida: "marcaciones" | "horas";
    diaSeleccionado?: string;
    seleccionarDia: (dia: string) => void;
}) {
    const identificador = useId();
    const indice = puntos.findIndex((p) => p.fecha === diaSeleccionado);
    const maximo = Math.max(1, ...puntos.map((p) => p[medida]));
    const coordenadas = puntos.map((p, i) => ({
        x: 24 + (i * 592) / Math.max(1, puntos.length - 1),
        y: 210 - (p[medida] / maximo) * 170,
        p,
    }));
    const linea = coordenadas
        .map((c, i) => `${i ? "L" : "M"} ${c.x} ${c.y}`)
        .join(" ");
    const seleccionado =
        coordenadas[indice < 0 ? Math.max(0, coordenadas.length - 1) : indice];
    const unidades = medida === "horas" ? "h" : "registros";
    const seleccionarConPuntero = (e: PointerEvent<SVGSVGElement>) => {
        const r = e.currentTarget.getBoundingClientRect();
        if (!puntos.length || r.width <= 0) return;
        const x = ((e.clientX - r.left) / r.width) * 640;
        const i = Math.max(
            0,
            Math.min(
                puntos.length - 1,
                Math.round(((x - 24) / 592) * (puntos.length - 1)),
            ),
        );
        seleccionarDia(puntos[i].fecha);
    };
    return (
        <figure className="evolucion-grafico">
            <div className="escala-grafico">
                <span>
                    {numero(maximo)} {unidades}
                </span>
                <span>0</span>
            </div>
            <svg
                viewBox="0 0 640 240"
                aria-hidden="true"
                onPointerMove={seleccionarConPuntero}
                onPointerDown={seleccionarConPuntero}
            >
                <defs>
                    <linearGradient
                        id={identificador}
                        x1="0"
                        y1="0"
                        x2="0"
                        y2="1"
                    >
                        <stop
                            offset="0%"
                            stopColor="currentColor"
                            stopOpacity=".16"
                        />
                        <stop
                            offset="100%"
                            stopColor="currentColor"
                            stopOpacity=".015"
                        />
                    </linearGradient>
                </defs>
                {[40, 82.5, 125, 167.5, 210].map((y) => (
                    <line
                        key={y}
                        x1="24"
                        x2="616"
                        y1={y}
                        y2={y}
                        stroke="var(--borde)"
                        strokeDasharray="4 6"
                    />
                ))}
                {coordenadas.length > 0 && (
                    <>
                        <path
                            d={`${linea} L 616 210 L 24 210 Z`}
                            fill={`url(#${identificador})`}
                        />
                        <path
                            d={linea}
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="3"
                            strokeLinejoin="round"
                        />
                        {coordenadas.map((c, i) => (
                            <circle
                                key={c.p.fecha}
                                cx={c.x}
                                cy={c.y}
                                r={i === indice ? 7 : 4}
                                fill="var(--superficie)"
                                stroke="currentColor"
                                strokeWidth="2"
                            />
                        ))}
                        {seleccionado && (
                            <line
                                x1={seleccionado.x}
                                x2={seleccionado.x}
                                y1="30"
                                y2="210"
                                stroke="currentColor"
                                strokeDasharray="3 5"
                                opacity=".25"
                            />
                        )}
                    </>
                )}
            </svg>
            <div className="dias-grafico" aria-label="Consultar un día">
                {puntos.map((p, i) => (
                    <button
                        type="button"
                        key={p.fecha}
                        aria-pressed={indice === i}
                        aria-label={`${fecha(p.fecha)}: ${numero(p[medida])} ${unidades}`}
                        onClick={() => seleccionarDia(p.fecha)}
                        onFocus={() => seleccionarDia(p.fecha)}
                    >
                        <span>
                            {new Intl.DateTimeFormat("es-NI", {
                                weekday: "short",
                                timeZone: "UTC",
                            }).format(new Date(`${p.fecha}T12:00:00Z`))}
                        </span>
                        <strong>{p.fecha.slice(-2)}</strong>
                    </button>
                ))}
            </div>
            <figcaption>
                {seleccionado && (
                    <>
                        <strong>
                            {numero(seleccionado.p[medida])} {unidades}
                        </strong>
                        <span>
                            {fecha(seleccionado.p.fecha)} ·{" "}
                            {seleccionado.p.marcaciones} registros consultados
                        </span>
                    </>
                )}
            </figcaption>
        </figure>
    );
}

export function BarrasComparativas({
    filas,
    unidad,
}: {
    filas: {
        id: string;
        nombre: string;
        valor: number;
        maximo: number;
        detalle: string;
    }[];
    unidad: "porcentaje" | "unidades";
}) {
    const [seleccion, setSeleccion] = useState<string>();
    const actual = filas.find((f) => f.id === seleccion);
    return (
        <div className="barras-comparativas">
            <div className="filas-barras">
                {filas.map((f) => (
                    <button
                        key={f.id}
                        type="button"
                        aria-pressed={seleccion === f.id}
                        onClick={() => setSeleccion(f.id)}
                        onFocus={() => setSeleccion(f.id)}
                    >
                        <span className="etiqueta-barra">
                            {f.nombre}
                            <strong>
                                {numero(
                                    unidad === "porcentaje"
                                        ? porcentajeActividad(f.valor, f.maximo)
                                        : f.valor,
                                    unidad === "porcentaje" ? 0 : 1,
                                )}
                                {unidad === "porcentaje" ? "%" : ""}
                            </strong>
                        </span>
                        <span className="pista-barra" aria-hidden="true">
                            <i
                                style={{
                                    width: `${porcentajeActividad(f.valor, f.maximo)}%`,
                                }}
                            />
                        </span>
                        <small>{f.detalle}</small>
                    </button>
                ))}
            </div>
            {actual && (
                <p className="detalle-barra" aria-live="polite">
                    <strong>{actual.nombre}</strong>
                    <span>{actual.detalle}</span>
                </p>
            )}
        </div>
    );
}
