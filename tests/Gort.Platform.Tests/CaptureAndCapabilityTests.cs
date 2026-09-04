using System.Buffers.Binary;
using System.IO.Compression;
using Gort.Core.Model;
using Gort.Platform.Capabilities;
using Gort.Platform.Capture;
using Gort.Platform.Diagnostics;
using Gort.Platform.Monitors;
using Xunit;

namespace Gort.Platform.Tests;

/// <summary>Backend de teste: registra o que lhe pedem e devolve o que mandarem.</summary>
internal sealed class FakeBackend : ICaptureBackend
{
    public List<Rect> Requested { get; } = new();
    public Func<int, Rect, CapturedRegion?>? Behaviour { get; set; }
    public List<nint> Excluded { get; } = new();

    public bool Supports(CaptureSource source) => true;

    public CapturedRegion? Capture(int index, Rect rect, CaptureSource source)
    {
        Requested.Add(rect);
        if (Behaviour is not null) return Behaviour(index, rect);
        return new CapturedRegion
        {
            Index = index,
            Image = ImageBuffer.Allocate(rect.Width, rect.Height, PixelFormat.Bgra32),
            ScreenRect = rect,
        };
    }

    public void ExcludeOwnWindow(nint windowHandle) => Excluded.Add(windowHandle);
    public void Dispose() { }
}

internal sealed class FixedMonitors : IMonitorProvider
{
    public FixedMonitors(params MonitorInfo[] monitors) => Monitors = monitors;
    public IReadOnlyList<MonitorInfo> Monitors { get; }
    public void Refresh() { }
}

public class ScreenCaptureTests
{
    private static readonly FixedMonitors UmMonitor =
        new(new MonitorInfo(new Rect(0, 0, 1920, 1080), 1.0, true, "principal"));

    /// <summary>
    /// PARTE VIII, "Região fora da tela": a captura não produz imagem, o índice é pulado e o
    /// ciclo continua com as demais regiões.
    ///
    /// A verificação é do programa e não do sistema: alguns sistemas devolvem uma imagem
    /// vazia em vez de recusar, e uma imagem vazia entraria no OCR como texto em branco.
    /// </summary>
    [Fact]
    public void Regiao_fora_da_tela_tem_o_indice_pulado_e_nem_chega_ao_sistema()
    {
        var backend = new FakeBackend();
        var capture = new ScreenCapture(backend, UmMonitor);

        var regioes = capture.Capture(new CaptureRequest
        {
            Rects = new[]
            {
                new Rect(100, 100, 200, 50),      // 0 — dentro
                new Rect(5000, 5000, 200, 50),    // 1 — fora
                new Rect(300, 300, 200, 50),      // 2 — dentro
            },
        });

        Assert.Equal(new[] { 0, 2 }, regioes.Select(r => r.Index));
        Assert.Equal(2, backend.Requested.Count);   // o sistema nem foi consultado para o 1
    }

    [Fact]
    public void Uma_regiao_parcialmente_visivel_ainda_e_capturada()
    {
        var backend = new FakeBackend();
        var capture = new ScreenCapture(backend, UmMonitor);

        var regioes = capture.Capture(new CaptureRequest
        {
            Rects = new[] { new Rect(1800, 100, 400, 50) },
        });

        Assert.Single(regioes);
    }

    [Fact]
    public void Sem_provedor_de_monitores_nada_e_descartado_por_falta_de_informacao()
    {
        var backend = new FakeBackend();
        var capture = new ScreenCapture(backend);   // P7 — erra em segurança

        Assert.Single(capture.Capture(new CaptureRequest
        {
            Rects = new[] { new Rect(99999, 99999, 10, 10) },
        }));
    }

    [Fact]
    public void A_verificacao_de_area_de_trabalho_so_vale_para_a_captura_de_tela()
    {
        // Nas outras fontes as coordenadas são relativas à janela, não à área de trabalho.
        var backend = new FakeBackend();
        var capture = new ScreenCapture(backend, UmMonitor);

        Assert.Single(capture.Capture(new CaptureRequest
        {
            Rects = new[] { new Rect(5000, 5000, 100, 50) },
            Source = CaptureSource.ActiveWindow,
        }));
    }

