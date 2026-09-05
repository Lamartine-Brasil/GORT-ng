using System.Globalization;

namespace Gort.Core.Localization;

/// <summary>
/// Cap. 26 — Localização da interface.
///
/// Fornece todo texto exibido pelo próprio programa no idioma da interface, A PARTIR DE
/// DADOS e não de literais no código.
///
/// RF-489 — A tabela é um arquivo de dados EXTERNO, distribuído junto do programa e editável
/// diretamente, sem recompilar e sem nenhuma etapa de exportação intermediária.
///
/// Não tem efeito sobre a tradução do conteúdo do usuário: são dois sistemas independentes.
/// </summary>
public sealed class Localizer
{
    /// <summary>RF-487 — O idioma inicial da interface é o português do Brasil.</summary>
    public const string InitialLanguage = "pt-BR";

    private readonly Dictionary<string, Dictionary<string, string>> _table = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _languages = new();

    /// <summary>Idiomas de interface disponíveis, na ordem das colunas da tabela.</summary>
    public IReadOnlyList<string> Languages => _languages;

    /// <summary>Idioma ativo.</summary>
    public string Language { get; private set; } = InitialLanguage;

    /// <summary>Quantidade de chaves carregadas.</summary>
    public int Count => _table.Count;

    /// <summary>
    /// RF-485 — Uma chave ausente na tabela resulta no PRÓPRIO NOME DA CHAVE sendo exibido,
    /// para tornar a falta visível.
    ///
    /// É deliberado que não se devolva vazio: um rótulo em branco na interface passaria
    /// despercebido, e o nome da chave salta aos olhos de quem está traduzindo.
    /// </summary>
    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_table.TryGetValue(key, out var byLanguage))
        {
            if (byLanguage.TryGetValue(Language, out var text) && text.Length > 0) return text;

            // O idioma ativo não tem essa coluna preenchida: cai para o inicial antes de
            // desistir, para que uma tradução parcial ainda seja utilizável.
            if (byLanguage.TryGetValue(InitialLanguage, out var fallback) && fallback.Length > 0)
                return fallback;
        }
        return key;
    }

    /// <summary>Texto com marcadores posicionais substituídos.</summary>
    public string Format(string key, params object[] arguments)
    {
        string template = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            // Um marcador malformado na tabela não pode derrubar a interface.
            return template;
        }
    }

    /// <summary>Verdadeiro quando a chave existe na tabela — usado por testes e diagnóstico.</summary>
    public bool Has(string key) => _table.ContainsKey(key);

    /// <summary>
    /// RF-484 — Quando o usuário não escolheu idioma, o programa deriva o idioma do SISTEMA
    /// OPERACIONAL, com queda para o idioma inicial se não houver correspondência na tabela.
    /// </summary>
    public void SelectLanguage(string? chosen, string? systemLanguage = null)
    {
        if (!string.IsNullOrWhiteSpace(chosen) && _languages.Contains(chosen, StringComparer.OrdinalIgnoreCase))
        {
            Language = Normalize(chosen);
            return;
        }

        string system = systemLanguage ?? CultureInfo.CurrentUICulture.Name;

        // Correspondência exata primeiro, depois pela subetiqueta primária: um sistema em
        // "pt-PT" deve encontrar "pt-BR" antes de cair para o inicial.
        var exact = _languages.FirstOrDefault(
            l => string.Equals(l, system, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) { Language = exact; return; }

        string primary = Primary(system);
        var partial = _languages.FirstOrDefault(
            l => string.Equals(Primary(l), primary, StringComparison.OrdinalIgnoreCase));

        Language = partial ?? InitialLanguage;
    }

    /// <summary>
    /// RF-486 — Trocar o idioma da interface exige reinício, e o usuário deve ser avisado
    /// disso NA LÍNGUA NOVA: avisar na antiga seria mostrar a mensagem no idioma que ele
    /// acabou de abandonar.
    /// </summary>
    public string RestartWarningIn(string newLanguage, string key = "ui.restart_required")
    {
        string previous = Language;
        try
        {
            SelectLanguage(newLanguage);
            return Get(key);
        }
        finally
        {
            Language = previous;
        }
    }

    private static string Primary(string code)
    {
        int i = code.IndexOfAny(new[] { '-', '_' });
        return i < 0 ? code : code[..i];
    }

    private string Normalize(string code)
        => _languages.FirstOrDefault(l => string.Equals(l, code, StringComparison.OrdinalIgnoreCase))
           ?? code;

    /// <summary>
    /// RF-482 — Carrega a tabela: uma coluna de CHAVE e uma coluna POR IDIOMA.
    ///
    /// RF-483 — Acrescentar um idioma de interface é acrescentar uma COLUNA de dados, sem
    /// tocar em código.
    /// </summary>
    public static Localizer Load(string path)
    {
        var localizer = new Localizer();

        try
        {
            if (!File.Exists(path)) return localizer;

            var rows = CsvTable.Parse(File.ReadAllText(path));
            if (rows.Count == 0) return localizer;

            // Primeira linha: cabeçalho. A primeira coluna é a chave; as demais, idiomas.
            var header = rows[0];
            for (int c = 1; c < header.Count; c++)
            {
                string language = header[c].Trim();
                if (language.Length > 0) localizer._languages.Add(language);
            }

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0) continue;

                string key = row[0].Trim();
                if (key.Length == 0 || key.StartsWith('#')) continue;

                var byLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c < row.Count && c - 1 < localizer._languages.Count; c++)
                {
                    byLanguage[localizer._languages[c - 1]] = row[c];
                }
                localizer._table[key] = byLanguage;
            }
        }
        catch
        {
            // P8 — sem tabela, cada chave aparece como o próprio nome (RF-485) e a interface
            // continua utilizável.
            return new Localizer();
        }

        return localizer;
    }
}
