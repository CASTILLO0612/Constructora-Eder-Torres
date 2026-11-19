import { expect, it } from "vitest";
import { construirRuta } from "../../src/api/cliente";
import {
    guardarConsulta,
    leerConsulta,
} from "../../src/offline/almacenamiento";
import { buscarConsultaGuardada } from "../../src/offline/busquedaLocal";

it("busca entre los datos preparados sin mezclar usuarios, obras ni campos internos", async () => {
    const ruta = "/api/personal/obras/42/asignaciones";
    const parametros = { soloVigentes: true, pagina: 1, tamanoPagina: 100 };
    const datos = Array.from({ length: 135 }, (_, i) => ({
        idAsignacion: i + 1,
        nombreCompletoTrabajador:
            i === 130 ? "José Ángel Núñez" : "Pedro Pérez",
        uuidRegistro: "referencia-privada",
        idUsuario: 77,
    }));
    await guardarConsulta(
        901,
        construirRuta(ruta, parametros) + "#lista-completa",
        datos,
    );
    await guardarConsulta(
        902,
        construirRuta(ruta, parametros) + "#lista-completa",
        [{ nombreCompletoTrabajador: "José Ajeno" }],
    );
    const base = { soloVigentes: true, pagina: 1, tamanoPagina: 50 };
    const resultado = await buscarConsultaGuardada(901, ruta, {
        ...base,
        buscar: "nunez JOSE",
    });
    expect(resultado?.datos).toHaveLength(1);
    expect(resultado?.datos[0].idAsignacion).toBe(131);
    expect(
        (
            await buscarConsultaGuardada(901, ruta, {
                ...base,
                buscar: "privada",
            })
        )?.datos,
    ).toEqual([]);
    expect(
        (await buscarConsultaGuardada(901, ruta, { ...base, buscar: "ajeno" }))
            ?.datos,
    ).toEqual([]);
    expect(
        await buscarConsultaGuardada(
            901,
            "/api/personal/obras/43/asignaciones",
            { ...base, buscar: "Jose" },
        ),
    ).toBeUndefined();
    expect(
        await leerConsulta(
            901,
            construirRuta(ruta, { ...base, buscar: "nunez JOSE" }),
        ),
    ).toBeUndefined();
    expect(
        (
            await buscarConsultaGuardada(901, ruta, {
                ...base,
                buscar: "Pedro",
                pagina: 3,
            })
        )?.datos,
    ).toHaveLength(34);
});