    /// <summary>
    /// Caso de erro do capítulo 11: área de largura ou altura 0 após ajustes é forçada a
    /// 1 px, em vez de descartada.
    /// </summary>
    [Fact]
    public void Area_degenerada_e_forcada_a_um_pixel()
    {
        var backend = new FakeBackend();
        new ScreenCapture(backend, UmMonitor).Capture(new CaptureRequest
        {
            Rects = new[] { new Rect(10, 10, 0, 0) },
        });

        Assert.Equal(new Rect(10, 10, 1, 1), backend.Requested[0]);
    }

    /// <summary>
    /// 6.2 — "se um retângulo não produz imagem, aquele índice é simplesmente ausente da
    /// lista devolvida — não é um erro."
    /// </summary>
    [Fact]
    public void Um_retangulo_sem_imagem_e_ausente_do_resultado_sem_erro()
    {
        var backend = new FakeBackend { Behaviour = (i, _) => i == 1 ? null : Ok(i) };
        var regioes = new ScreenCapture(backend, UmMonitor).Capture(new CaptureRequest
        {
            Rects = new[] { new Rect(0, 0, 10, 10), new Rect(20, 0, 10, 10), new Rect(40, 0, 10, 10) },
        });

        Assert.Equal(new[] { 0, 2 }, regioes.Select(r => r.Index));
    }

    /// <summary>
    /// RF-561 / P8 — Nenhuma falha de captura pode encerrar o laço: ela degrada para
    /// "índice pulado".
    /// </summary>
    [Fact]
    public void Uma_falha_do_sistema_degrada_para_indice_pulado()
    {
        var backend = new FakeBackend
        {
            Behaviour = (i, _) => i == 0 ? throw new InvalidOperationException("falha nativa") : Ok(i),
        };

        var regioes = new ScreenCapture(backend, UmMonitor).Capture(new CaptureRequest
        {
            Rects = new[] { new Rect(0, 0, 10, 10), new Rect(20, 0, 10, 10) },
        });

        Assert.Equal(new[] { 1 }, regioes.Select(r => r.Index));
    }

    [Fact]
    public void C1_a_janela_do_proprio_programa_e_repassada_ao_sistema_para_exclusao()
    {
        var backend = new FakeBackend();
        new ScreenCapture(backend, UmMonitor).ExcludeOwnWindow(1234);
        Assert.Equal(new nint[] { 1234 }, backend.Excluded);
    }

    private static CapturedRegion Ok(int index) => new()
    {
        Index = index,
        Image = ImageBuffer.Allocate(10, 10, PixelFormat.Bgra32),
        ScreenRect = new Rect(0, 0, 10, 10),
    };
}

/// <summary>RF-576 — O retrato de capacidades apurado na inicialização.</summary>
public class CapabilityReportTests
{
    [Fact]
    public void RF_576_uma_capacidade_nao_avaliada_conta_como_indisponivel()
    {
        var relatorio = new CapabilityReport(new[]
        {
            CapabilityStatus.Ok(Capability.ScreenRegionCapture),
        });

        Assert.True(relatorio.Has(Capability.ScreenRegionCapture));
        Assert.False(relatorio.Has(Capability.Clipboard));
        Assert.Equal(UnavailabilityKind.NotSupported, relatorio[Capability.Clipboard].Kind);
    }

    /// <summary>
    /// RF-569 — Sem capacidade essencial, nenhuma tradução é possível: o programa deve dizer
    /// isso e NÃO INICIAR.
    /// </summary>
    [Fact]
    public void Sem_captura_de_tela_nao_ha_traducao_possivel()
    {
        var semCaptura = new CapabilityReport(new[]
        {
            CapabilityStatus.Missing(Capability.ScreenRegionCapture,
                UnavailabilityKind.PermissionRequired, "falta permissão"),
            CapabilityStatus.Ok(Capability.MonitorEnumeration),
        });

        Assert.False(semCaptura.CanTranslate);
        Assert.Contains("falta permissão", semCaptura.BlockingExplanation());
    }

    [Fact]
    public void Com_as_capacidades_essenciais_a_traducao_e_possivel()
    {
        var completo = new CapabilityReport(new[]
        {
            CapabilityStatus.Ok(Capability.ScreenRegionCapture),
            CapabilityStatus.Ok(Capability.MonitorEnumeration),
        });
        Assert.True(completo.CanTranslate);
    }

    [Fact]
    public void RF_577_a_abstracao_e_exigida_para_C1_a_C12()
    {
        for (int i = 1; i <= 12; i++)
            Assert.True(CapabilityInfo.RequiresAbstraction((Capability)i));

        Assert.False(CapabilityInfo.RequiresAbstraction(Capability.SystemTextRecognition));
    }

