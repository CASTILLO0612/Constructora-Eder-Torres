import "fake-indexeddb/auto";
import "@testing-library/jest-dom/vitest";
import { beforeEach } from "vitest";
import { baseLocal } from "../../src/offline/almacenamiento";
import { olvidarToken } from "../../src/api/cliente";
beforeEach(async () => {
    const base = await baseLocal();
    for (const tabla of [
        "operaciones",
        "resumenes",
        "consultas",
        "perfil",
    ] as const)
        await base.clear(tabla);
    olvidarToken();
    Object.defineProperty(navigator, "onLine", {
        value: true,
        configurable: true,
    });
});
