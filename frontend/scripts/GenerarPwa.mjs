import { readdir, readFile, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import path from "node:path";
import { fileURLToPath } from "node:url";
const raiz = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../dist",
);
async function archivos(carpeta) {
    const entradas = await readdir(carpeta, { withFileTypes: true });
    const listas = await Promise.all(
        entradas.map((e) =>
            e.isDirectory()
                ? archivos(path.join(carpeta, e.name))
                : [path.join(carpeta, e.name)],
        ),
    );
    return listas.flat();
}
const rutas = (await archivos(raiz)).filter(
    (p) =>
        /\.(js|css|html|svg|png|woff2|webmanifest)$/.test(p) &&
        !p.endsWith(`${path.sep}sw.js`),
);
const hash = createHash("sha256");
for (const r of rutas) hash.update(await readFile(r));
const version = hash.digest("hex").slice(0, 16);
const recursos = rutas.map(
    (p) => "/" + path.relative(raiz, p).replaceAll(path.sep, "/"),
);
const script = `const CACHE='cet-aplicacion-${version}';
const RECURSOS=${JSON.stringify(recursos)};
self.addEventListener('install',evento=>{evento.waitUntil(caches.open(CACHE).then(cache=>cache.addAll(RECURSOS)));});
self.addEventListener('message',evento=>{if(evento.data==='ACTIVAR_VERSION')self.skipWaiting();});
self.addEventListener('activate',evento=>{evento.waitUntil((async()=>{const claves=(await caches.keys()).filter(c=>c.startsWith('cet-aplicacion-'));for(const clave of claves.slice(0,-2))if(clave!==CACHE)await caches.delete(clave);await self.clients.claim();})());});
self.addEventListener('fetch',evento=>{
 const peticion=evento.request;const url=new URL(peticion.url);
 // Nunca interceptar ni cachear solicitudes autenticadas, salud o mosaicos externos.
 if(peticion.method!=='GET'||url.origin!==self.location.origin||/^\\/(api|salud|scalar|openapi)(\\/|$)/.test(url.pathname))return;
 if(peticion.mode==='navigate'){evento.respondWith(fetch(peticion).catch(()=>caches.open(CACHE).then(cache=>cache.match('/index.html'))));return;}
 if(RECURSOS.includes(url.pathname))evento.respondWith(caches.open(CACHE).then(cache=>cache.match(peticion)).then(c=>c||fetch(peticion)));
});`;
await writeFile(path.join(raiz, "sw.js"), script);
console.log(
    `PWA ${version}: ${recursos.length} recursos locales; API y mapas excluidos del cache.`,
);
