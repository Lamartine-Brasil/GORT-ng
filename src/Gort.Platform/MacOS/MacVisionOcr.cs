using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Gort.Core.Model;
using Gort.Core.Ocr;

namespace Gort.Platform.MacOS;

/// <summary>
/// C20 / RF-121 — "Motor do sistema operacional": não usa rede, devolve posição por palavra,
/// e depende dos pacotes de idioma instalados no sistema.
///
/// No macOS é o reconhecimento de texto do Vision. RF-575 — ele só aparece na lista quando
/// está efetivamente disponível; o programa nunca apresenta um motor que falhará ao ser
/// usado.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacVisionOcr : IOcrEngine
{
    private const string CoreGraphicsLib =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    /// <summary>Nível de reconhecimento: 0 = preciso, 1 = rápido.</summary>
    private const long RecognitionLevelAccurate = 0;

    private readonly nint _requestClass;
    private readonly nint _handlerClass;

    public MacVisionOcr()
    {
        // As classes do Objective-C só existem depois que o framework que as define é
        // carregado no processo. Perguntar por elas antes disso devolve nulo, e o motor se
        // declararia indisponível numa máquina em que ele funciona perfeitamente.
        LoadVisionFramework();

        _requestClass = ObjC.objc_getClass("VNRecognizeTextRequest");
        _handlerClass = ObjC.objc_getClass("VNImageRequestHandler");

        if (_requestClass == nint.Zero || _handlerClass == nint.Zero)
        {
            UnavailableReason =
                "O reconhecimento de texto do sistema não está disponível nesta versão do macOS.";
            return;
        }

        Languages = QuerySupportedLanguages();

        if (Languages.Count == 0)
        {
            // RF-137 — sem idioma útil instalado, o motor é marcado indisponível com a
            // mensagem guardada, em vez de reconhecer nada silenciosamente.
            UnavailableReason =
                "O reconhecimento de texto do sistema não tem nenhum idioma útil instalado.";
        }
    }

    public string Key => "system";

    public bool IsAvailable => UnavailableReason is null;

    public string? UnavailableReason { get; }

    /// <summary>
    /// RF-121 — Este motor devolve posição por palavra, então a sobreposição é permitida
    /// com ele (RF-351).
    /// </summary>
    public bool ProvidesWordPositions => true;

    /// <summary>
    /// RF-136 / RF-151 — Os idiomas que o sistema reconhece, intersectados com a tabela do
    /// programa pelo catálogo.
    /// </summary>
    public IReadOnlyList<string> Languages { get; } = Array.Empty<string>();

    /// <summary>Idiomas do sistema, no formato dele, para exibir ao usuário (RF-136).</summary>
    public IReadOnlyList<string> SystemLanguageCodes { get; private set; } = Array.Empty<string>();

    public OcrResult Recognize(ImageBuffer image, string languageCode)
    {
        if (!IsAvailable) return OcrResult.FromError(UnavailableReason!);
        if (image.IsEmpty) return OcrResult.Empty;

        nint cgImage = nint.Zero, handler = nint.Zero, request = nint.Zero, requests = nint.Zero;

        try
        {
            cgImage = CreateCGImage(image);
            if (cgImage == nint.Zero)
                return OcrResult.FromError("Não foi possível preparar a imagem para o sistema.");

            request = ObjC.New("VNRecognizeTextRequest");
            if (request == nint.Zero)
                return OcrResult.FromError("Não foi possível criar o pedido de reconhecimento.");

            ObjC.SendVoidLong(request, ObjC.sel_registerName("setRecognitionLevel:"),
                              RecognitionLevelAccurate);

            SetLanguages(request, languageCode);

            handler = ObjC.Send(
                ObjC.Send(_handlerClass, ObjC.sel_registerName("alloc")),
                ObjC.sel_registerName("initWithCGImage:options:"),
                cgImage,
                ObjC.New("NSDictionary"));

            if (handler == nint.Zero)
                return OcrResult.FromError("Não foi possível iniciar o reconhecimento.");

            requests = ObjC.NSArray(request);

            bool ok = ObjC.SendBoolResult(handler, ObjC.sel_registerName("performRequests:error:"),
                                          requests, nint.Zero);
            if (!ok) return OcrResult.Empty;

            return ReadResults(request, image.Width, image.Height);
        }
        catch (Exception ex)
        {
            // RF-145 — o erro vira resultado vazio com a mensagem; o ciclo continua.
            return OcrResult.FromError(ex.Message);
        }
        finally
        {
            ObjC.Release(handler);
            ObjC.Release(request);
            if (cgImage != nint.Zero) CGImageRelease(cgImage);
        }
    }

    /// <summary>
    /// RF-136 — Os idiomas de reconhecimento disponíveis, perguntados ao sistema e
    /// traduzidos para as chaves da tabela do programa.
    /// </summary>
    private IReadOnlyList<string> QuerySupportedLanguages()
    {
        var keys = new List<string>();
        var codes = new List<string>();

        nint request = nint.Zero;
        try
        {
            request = ObjC.New("VNRecognizeTextRequest");
            if (request == nint.Zero) return keys;

            nint array = ObjC.Send(request,
                ObjC.sel_registerName("supportedRecognitionLanguagesAndReturnError:"));

            nuint count = ObjC.ArrayCount(array);
            for (nuint i = 0; i < count; i++)
            {
                string code = ObjC.ReadString(ObjC.ArrayAt(array, i));
                if (code.Length == 0) continue;

                codes.Add(code);

                // O sistema devolve etiquetas como "en-US" e "ja"; RF-316 trata "en" e
                // "en-US" como equivalentes.
                string key = code.Split('-')[0].ToLowerInvariant();
                string mapped = key switch { "ja" => "ja", "en" => "en", _ => "" };

                if (mapped.Length > 0 && !keys.Contains(mapped)) keys.Add(mapped);
            }
        }
        catch
        {
            // Caso de erro do cap. 14: falha na enumeração marca o motor indisponível.
            return Array.Empty<string>();
        }
        finally
        {
            ObjC.Release(request);
        }

        SystemLanguageCodes = codes;
        return keys;
    }

    private void SetLanguages(nint request, string languageKey)
    {
        // Prefere a etiqueta completa que o sistema declarou para este idioma.
        string code = SystemLanguageCodes
            .FirstOrDefault(c => c.StartsWith(languageKey, StringComparison.OrdinalIgnoreCase))
            ?? languageKey;

        nint value = ObjC.NSString(code);
        if (value == nint.Zero) return;

        nint array = ObjC.NSArray(value);
        if (array != nint.Zero)
        {
            ObjC.SendVoid(request, ObjC.sel_registerName("setRecognitionLanguages:"), array);
        }
    }

    /// <summary>
    /// Converte as observações do sistema no resultado de 6.4.
    ///
    /// As caixas do sistema vêm NORMALIZADAS (0 a 1) e com a origem no canto INFERIOR
    /// esquerdo; o pipeline usa pixels com origem no canto superior esquerdo, então o eixo
    /// vertical é invertido aqui.
    /// </summary>
    private static OcrResult ReadResults(nint request, int width, int height)
    {
        nint results = ObjC.Send(request, ObjC.sel_registerName("results"));
        nuint count = ObjC.ArrayCount(results);

        var lines = new List<(string Text, Rect Box)>((int)count);

        for (nuint i = 0; i < count; i++)
        {
            nint observation = ObjC.ArrayAt(results, i);
            if (observation == nint.Zero) continue;

            nint candidates = ObjC.SendULong(
                observation, ObjC.sel_registerName("topCandidates:"), 1);

            if (ObjC.ArrayCount(candidates) == 0) continue;

            nint candidate = ObjC.ArrayAt(candidates, 0);
            string text = ObjC.ReadString(ObjC.Send(candidate, ObjC.sel_registerName("string")));
            if (text.Length == 0) continue;

            var box = ObjC.SendRect(observation, ObjC.sel_registerName("boundingBox"));

            int x = (int)Math.Floor(box.Origin.X * width);
            int w = (int)Math.Ceiling(box.Size.Width * width);
            int h = (int)Math.Ceiling(box.Size.Height * height);

            // Inversão do eixo vertical.
            int y = (int)Math.Floor((1.0 - box.Origin.Y - box.Size.Height) * height);

            lines.Add((text, new Rect(x, y, Math.Max(1, w), Math.Max(1, h))));
        }

        if (lines.Count == 0) return OcrResult.Empty;

        // RF-141 — este motor devolve LINHAS; cada uma vira uma palavra com a caixa da linha.
        return OcrResultBuilder.FromLines(
            lines.OrderBy(l => l.Box.Top).ThenBy(l => l.Box.Left));
    }

    /// <summary>Cria uma imagem do sistema a partir dos bytes da captura.</summary>
    private static nint CreateCGImage(ImageBuffer image)
    {
        var bgra = ToBgra(image);
        var handle = GCHandle.Alloc(bgra, GCHandleType.Pinned);
        nint space = nint.Zero, context = nint.Zero;

        try
        {
            space = CoreGraphics.CGColorSpaceCreateDeviceRGB();
            if (space == nint.Zero) return nint.Zero;

            context = CoreGraphics.CGBitmapContextCreate(
                handle.AddrOfPinnedObject(),
                (nuint)image.Width, (nuint)image.Height, 8, (nuint)(image.Width * 4),
                space, CoreGraphics.BitmapInfoBgra32);

            return context == nint.Zero ? nint.Zero : CGBitmapContextCreateImage(context);
        }
        finally
        {
            if (context != nint.Zero) CoreGraphics.CGContextRelease(context);
            if (space != nint.Zero) CoreGraphics.CGColorSpaceRelease(space);
            handle.Free();
        }
    }

    /// <summary>RF-117 — Imagens de 1, 3 e 4 canais convertidas para o formato exigido.</summary>
    private static byte[] ToBgra(ImageBuffer image)
    {
        if (image.Format == PixelFormat.Bgra32) return image.Pixels;

        var output = new byte[(long)image.Width * image.Height * 4];
        int o = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var (b, g, r, a) = image.GetPixel(x, y);
                output[o++] = b; output[o++] = g; output[o++] = r; output[o++] = a;
            }
        }
        return output;
    }

    private const int RtldLazy = 1;

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlopen")]
    private static extern nint DlOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

    private static void LoadVisionFramework()
    {
        try
        {
            DlOpen("/System/Library/Frameworks/Vision.framework/Vision", RtldLazy);
        }
        catch
        {
            // Sem o framework, as classes continuam ausentes e o motor se declara
            // indisponível logo abaixo — que é o comportamento de RF-575.
        }
    }

    [DllImport(CoreGraphicsLib)]
    private static extern nint CGBitmapContextCreateImage(nint context);

    [DllImport(CoreGraphicsLib)]
    private static extern void CGImageRelease(nint image);

    public void Dispose() { }
}
