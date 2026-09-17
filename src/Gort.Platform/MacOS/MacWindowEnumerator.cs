using System.Runtime.Versioning;
using Gort.Core.Model;
using Gort.Platform.Capture;

namespace Gort.Platform.MacOS;

/// <summary>
/// C3 / RF-089 — O seletor de janelas no macOS, via <c>CGWindowListCopyWindowInfo</c>.
///
/// RF-569 — a lista existe sem permissão nenhuma, mas os TÍTULOS só aparecem com permissão
/// de gravação de tela. Uma lista de janelas sem nome não serve para escolher, então a
/// ausência de títulos é tratada como indisponibilidade explicada, e não como uma lista
/// vazia sem motivo.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacWindowEnumerator : IWindowEnumerator
{
    /// <summary>
    /// Janelas acima desta camada são do sistema — menus, Dock, barra de status. O usuário
    /// não quer traduzir nenhuma delas, e listá-las enterraria as que ele quer.
    /// </summary>
    private const int NormalWindowLayer = 0;

    private readonly int _ownProcessId = Environment.ProcessId;

    public bool IsAvailable => true;

    public string? UnavailableReason => null;

    public IReadOnlyList<CapturableWindow> List()
    {
        var windows = new List<CapturableWindow>();

        // `ListOptionOnScreenOnly` — só as janelas visíveis. Uma janela minimizada não pode
        // ser capturada, e oferecê-la seria oferecer uma escolha que falha depois.
        nint array = CoreGraphics.CGWindowListCopyWindowInfo(
            CoreGraphics.ListOptionOnScreenOnly | CoreGraphics.ListExcludeDesktopElements,
            CoreGraphics.NullWindowID);

        if (array == nint.Zero) return windows;

        try
        {
            nuint count = ObjC.ArrayCount(array);
            for (nuint i = 0; i < count; i++)
            {
                var window = Read(ObjC.ArrayAt(array, i));
                if (window is not null) windows.Add(window);
            }
        }
        finally
        {
            ObjC.Release(array);
        }

        return windows;
    }

    private CapturableWindow? Read(nint info)
    {
        if (info == nint.Zero) return null;

        // RF-343 — a própria janela do programa nunca entra: escolhê-la faria o programa
        // traduzir a sua própria tradução.
        if (Int(info, CoreGraphics.WindowPidKey) == _ownProcessId) return null;

        // Camadas acima da normal são do sistema.
        if (Int(info, CoreGraphics.WindowLayerKey) != NormalWindowLayer) return null;

        int id = Int(info, CoreGraphics.WindowNumberKey);
        if (id <= 0) return null;

        string application = String(info, CoreGraphics.WindowOwnerNameKey);
        string title = String(info, CoreGraphics.WindowNameKey);

        // Sem nome de aplicativo não há como o usuário reconhecer a janela na lista.
        if (application.Length == 0) return null;

        var bounds = Bounds(info);

        // Uma janela sem área não produz imagem; oferecê-la seria oferecer uma escolha que
        // já se sabe que falha.
        if (bounds.IsEmpty) return null;

        return new CapturableWindow((ulong)id, title, application, bounds);
    }

    private static Rect Bounds(nint info)
    {
        nint dict = ObjC.DictionaryValue(info, CoreGraphics.WindowBoundsKey);
        if (dict == nint.Zero) return Rect.Empty;

        double x = Double(dict, "X");
        double y = Double(dict, "Y");
        double width = Double(dict, "Width");
        double height = Double(dict, "Height");

        return new Rect((int)Math.Round(x), (int)Math.Round(y),
                        (int)Math.Round(width), (int)Math.Round(height));
    }

    private static int Int(nint dictionary, string key)
    {
        nint value = ObjC.DictionaryValue(dictionary, key);
        return value == nint.Zero
            ? 0
            : ObjC.SendInt(value, ObjC.sel_registerName("intValue"));
    }

    private static double Double(nint dictionary, string key)
    {
        nint value = ObjC.DictionaryValue(dictionary, key);
        return value == nint.Zero
            ? 0
            : ObjC.SendDouble(value, ObjC.sel_registerName("doubleValue"));
    }

    private static string String(nint dictionary, string key)
    {
        nint value = ObjC.DictionaryValue(dictionary, key);
        return value == nint.Zero ? "" : ObjC.ReadString(value);
    }

    /// <summary>
    /// RF-090 / RF-097 — Se a janela ainda existe.
    ///
    /// A pergunta é feita pedindo a informação DAQUELA janela: uma lista vazia significa que
    /// ela não está mais na tela. Comparar com uma lista completa custaria enumerar tudo a
    /// cada ciclo.
    /// </summary>
    public bool Exists(ulong id)
    {
        if (id == 0 || id > uint.MaxValue) return false;

        nint array = CoreGraphics.CGWindowListCopyWindowInfo(
            CoreGraphics.ListOptionIncludingWindow, (uint)id);

        if (array == nint.Zero) return false;

        try
        {
            return ObjC.ArrayCount(array) > 0;
        }
        finally
        {
            ObjC.Release(array);
        }
    }
}
