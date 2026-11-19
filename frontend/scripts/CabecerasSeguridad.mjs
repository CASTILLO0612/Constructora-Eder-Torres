// Política de la PWA compilada. Google Maps necesita cargar su API, recursos
// estáticos, mosaicos y fuentes desde los dominios oficiales de Maps Platform.
export const cabecerasSeguridad = {
    "x-content-type-options": "nosniff",
    "x-frame-options": "DENY",
    // Maps Platform valida las claves de navegador mediante el origen HTTP
    // referente. Solo se comparte el origen al hacer solicitudes externas.
    "referrer-policy": "strict-origin-when-cross-origin",
    "permissions-policy":
        "geolocation=(self), camera=(self), microphone=(), payment=(), usb=()",
    "content-security-policy": [
        "default-src 'self'",
        "script-src 'self' https://maps.googleapis.com https://maps.gstatic.com",
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
        "img-src 'self' data: blob: https://maps.googleapis.com https://mapsresources-pa.googleapis.com https://maps.gstatic.com https://*.google.com",
        "font-src 'self' https://fonts.gstatic.com",
        "connect-src 'self' https://maps.googleapis.com https://mapsresources-pa.googleapis.com https://maps.gstatic.com https://csi.gstatic.com https://www.google.com https://maps.google.com data: blob:",
        "frame-src 'self' https://www.google.com https://maps.google.com",
        "media-src 'self' blob:",
        "worker-src 'self'",
        "manifest-src 'self'",
        "object-src 'none'",
        "base-uri 'none'",
        "frame-ancestors 'none'",
        "form-action 'self'",
    ].join("; "),
};
