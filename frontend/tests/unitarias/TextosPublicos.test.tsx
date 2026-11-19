import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import PaginaPendientes from "../../src/features/Sincronizacion/PaginaPendientes";
import { ErrorApi, mensajeError } from "../../src/api/cliente";
import { ErrorVisible } from "../../src/components/Interfaz";
import { valorCelda } from "../../src/components/TablaDatos";
import type { OperacionPendiente } from "../../src/offline/almacenamiento";

const datos = vi.hoisted(() => ({ operaciones: [] as OperacionPendiente[] }));
vi.mock("../../src/app/Sesion", () => ({
    useSesion: () => ({ perfil: { idUsuario: 7 } }),
}));
vi.mock("../../src/hooks/usoCola", () => ({
    useCola: () => ({
        operaciones: datos.operaciones,
        pendientes: datos.operaciones.filter((o) => o.estado !== "CONFIRMADA"),
        refrescar: vi.fn(),
        cargando: false,
    }),
}));
afterEach(() => {
    cleanup();
    datos.operaciones = [];
});

const asistencia = (
    estado: OperacionPendiente["estado"],
): OperacionPendiente => ({
    idLocal: "captura-prueba",
    idUsuario: 7,
    coleccion: "asistencias",
    titulo: "Entrada de asistencia",
    datos: {
        idAsignacion: 18,
        uuidRegistro: "f1b81f76-583e-4b0e-bae9-8b36d3665b03",
        fechaJornada: "2026-11-18",
        entradaEn: "2026-11-19T00:08:03.970Z",
        latitudEntrada: 12,
        longitudEntrada: -86,
        precisionGpsEntradaMetros: 8,
        contenidoArchivo: "Ynl0ZXM=",
        esSincronizacionDiferida: true,
        marcaciones: [{ idAsignacion: 18 }],
    },
    creadoEn: "2026-11-19T00:08:03.970Z",
    estado,
    intentos: 3,
    proximoIntento: 0,
    idServidor: "79",
    mensaje: "Confirmado por el servidor",
});

it("muestra una asistencia antigua como confirmada sin exponer sus datos internos", () => {
    datos.operaciones = [asistencia("CONFIRMADA")];
    const original = JSON.stringify(datos.operaciones);
    const { container } = render(<PaginaPendientes />);
    fireEvent.click(screen.getByRole("button", { name: "Confirmadas" }));
    expect(
        screen.getByRole("heading", { name: "Entrada de asistencia" }),
    ).toBeVisible();
    expect(screen.getByText("Confirmada", { exact: true })).toBeVisible();
    expect(container.querySelector("details")).toBeNull();
    expect(container.textContent).not.toMatch(
        /servidor|acuse|uuid|asignación de trabajador|intentos|registro confirmado/i,
    );
    expect(container.textContent).not.toContain("f1b81f76");
    expect(JSON.stringify(datos.operaciones)).toBe(original);
});

it("conserva el motivo de un pendiente y muestra fechas legibles sin volcar el envío", () => {
    datos.operaciones = [
        {
            ...asistencia("ATENCION"),
            mensaje: "La obra no está activa para registrar asistencia.",
        },
    ];
    const { container } = render(<PaginaPendientes />);
    expect(
        screen.getByText("La obra no está activa para registrar asistencia."),
    ).toBeVisible();
    fireEvent.click(screen.getByText("Ver datos de la captura"));
    expect(screen.getByText("Entrada", { exact: true })).toBeInTheDocument();
    expect(container.textContent).toMatch(/06:08/);
    expect(container.textContent).not.toMatch(
        /uuid|idAsignacion|latitud|longitud|precisionGps|marcaciones|Ynl0ZXM|2026-11-19T/,
    );
    expect(
        screen.getByRole("button", { name: "Reintentar envío" }),
    ).toBeVisible();
});

it("traduce fallos y validaciones técnicos sin mostrar trazas ni rutas privadas", () => {
    const error = new ErrorApi(503, {
        detail: "Base de datos PostgreSQL no disponible en https://interno.example/api/obras.",
        identificador: "traza-privada",
        errors: {
            entradaEn: ["Indique una fecha y hora válida en UTC con sufijo Z."],
            idAsignacion: ["El identificador debe ser mayor que cero."],
        },
    });
    const { container } = render(<ErrorVisible error={error} />);
    expect(
        screen.getByText("Revisa la fecha y la hora indicadas."),
    ).toBeVisible();
    expect(screen.getByText("Selecciona una opción válida.")).toBeVisible();
    expect(container.textContent).not.toMatch(
        /PostgreSQL|servidor|traza-privada|UTC|https?:|\/api\//i,
    );
    expect(error.problema.identificador).toBe("traza-privada");
});

it.each([
    [
        400,
        "Obtenga un token de /api/sesion/antifalsificacion en la cabecera X-CET-CSRF.",
    ],
    [409, "El UUID ya está asociado a una operación con datos diferentes."],
    [429, "Espere el tiempo indicado en Retry-After y conserve el UUID."],
    [500, "System.InvalidOperationException: fallo del servidor."],
])("presenta un error %s con indicaciones comprensibles", (estado, detail) => {
    const mensaje = mensajeError(new ErrorApi(estado, { detail }));
    expect(mensaje).not.toMatch(
        /\bAPI\b|servidor|UUID|token|CSRF|System\.|Retry-After|\(\d{3}\)/i,
    );
    expect(mensaje.length).toBeGreaterThan(15);
});

it("mantiene las validaciones del negocio y las etiquetas legibles de auditoría", () => {
    const detalle =
        "La precisión GPS no permite asegurar que estás dentro del radio. Obtén otra lectura.";
    expect(mensajeError(new ErrorApi(400, { detail: detalle }))).toBe(detalle);
    expect(
        valorCelda("entidad", { entidad: "asignaciones_trabajador_obra" }),
    ).toBe("Asignaciones a obra");
    expect(valorCelda("campo", { campo: "id_herramienta" })).toBe(
        "Herramienta",
    );
    expect(
        valorCelda("valorNuevo", { campo: "id_herramienta", valorNuevo: "42" }),
    ).toBe("Referencia del registro");
});
