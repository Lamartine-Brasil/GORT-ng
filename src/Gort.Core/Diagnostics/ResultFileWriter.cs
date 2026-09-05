using Gort.Core.Caching;

namespace Gort.Core.Diagnostics;

/// <summary>
/// RF-496 — Gravação do resultado em arquivo de texto a cada ciclo, no FORMATO DO BANCO DE
/// DADOS (`/s`, reconhecido, `/t`, traduzido, `/e`).
///
/// O formato não é coincidência: é o mesmo do banco de dados de tradução, para que o usuário
/// construa bancos a partir do uso real e depois os carregue como fonte local — o que torna
/// aquelas traduções instantâneas e offline.
/// </summary>
public sealed class ResultFileWriter
{
    private readonly object _gate = new();
    private string _lastRecognized = "";

    public ResultFileWriter(string path) => Path = path;

    public string Path { get; }

    /// <summary>RF-496 — A gravação está ligada.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Grava o par do ciclo. Devolve falso quando nada foi gravado.
    ///
    /// Um par com texto reconhecido vazio não é gravado: ele não serve como entrada de banco
    /// de dados. O mesmo par duas vezes seguidas também não — a detecção de mudança já
    /// evita a maior parte disso, mas um ciclo pontual repetido passaria.
    /// </summary>
    public bool Write(string recognized, string translated)
    {
        if (!Enabled) return false;
        if (string.IsNullOrWhiteSpace(recognized)) return false;

        lock (_gate)
        {
            string key = recognized.TrimEnd();
            if (key == _lastRecognized) return false;

            try
            {
                PairFile.Append(Path, new[] { new TranslationPair(key, translated ?? "") });
                _lastRecognized = key;
                return true;
            }
            catch
            {
                // P8 — uma falha de disco não pode interromper o laço.
                return false;
            }
        }
    }

    public void Reset()
    {
        lock (_gate) _lastRecognized = "";
    }
}
