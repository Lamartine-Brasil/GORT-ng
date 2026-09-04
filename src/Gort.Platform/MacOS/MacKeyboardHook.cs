using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Gort.Platform.Input;

namespace Gort.Platform.MacOS;

/// <summary>Ligação com os serviços de acessibilidade e de eventos do macOS.</summary>
[SupportedOSPlatform("macos")]
internal static partial class MacInput
{
    private const string AppServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>
    /// RF-569 — Verifica a permissão de Acessibilidade SEM solicitá-la, para que a ausência
    /// seja detectada na inicialização (RF-576) e não no meio de uma tradução.
    /// </summary>
    [LibraryImport(AppServices)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AXIsProcessTrusted();

    // ── Interceptação de eventos ─────────────────────────────────────────────

    internal const uint TapSession = 1;          // kCGSessionEventTap
    internal const uint HeadInsert = 0;          // kCGHeadInsertEventTap

    /// <summary>
    /// kCGEventTapOptionListenOnly — o gancho OBSERVA sem consumir, que é o que C10 exige:
    /// o jogo continua recebendo tudo o que o usuário digita.
    /// </summary>
    internal const uint ListenOnly = 1;

    internal const uint EventKeyDown = 10;
    internal const uint EventKeyUp = 11;
    internal const uint EventFlagsChanged = 12;

    internal const int FieldKeycode = 9;         // kCGKeyboardEventKeycode
    internal const int FieldAutorepeat = 8;      // kCGKeyboardEventAutorepeat

    internal const ulong FlagShift = 0x00020000;
    internal const ulong FlagControl = 0x00040000;
    internal const ulong FlagAlternate = 0x00080000;
    internal const ulong FlagCommand = 0x00100000;

    internal delegate nint EventTapCallback(nint proxy, uint type, nint handle, nint userInfo);

    [DllImport(AppServices)]
    internal static extern nint CGEventTapCreate(uint tap, uint place, uint options,
                                                  ulong eventsOfInterest,
                                                  EventTapCallback callback, nint userInfo);

    [LibraryImport(AppServices)]
    internal static partial long CGEventGetIntegerValueField(nint handle, int field);

    [LibraryImport(AppServices)]
    internal static partial ulong CGEventGetFlags(nint handle);

    [LibraryImport(AppServices)]
    internal static partial void CGEventTapEnable(nint tap,
        [MarshalAs(UnmanagedType.Bool)] bool enable);

    [LibraryImport(CoreFoundation)]
    internal static partial nint CFMachPortCreateRunLoopSource(nint allocator, nint port, nint order);

    [LibraryImport(CoreFoundation)]
    internal static partial nint CFRunLoopGetCurrent();

    [LibraryImport(CoreFoundation)]
    internal static partial void CFRunLoopAddSource(nint loop, nint source, nint mode);

    [LibraryImport(CoreFoundation)]
    internal static partial void CFRunLoopRun();

    [LibraryImport(CoreFoundation)]
    internal static partial void CFRunLoopStop(nint loop);

    [LibraryImport(CoreFoundation)]
    internal static partial void CFRelease(nint cf);

    [LibraryImport(CoreFoundation, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint CFStringCreateWithCString(nint alloc, string value, uint encoding);
}

/// <summary>
/// C10 no macOS, por interceptação de eventos do sistema.
///
/// RF-436 — Funciona com qualquer janela em primeiro plano. O gancho é instalado em modo
/// APENAS OBSERVAÇÃO: as teclas seguem para o jogo intactas.
///
/// RF-569 — Exige permissão de Acessibilidade. Sem ela, o gancho não é instalado, a
/// capacidade é reportada indisponível na inicialização e o usuário opera pelo controle
/// remoto.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacKeyboardHook : IGlobalKeyboardHook
{
    private readonly MacInput.EventTapCallback _callback;   // mantido vivo contra a coleta
    private Thread? _thread;
    private nint _tap;
    private nint _source;
    private nint _runLoop;
    private volatile bool _running;

    public MacKeyboardHook() => _callback = OnEvent;

    public bool IsActive => _running;
    public string? UnavailableReason { get; private set; }
    public event Action<KeyEvent>? KeyChanged;

    /// <summary>Estado dos modificadores, derivado dos sinalizadores de cada evento.</summary>
    private ulong _flags;

    public bool Start()
    {
        if (_running) return true;

        if (!MacInput.AXIsProcessTrusted())
        {
            UnavailableReason =
                "O macOS exige permissão de Acessibilidade para receber teclas enquanto " +
                "outro programa está em primeiro plano. Sem ela, use o controle remoto.";
            return false;
        }

        var ready = new TaskCompletionSource<bool>();

        // A interceptação exige um laço de execução próprio; ele fica numa thread dedicada
        // para não bloquear a interface.
        _thread = new Thread(() => RunLoop(ready))
        {
            IsBackground = true,
            Name = "gort-gancho-de-teclado",
        };
        _thread.Start();

        return ready.Task.Wait(TimeSpan.FromSeconds(2)) && ready.Task.Result;
    }

    private void RunLoop(TaskCompletionSource<bool> ready)
    {
        try
        {
            ulong mask = (1UL << (int)MacInput.EventKeyDown)
                       | (1UL << (int)MacInput.EventKeyUp)
                       | (1UL << (int)MacInput.EventFlagsChanged);

            _tap = MacInput.CGEventTapCreate(MacInput.TapSession, MacInput.HeadInsert,
                                             MacInput.ListenOnly, mask, _callback, nint.Zero);

            if (_tap == nint.Zero)
            {
                UnavailableReason = "Não foi possível instalar o interceptador de teclado.";
                ready.TrySetResult(false);
                return;
            }

            _source = MacInput.CFMachPortCreateRunLoopSource(nint.Zero, _tap, nint.Zero);
            _runLoop = MacInput.CFRunLoopGetCurrent();

            nint mode = MacInput.CFStringCreateWithCString(nint.Zero, "kCFRunLoopCommonModes", 0x08000100);
            MacInput.CFRunLoopAddSource(_runLoop, _source, mode);
            MacInput.CFRelease(mode);

            MacInput.CGEventTapEnable(_tap, true);

            _running = true;
            ready.TrySetResult(true);

            MacInput.CFRunLoopRun();
        }
        catch (Exception ex)
        {
            UnavailableReason = ex.Message;
            ready.TrySetResult(false);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Tratamento de um evento. Tem de devolver DEPRESSA: o sistema remove um interceptador
    /// que fique preso, e isso mataria todos os atalhos até reiniciar (RF-011).
    /// </summary>
    private nint OnEvent(nint proxy, uint type, nint handle, nint userInfo)
    {
        try
        {
            if (type == MacInput.EventFlagsChanged)
            {
                ulong flags = MacInput.CGEventGetFlags(handle);
                RaiseModifierChanges(_flags, flags);
                _flags = flags;
            }
            else if (type == MacInput.EventKeyDown || type == MacInput.EventKeyUp)
            {
                _flags = MacInput.CGEventGetFlags(handle);

                long code = MacInput.CGEventGetIntegerValueField(handle, MacInput.FieldKeycode);
                string? name = MacKeyCodes.Name((int)code);
                if (name is not null)
                {
                    bool repeat = type == MacInput.EventKeyDown
                        && MacInput.CGEventGetIntegerValueField(handle, MacInput.FieldAutorepeat) != 0;

                    KeyChanged?.Invoke(new KeyEvent(name, type == MacInput.EventKeyDown, repeat));
                }
            }
        }
        catch
        {
            // Uma exceção aqui atravessaria a fronteira nativa; engoli-la é o único
            // comportamento seguro (P8).
        }

        // Modo de observação: o evento segue intacto para o programa em primeiro plano.
        return handle;
    }

    /// <summary>
    /// RF-437 — Os modificadores chegam como sinalizadores agregados, em que as variantes
    /// esquerda e direita JÁ vêm fundidas. É a normalização do requisito, feita pelo próprio
    /// sistema.
    /// </summary>
    private void RaiseModifierChanges(ulong before, ulong after)
    {
        void Check(ulong flag, string key)
        {
            bool was = (before & flag) != 0;
            bool now = (after & flag) != 0;
            if (was != now) KeyChanged?.Invoke(new KeyEvent(key, now));
        }

        Check(MacInput.FlagControl, Gort.Core.Shortcuts.KeyNames.Control);
        Check(MacInput.FlagShift, Gort.Core.Shortcuts.KeyNames.Shift);
        Check(MacInput.FlagAlternate, Gort.Core.Shortcuts.KeyNames.Alt);
        Check(MacInput.FlagCommand, Gort.Core.Shortcuts.KeyNames.Meta);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        try
        {
            if (_tap != nint.Zero) MacInput.CGEventTapEnable(_tap, false);
            if (_runLoop != nint.Zero) MacInput.CFRunLoopStop(_runLoop);
            _thread?.Join(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // P8
        }
        finally
        {
            if (_source != nint.Zero) { MacInput.CFRelease(_source); _source = nint.Zero; }
            if (_tap != nint.Zero) { MacInput.CFRelease(_tap); _tap = nint.Zero; }
            _runLoop = nint.Zero;
        }
    }

    public void Dispose() => Stop();
}

/// <summary>
/// Códigos de tecla virtuais do macOS para os nomes independentes de plataforma.
///
/// Os modificadores NÃO estão aqui: eles chegam pelos sinalizadores do evento, já com as
/// variantes esquerda e direita fundidas (RF-437).
/// </summary>
internal static class MacKeyCodes
{
    private static readonly Dictionary<int, string> Map = new()
    {
        [0] = "A", [11] = "B", [8] = "C", [2] = "D", [14] = "E", [3] = "F", [5] = "G",
        [4] = "H", [34] = "I", [38] = "J", [40] = "K", [37] = "L", [46] = "M", [45] = "N",
        [31] = "O", [35] = "P", [12] = "Q", [15] = "R", [1] = "S", [17] = "T", [32] = "U",
        [9] = "V", [13] = "W", [7] = "X", [16] = "Y", [6] = "Z",

        [29] = "0", [18] = "1", [19] = "2", [20] = "3", [21] = "4",
        [23] = "5", [22] = "6", [26] = "7", [28] = "8", [25] = "9",

        [122] = "F1", [120] = "F2", [99] = "F3", [118] = "F4", [96] = "F5", [97] = "F6",
        [98] = "F7", [100] = "F8", [101] = "F9", [109] = "F10", [103] = "F11", [111] = "F12",

        [36] = "Enter", [48] = "Tab", [49] = "Space", [51] = "Backspace", [53] = "Escape",
        [123] = "Left", [124] = "Right", [125] = "Down", [126] = "Up",
        [117] = "Delete", [115] = "Home", [119] = "End",
        [116] = "PageUp", [121] = "PageDown",
    };

    public static string? Name(int keyCode) => Map.TryGetValue(keyCode, out var n) ? n : null;
}
