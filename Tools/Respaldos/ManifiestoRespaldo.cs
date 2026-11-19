namespace ConstructoraEderTorres.Respaldos;

public sealed record ArchivoEvidenciaRespaldo(string RutaRelativa, string HuellaSha256, long TamanoBytes);
public sealed record ManifiestoRespaldo(int Version, DateTime CreadoUtc, string BaseOrigen,
    string HuellaBaseSha256, long TamanoBaseBytes, IReadOnlyList<string> Migraciones,
    IReadOnlyList<ArchivoEvidenciaRespaldo> Evidencias);
public sealed record ResultadoRespaldo(string Accion, string Carpeta, int Evidencias, int Eliminados = 0);
