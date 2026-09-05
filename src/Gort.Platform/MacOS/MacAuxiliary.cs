using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Gort.Platform.Input;

namespace Gort.Platform.MacOS;

/// <summary>Posição do cursor no macOS, pelo evento corrente do sistema.</summary>
[SupportedOSPlatform("macos")]
internal sealed partial class MacCursorPosition : ICursorPosition
{
    private const string CoreGraphicsLib =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [LibraryImport(CoreGraphicsLib)]
    private static partial nint CGEventCreate(nint source);

    [DllImport(CoreGraphicsLib)]
    private static extern CoreGraphics.CGPoint CGEventGetLocation(nint handle);

    [LibraryImport(CoreGraphicsLib)]
    private static partial void CFRelease(nint cf);

    public bool TryGet(out int x, out int y)
    {
        x = 0;
        y = 0;

        nint handle = nint.Zero;
        try
        {
            handle = CGEventCreate(nint.Zero);
            if (handle == nint.Zero) return false;

            var point = CGEventGetLocation(handle);
            x = (int)Math.Round(point.X);
            y = (int)Math.Round(point.Y);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (handle != nint.Zero) CFRelease(handle);
        }
    }
}

/// <summary>
/// C15 no macOS, pelo sintetizador de voz do sistema.
///
/// RF-476 — Lê o resultado de cada ciclo em áudio, para quem prefere ouvir a tradução em vez
/// de desviar o olhar do jogo.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed partial class MacTextToSpeech : ITextToSpeech
{
    private const int RtldLazy = 1;

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dlopen")]
    private static extern nint DlOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

    private nint _synthesizer;

    public MacTextToSpeech()
    {
        try
        {
            // As classes do Objective-C só existem depois que o framework é carregado.
            DlOpen("/System/Library/Frameworks/AVFoundation.framework/AVFoundation", RtldLazy);

            _synthesizer = ObjC.New("AVSpeechSynthesizer");

            if (_synthesizer == nint.Zero)
            {
                UnavailableReason = "O sintetizador de voz do sistema não está disponível.";
            }
        }
        catch (Exception ex)
        {
            UnavailableReason = $"Falha ao iniciar o sintetizador de voz: {ex.Message}";
        }
    }

    public bool IsAvailable => _synthesizer != nint.Zero;

    public string? UnavailableReason { get; }

    /// <summary>RF-477 — Se ainda há fala tocando.</summary>
    public bool IsSpeaking
    {
        get
        {
            if (!IsAvailable) return false;
            try
            {
                return ObjC.Send(_synthesizer, ObjC.sel_registerName("isSpeaking")) != nint.Zero;
            }
            catch
            {
                return false;
            }
        }
    }

    public void Speak(string text, bool interrupt)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(text)) return;

        try
        {
            if (interrupt) Stop();

            nint value = ObjC.NSString(text);
            if (value == nint.Zero) return;

            nint utteranceClass = ObjC.objc_getClass("AVSpeechUtterance");
            if (utteranceClass == nint.Zero) return;

            nint utterance = ObjC.Send(utteranceClass,
                ObjC.sel_registerName("speechUtteranceWithString:"), value);
            if (utterance == nint.Zero) return;

            ObjC.SendVoid(_synthesizer, ObjC.sel_registerName("speakUtterance:"), utterance);
        }
        catch
        {
            // RF-480 — a opção é inerte diante de falha; nunca gera erro.
        }
    }

    /// <summary>Interrompe imediatamente (limite 0 = imediato).</summary>
    public void Stop()
    {
        if (!IsAvailable) return;

        try
        {
            ObjC.SendVoidLong(_synthesizer,
                ObjC.sel_registerName("stopSpeakingAtBoundary:"), 0);
        }
        catch
        {
            // P8
        }
    }

    public void Dispose()
    {
        Stop();
        ObjC.Release(_synthesizer);
        _synthesizer = nint.Zero;
    }
}
