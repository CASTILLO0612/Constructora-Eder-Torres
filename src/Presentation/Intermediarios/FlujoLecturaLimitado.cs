namespace ConstructoraEderTorres.Presentacion.Intermediarios;

internal sealed class FlujoLecturaLimitado(Stream origen, long maximoBytes) : Stream
{
    private long _leidos;
    public override bool CanRead => origen.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    private int Contabilizar(int cantidad)
    {
        _leidos += cantidad;
        if (_leidos > maximoBytes)
            throw new BadHttpRequestException("La solicitud supera el tamaño máximo permitido.", StatusCodes.Status413PayloadTooLarge);
        return cantidad;
    }

    public override int Read(byte[] buffer, int offset, int count) => Contabilizar(origen.Read(buffer, offset, count));
    public override int Read(Span<byte> buffer) => Contabilizar(origen.Read(buffer));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Contabilizar(await origen.ReadAsync(buffer, cancellationToken));
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Contabilizar(await origen.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
