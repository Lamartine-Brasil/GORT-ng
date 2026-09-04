using System.Buffers.Binary;
using System.IO.Compression;
using Gort.Core.Model;

namespace Gort.Platform.Diagnostics;

/// <summary>
/// Gravador de PNG mínimo, usado pelo teste visual da Etapa 2 e pelo retrato de análise
/// (cap. 27).
///
/// É deliberadamente próprio e sem dependências: a única biblioteca de imagem de que o
/// programa precisa é a que desenha texto (C16/C17), e essa tem de ser a mesma em todas as
/// plataformas (RF-572). Trazer um decodificador de imagem completo só para gravar um
/// diagnóstico seria peso morto.
/// </summary>
public static class PngWriter
{
    private static ReadOnlySpan<byte> Signature => new byte[]
        { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static void Save(ImageBuffer image, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var file = File.Create(path);
        Write(image, file);
    }

    public static void Write(ImageBuffer image, Stream output)
    {
        output.Write(Signature);

        // IHDR: 8 bits por componente, cor verdadeira com alfa, sem entrelaçamento.
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), image.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), image.Height);
        ihdr[8] = 8;    // profundidade
        ihdr[9] = 6;    // RGBA
        ihdr[10] = 0;   // compressão
        ihdr[11] = 0;   // filtro
        ihdr[12] = 0;   // entrelaçamento
        WriteChunk(output, "IHDR", ihdr);

        WriteChunk(output, "IDAT", Compress(BuildScanlines(image)));
        WriteChunk(output, "IEND", Array.Empty<byte>());
    }

    /// <summary>
    /// Monta as linhas com o byte de filtro 0 (nenhum) e converte de BGRA — a ordem em que
    /// a captura entrega os pixels — para o RGBA que o PNG exige.
    /// </summary>
    private static byte[] BuildScanlines(ImageBuffer image)
    {
        int rowBytes = image.Width * 4;
        var data = new byte[(rowBytes + 1) * (long)image.Height];

        int o = 0;
        for (int y = 0; y < image.Height; y++)
        {
            data[o++] = 0;   // filtro: nenhum
            for (int x = 0; x < image.Width; x++)
            {
                var (b, g, r, a) = image.GetPixel(x, y);
                data[o++] = r;
                data[o++] = g;
                data[o++] = b;
                data[o++] = a;
            }
        }
        return data;
    }

    /// <summary>Fluxo zlib: cabeçalho, dados desinflados e Adler-32 do conteúdo original.</summary>
    private static byte[] Compress(byte[] raw)
    {
        using var buffer = new MemoryStream();
        buffer.WriteByte(0x78);   // CMF: deflate, janela de 32 KiB
        buffer.WriteByte(0x9C);   // FLG: compressão padrão

        using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw, 0, raw.Length);
        }

        Span<byte> adler = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, Adler32(raw));
        buffer.Write(adler);

        return buffer.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        var typeBytes = new byte[4];
        for (int i = 0; i < 4; i++) typeBytes[i] = (byte)type[i];
        output.Write(typeBytes);
        output.Write(data);

        uint crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (byte v in data)
        {
            a = (a + v) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte v in a) c = CrcTable[(c ^ v) & 0xFF] ^ (c >> 8);
        foreach (byte v in b) c = CrcTable[(c ^ v) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
