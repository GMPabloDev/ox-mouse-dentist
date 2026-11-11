using System.Buffers.Binary;

namespace MouseDent.Api.Locales;

/// <summary>
/// Reglas de la foto en el borde: tipo declarado admitido, firma binaria
/// correspondiente y hasta 5 MB. No procesa la imagen, solo la acepta o la
/// rechaza antes de que llegue al almacenamiento.
/// </summary>
public static class LocalPhotoRules
{
    public const long MaximumBytes = 5L * 1024 * 1024;

    public static readonly IReadOnlyList<string> ContentTypes = ["image/jpeg", "image/png", "image/webp"];

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Devuelve el error de validación de la foto, o <c>null</c> si es aceptable.</summary>
    public static async Task<string?> CheckAsync(IFormFile photo, CancellationToken cancellationToken)
    {
        if (photo.Length <= 0)
        {
            return "La foto está vacía.";
        }

        if (photo.Length > MaximumBytes)
        {
            return "La foto no puede superar 5 MB.";
        }

        var contentType = Normalize(photo.ContentType);
        if (!ContentTypes.Contains(contentType))
        {
            return "Solo se aceptan imágenes JPG, PNG o WebP.";
        }

        await using var stream = photo.OpenReadStream();
        return await MatchesStructureAsync(contentType, stream, photo.Length, cancellationToken)
            ? null
            : "El archivo no es una imagen JPG, PNG o WebP válida.";
    }

    public static string Normalize(string? contentType) =>
        (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();

    private static async Task<bool> MatchesStructureAsync(
        string contentType, Stream stream, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[12];
        if (contentType == "image/jpeg")
        {
            if (length < 5) return false;
            await stream.ReadExactlyAsync(buffer.AsMemory(0, 3), cancellationToken);
            if (buffer[0] != 0xFF || buffer[1] != 0xD8 || buffer[2] != 0xFF) return false;
            stream.Seek(-2, SeekOrigin.End);
            await stream.ReadExactlyAsync(buffer.AsMemory(0, 2), cancellationToken);
            return buffer[0] == 0xFF && buffer[1] == 0xD9;
        }

        if (contentType == "image/png")
        {
            if (length < 20) return false;
            await stream.ReadExactlyAsync(buffer.AsMemory(0, 8), cancellationToken);
            if (!buffer.AsSpan(0, 8).SequenceEqual(PngSignature)) return false;
            stream.Seek(-12, SeekOrigin.End);
            await stream.ReadExactlyAsync(buffer, cancellationToken);
            return buffer.AsSpan().SequenceEqual(new byte[] { 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
        }

        if (contentType != "image/webp" || length < 20) return false;
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        if (!buffer.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4, 4)) != length - 8
            || !buffer.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return false;

        var found = false;
        while (stream.Position < length)
        {
            if (length - stream.Position < 8) return false;
            await stream.ReadExactlyAsync(buffer.AsMemory(0, 8), cancellationToken);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4, 4));
            var paddedLength = (long)chunkLength + (chunkLength & 1);
            if (paddedLength > length - stream.Position) return false;
            found |= buffer.AsSpan(0, 4).SequenceEqual("VP8 "u8)
                || buffer.AsSpan(0, 4).SequenceEqual("VP8L"u8)
                || buffer.AsSpan(0, 4).SequenceEqual("VP8X"u8);
            stream.Seek(paddedLength, SeekOrigin.Current);
        }
        return found;
    }
}
