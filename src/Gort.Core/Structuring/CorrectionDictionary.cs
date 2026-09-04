using System.Text;
using Gort.Core.Calibration;

namespace Gort.Core.Structuring;

/// <summary>
/// RF-181 a RF-185 — Dicionário de correção: lista de substituições texto→texto aplicada
/// ao resultado do OCR ANTES da tradução, para consertar erros recorrentes do motor.
///
/// RF-185 — Formato do arquivo: uma linha "/s", a linha do texto original, a linha do
/// texto corrigido, e uma linha em branco.
/// </summary>
public sealed class CorrectionDictionary
{
    public const string RecordMarker = "/s";

    private readonly List<(string From, string To)> _entries = new();

    /// <summary>
    /// RF-183 — Modo "por palavra": quando ativo, a substituição só ocorre em limites de
    /// palavra; quando inativo, em qualquer posição.
    /// Motivo: idiomas sem separador de palavra precisam do modo inativo — aplicar
    /// substituição em limite de palavra a um idioma sem separador não corrige nada e ainda
    /// mascara erros de OCR reais (RF-044).
    /// </summary>
    public bool WholeWord { get; set; }

    /// <summary>
    /// RF-182 — Passagens ADICIONAIS, de 0 a P-46, para permitir correções encadeadas.
    /// Zero significa uma única passagem.
    /// </summary>
    public int ExtraPasses { get; set; } = P.DictionaryExtraPassesDefault;

    public IReadOnlyList<(string From, string To)> Entries => _entries;
    public int Count => _entries.Count;

    /// <summary>RF-181 — Aplica o dicionário ao texto.</summary>
    public string Apply(string text)
    {
        if (_entries.Count == 0 || string.IsNullOrEmpty(text)) return text;

        int passes = 1 + Math.Clamp(ExtraPasses, P.DictionaryExtraPassesMin, P.DictionaryExtraPassesMax);
        for (int pass = 0; pass < passes; pass++)
        {
            string before = text;
            foreach (var (from, to) in _entries)
            {
                if (from.Length == 0) continue;
                text = WholeWord ? ReplaceWholeWord(text, from, to) : text.Replace(from, to);
            }
            // Correções encadeadas que já estabilizaram não precisam das passagens restantes.
            if (before == text) break;
        }
        return text;
    }

    /// <summary>
    /// RF-183 — Substituição apenas em limites de palavra. Um limite é o início/fim do
    /// texto ou um caractere que não seja letra, dígito ou sublinhado.
    /// </summary>
    private static string ReplaceWholeWord(string text, string from, string to)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            int found = text.IndexOf(from, i, StringComparison.Ordinal);
            if (found < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            bool leftOk = found == 0 || !IsWordChar(text[found - 1]);
            int after = found + from.Length;
            bool rightOk = after >= text.Length || !IsWordChar(text[after]);

            sb.Append(text, i, found - i);
            if (leftOk && rightOk)
            {
                sb.Append(to);
                i = after;
            }
            else
            {
                sb.Append(from);
                i = after;
            }
        }
        return sb.ToString();
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>RF-184 — Acrescenta um par e o mantém disponível imediatamente.</summary>
    public void Add(string from, string to)
    {
        if (string.IsNullOrEmpty(from)) return;
        _entries.Add((from, to));
    }

    public void Clear() => _entries.Clear();

    // ─────────────────────────────────────────────────────────────────────────
    // Persistência — RF-185
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-185 — Lê o arquivo. Um arquivo ausente não é erro: o resultado é um dicionário
    /// vazio, sem correção (caso de erro do capítulo 15).
    /// Linhas inválidas são ignoradas em vez de interromper a leitura (P8).
    /// </summary>
    public static CorrectionDictionary Load(string path)
    {
        var dict = new CorrectionDictionary();
        if (!File.Exists(path)) return dict;

        try
        {
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimEnd() != RecordMarker) continue;
                if (i + 2 >= lines.Length) break;
                dict.Add(lines[i + 1], lines[i + 2]);
                i += 2;
            }
        }
        catch
        {
            // P8 — degradação silenciosa: sem correção, sem erro.
            dict.Clear();
        }
        return dict;
    }

    /// <summary>RF-184 — Acrescenta o par ao arquivo, no formato de RF-185.</summary>
    public static void AppendToFile(string path, string from, string to)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.Append(RecordMarker).Append('\n');
        sb.Append(from).Append('\n');
        sb.Append(to).Append('\n');
        sb.Append('\n');
        File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        foreach (var (from, to) in _entries)
        {
            sb.Append(RecordMarker).Append('\n');
            sb.Append(from).Append('\n');
            sb.Append(to).Append('\n');
            sb.Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
