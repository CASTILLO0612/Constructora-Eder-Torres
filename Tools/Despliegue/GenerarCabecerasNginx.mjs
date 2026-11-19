import { writeFile } from 'node:fs/promises';
import { cabecerasSeguridad } from '../../frontend/scripts/CabecerasSeguridad.mjs';

const destino = process.argv[2];
if (!destino) throw new Error('Indica la ruta de salida del fragmento Nginx.');
const lineas = Object.entries(cabecerasSeguridad).map(([nombre, valor]) => {
    if (!/^[a-z-]+$/.test(nombre) || /[\r\n$]/.test(valor))
        throw new Error('La cabecera no es compatible con el formato Nginx.');
    return `add_header ${nombre} "${valor.replaceAll('\\', '\\\\').replaceAll('"', '\\"')}" always;`;
});
await writeFile(destino, '# Generado desde frontend/scripts/CabecerasSeguridad.mjs\n' + lineas.join('\n') + '\n');
