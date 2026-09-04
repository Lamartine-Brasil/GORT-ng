using Gort.Ocr.Rapid.Detection;
using Xunit;

namespace Gort.Ocr.Tests;

/// <summary>Geometria do pós-processamento do detector.</summary>
public class Geometry2DTests
{
    [Fact]
    public void O_casco_convexo_descarta_os_pontos_interiores()
    {
        var pontos = new List<PointD>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10),
            new(5, 5),   // interior
            new(3, 7),   // interior
        };

        var casco = Geometry2D.ConvexHull(pontos);
        Assert.Equal(4, casco.Count);
        Assert.DoesNotContain(new PointD(5, 5), casco);
    }

    [Fact]
    public void O_casco_convexo_descarta_pontos_colineares()
    {
        var casco = Geometry2D.ConvexHull(new List<PointD>
        {
            new(0, 0), new(5, 0), new(10, 0), new(10, 10), new(0, 10),
        });
        Assert.Equal(4, casco.Count);
    }

    [Fact]
    public void O_retangulo_de_area_minima_de_um_quadrado_alinhado_e_ele_mesmo()
    {
        var r = Geometry2D.MinAreaRect(new List<PointD>
        {
            new(10, 20), new(110, 20), new(110, 70), new(10, 70),
        });

        Assert.Equal(60, r.Center.X, 6);
        Assert.Equal(45, r.Center.Y, 6);
        Assert.Equal(100, Math.Max(r.Width, r.Height), 6);
        Assert.Equal(50, Math.Min(r.Width, r.Height), 6);
    }

    [Fact]
    public void O_retangulo_de_area_minima_acompanha_a_rotacao()
    {
        // Um retângulo 100 x 50 girado 45 graus.
        double a = Math.PI / 4, c = Math.Cos(a), s = Math.Sin(a);
        var cantos = new[] { (-50.0, -25.0), (50.0, -25.0), (50.0, 25.0), (-50.0, 25.0) }
            .Select(p => new PointD(p.Item1 * c - p.Item2 * s, p.Item1 * s + p.Item2 * c))
            .ToList();

        var r = Geometry2D.MinAreaRect(cantos);
        Assert.Equal(100, Math.Max(r.Width, r.Height), 3);
        Assert.Equal(50, Math.Min(r.Width, r.Height), 3);
        // A caixa girada é bem menor que a caixa alinhada aos eixos, que teria ~106 de lado.
        Assert.True(r.Width * r.Height < 100 * 100);
    }

    [Fact]
    public void Os_quatro_cantos_reconstroem_o_retangulo()
    {
        var r = new RotatedRect(new PointD(50, 50), 80, 40, 0.3);
        var cantos = r.Corners();
        Assert.Equal(4, cantos.Length);

        var reconstruido = Geometry2D.MinAreaRect(cantos);
        Assert.Equal(80, Math.Max(reconstruido.Width, reconstruido.Height), 3);
        Assert.Equal(40, Math.Min(reconstruido.Width, reconstruido.Height), 3);
    }

    /// <summary>
    /// A expansão (unclip) soma 2 × distância a cada dimensão. É a equivalência que dispensa
    /// uma biblioteca de recorte de polígonos: inflar um retângulo e tomar de novo o
    /// retângulo de área mínima dá exatamente isso.
    /// </summary>
    [Fact]
    public void A_expansao_soma_o_dobro_da_distancia_a_cada_dimensao()
    {
        var r = new RotatedRect(new PointD(0, 0), 100, 50, 0);
        var e = r.Expand(5);
        Assert.Equal(110, e.Width);
        Assert.Equal(60, e.Height);
        Assert.Equal(r.Center, e.Center);
    }

    [Fact]
    public void Area_e_perimetro_de_um_retangulo()
    {
        var poligono = new List<PointD> { new(0, 0), new(10, 0), new(10, 5), new(0, 5) };
        Assert.Equal(50, Geometry2D.PolygonArea(poligono), 6);
        Assert.Equal(30, Geometry2D.PolygonPerimeter(poligono), 6);
    }

    [Fact]
    public void A_media_dentro_do_poligono_ignora_o_que_esta_fora()
    {
        // Mapa 10x10: metade esquerda vale 1, metade direita vale 0.
        var mapa = new float[100];
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 5; x++) mapa[y * 10 + x] = 1f;
        }

        var esquerda = new List<PointD> { new(0, 0), new(5, 0), new(5, 10), new(0, 10) };
        var direita = new List<PointD> { new(5, 0), new(10, 0), new(10, 10), new(5, 10) };

        Assert.Equal(1.0, Geometry2D.MeanInsidePolygon(mapa, 10, 10, esquerda), 3);
        Assert.Equal(0.0, Geometry2D.MeanInsidePolygon(mapa, 10, 10, direita), 3);
    }

    [Fact]
    public void Um_poligono_degenerado_nao_lanca()
    {
        var mapa = new float[100];
        Assert.Equal(0, Geometry2D.MeanInsidePolygon(mapa, 10, 10, new List<PointD> { new(0, 0) }));
        Assert.Equal(default, Geometry2D.MinAreaRect(Array.Empty<PointD>()));
    }
}