    /// <summary>
    /// RF-569 — Quando falta uma PERMISSÃO, há uma tela de configuração a oferecer; quando a
    /// capacidade simplesmente não existe, não há.
    /// </summary>
    [Fact]
    public void Uma_permissao_ausente_traz_a_tela_de_configuracao_a_oferecer()
    {
        var status = CapabilityStatus.Missing(Capability.ScreenRegionCapture,
            UnavailabilityKind.PermissionRequired, "explicação", "Ajustes › Gravação de Tela");

        Assert.NotNull(status.RemediationHint);
        Assert.Null(CapabilityStatus.Missing(Capability.CompositorSync,
            UnavailabilityKind.NotSupported, "não existe").RemediationHint);
    }

    /// <summary>
    /// RF-575 / RF-576 — A plataforma real desta máquina reporta todas as capacidades, e
    /// nenhuma fica sem explicação quando indisponível.
    /// </summary>
    [Fact]
    public void A_plataforma_atual_explica_toda_capacidade_indisponivel()
    {
        using var plataforma = PlatformServices.Create();

        Assert.Equal(Enum.GetValues<Capability>().Length, plataforma.Capabilities.All.Count());
        Assert.All(plataforma.Capabilities.Unavailable,
            s => Assert.False(string.IsNullOrWhiteSpace(s.Explanation)));
    }
}

/// <summary>O gravador de PNG do teste visual da Etapa 2.</summary>
public class PngWriterTests
{
    [Fact]
    public void Grava_um_png_valido_com_as_dimensoes_e_os_pixels_certos()
    {
        var img = ImageBuffer.Allocate(3, 2, PixelFormat.Bgra32);
        // BGRA: um pixel vermelho opaco no canto.
        img.Pixels[img.OffsetOf(0, 0) + 0] = 0;     // B
        img.Pixels[img.OffsetOf(0, 0) + 1] = 0;     // G
        img.Pixels[img.OffsetOf(0, 0) + 2] = 255;   // R
        img.Pixels[img.OffsetOf(0, 0) + 3] = 255;   // A

        using var ms = new MemoryStream();
        PngWriter.Write(img, ms);
        var bytes = ms.ToArray();

        // Assinatura PNG.
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);

        // IHDR: comprimento 13, tipo, largura, altura, RGBA de 8 bits.
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8)));
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
        Assert.Equal(8, bytes[24]);    // profundidade
        Assert.Equal(6, bytes[25]);    // cor verdadeira com alfa

        // O fluxo IDAT infla de volta para as linhas esperadas, com o byte de filtro 0 e
        // os pixels convertidos de BGRA para RGBA.
        var chunks = Chunks(bytes);
        var zlib = chunks["IDAT"];

        using var deflated = new MemoryStream(zlib, 2, zlib.Length - 6);   // sem cabeçalho nem Adler
        using var inflater = new DeflateStream(deflated, CompressionMode.Decompress);
        using var saida = new MemoryStream();
        inflater.CopyTo(saida);
        var linhas = saida.ToArray();

        Assert.Equal((3 * 4 + 1) * 2, linhas.Length);
        Assert.Equal(0, linhas[0]);                       // filtro: nenhum
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, linhas[1..5]);   // RGBA vermelho

        Assert.True(chunks.ContainsKey("IEND"));
    }

    [Fact]
    public void Grava_em_arquivo_criando_a_pasta()
    {
        string caminho = Path.Combine(Path.GetTempPath(), "gort-png",
                                      Guid.NewGuid().ToString("N"), "img.png");
        PngWriter.Save(ImageBuffer.Allocate(4, 4, PixelFormat.Bgra32), caminho);
        Assert.True(File.Exists(caminho));
        Assert.True(new FileInfo(caminho).Length > 8);
    }

    /// <summary>
    /// Percorre a estrutura do PNG bloco a bloco — [comprimento:4][tipo:4][dados:n][crc:4] —
    /// em vez de procurar os quatro bytes do tipo soltos no arquivo, que casariam por acaso
    /// dentro dos dados comprimidos.
    /// </summary>
    private static Dictionary<string, byte[]> Chunks(byte[] png)
    {
        var chunks = new Dictionary<string, byte[]>();
        int i = 8;   // depois da assinatura
        while (i + 12 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i));
            string type = System.Text.Encoding.ASCII.GetString(png, i + 4, 4);
            chunks[type] = png.AsSpan(i + 8, length).ToArray();
            i += 12 + length;
        }
        return chunks;
    }
}