/// <summary>Pós-processamento do DBNet sobre mapas de probabilidade sintéticos.</summary>
public class DbPostProcessorTests
{
    /// <summary>Mapa com um bloco retangular de alta probabilidade.</summary>
    private static float[] MapWithBlock(int w, int h, int bx, int by, int bw, int bh, float value = 0.9f)
    {
        var map = new float[w * h];
        for (int y = by; y < by + bh; y++)
        {
            for (int x = bx; x < bx + bw; x++)
            {
                if (x >= 0 && y >= 0 && x < w && y < h) map[y * w + x] = value;
            }
        }
        return map;
    }

    [Fact]
    public void Um_bloco_de_alta_probabilidade_vira_uma_caixa()
    {
        var map = MapWithBlock(100, 60, 20, 15, 40, 12);
        var boxes = DbPostProcessor.ExtractBoxes(map, 100, 60, new DbOptions());

        Assert.Single(boxes);
        Assert.True(boxes[0].Score > 0.5);
    }

    [Fact]
    public void A_caixa_sai_maior_que_o_bloco_por_causa_da_expansao()
    {
        var map = MapWithBlock(100, 60, 20, 15, 40, 12);
        var boxes = DbPostProcessor.ExtractBoxes(map, 100, 60, new DbOptions { UseDilation = false });

        var xs = boxes[0].Corners.Select(c => c.X).ToArray();
        var ys = boxes[0].Corners.Select(c => c.Y).ToArray();

        // O modelo prevê o núcleo do texto, não a sua extensão; a expansão devolve as bordas.
        Assert.True(xs.Max() - xs.Min() > 40);
        Assert.True(ys.Max() - ys.Min() > 12);
    }

    [Fact]
    public void Um_mapa_todo_abaixo_do_corte_nao_produz_caixa()
    {
        var map = MapWithBlock(100, 60, 20, 15, 40, 12, value: 0.1f);
        Assert.Empty(DbPostProcessor.ExtractBoxes(map, 100, 60, new DbOptions()));
    }

    [Fact]
    public void Um_mapa_vazio_nao_produz_caixa()
        => Assert.Empty(DbPostProcessor.ExtractBoxes(new float[600], 100, 6, new DbOptions()));

    [Fact]
    public void Blocos_menores_que_o_lado_minimo_sao_descartados()
    {
        var map = MapWithBlock(100, 60, 20, 15, 2, 2);
        Assert.Empty(DbPostProcessor.ExtractBoxes(map, 100, 60,
            new DbOptions { UseDilation = false }));
    }

    [Fact]
    public void Dois_blocos_separados_viram_duas_caixas()
    {
        var map = MapWithBlock(200, 60, 10, 15, 40, 12);
        var segundo = MapWithBlock(200, 60, 120, 15, 40, 12);
        for (int i = 0; i < map.Length; i++) map[i] = Math.Max(map[i], segundo[i]);

        Assert.Equal(2, DbPostProcessor.ExtractBoxes(map, 200, 60, new DbOptions()).Count);
    }

    /// <summary>
    /// A dilatação junta traços vizinhos: dois blocos separados por 1 px viram um só,
    /// que é o que evita uma linha de texto virar várias caixas.
    /// </summary>
    [Fact]
    public void A_dilatacao_junta_blocos_adjacentes()
    {
        var map = MapWithBlock(100, 60, 10, 15, 20, 12);
        var vizinho = MapWithBlock(100, 60, 31, 15, 20, 12);
        for (int i = 0; i < map.Length; i++) map[i] = Math.Max(map[i], vizinho[i]);

        Assert.Equal(2, DbPostProcessor.ExtractBoxes(map, 100, 60,
            new DbOptions { UseDilation = false }).Count);
        Assert.Single(DbPostProcessor.ExtractBoxes(map, 100, 60,
            new DbOptions { UseDilation = true }));
    }

    [Fact]
    public void O_teto_de_candidatos_limita_o_custo_em_imagens_ruidosas()
    {
        // Muitos blocos isolados; o teto corta a varredura.
        var map = new float[400 * 400];
        for (int y = 5; y < 395; y += 10)
        {
            for (int x = 5; x < 395; x += 10)
            {
                for (int dy = 0; dy < 6; dy++)
                {
                    for (int dx = 0; dx < 6; dx++) map[(y + dy) * 400 + x + dx] = 0.9f;
                }
            }
        }

        var boxes = DbPostProcessor.ExtractBoxes(map, 400, 400,
            new DbOptions { MaxCandidates = 10 });
        Assert.True(boxes.Count <= 10);
    }
}
