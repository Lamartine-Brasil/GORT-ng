using Gort.Core.Calibration;
using Gort.Core.Configuration;

namespace Gort.Core.Translation.Keys;

/// <summary>RF-252 — Estado de uma chave.</summary>
public enum KeyState
{
    /// <summary>Utilizável.</summary>
    Normal,
    /// <summary>A última tentativa falhou por autenticação.</summary>
    Error,
    /// <summary>A última tentativa falhou por cota.</summary>
    Limit,
}

/// <summary>RF-250 / RF-253 — Uma credencial do rodízio.</summary>
public sealed class TranslationKey
{
    /// <summary>Identificador visível da chave; é por ele que o usuário a reconhece.</summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// RF-035 — O segredo, em TEXTO PURO. Decisão explícita da especificação: o programa
    /// roda inteiramente na máquina do usuário, e cifrar com uma chave também local não
    /// acrescenta proteção real. Por isso a pasta de credenciais nunca entra no controle de
    /// versão.
    /// </summary>
    public string Secret { get; set; } = "";

    /// <summary>RF-252 / RF-253 — Gratuita ou paga.</summary>
    public bool IsFree { get; set; } = true;

    public KeyState State { get; set; } = KeyState.Normal;

    public TranslationKey Clone() => new()
    {
        Id = Id, Secret = Secret, IsFree = IsFree, State = State,
    };
}

/// <summary>
/// RF-250 a RF-253 — O rodízio de chaves.
///
/// O usuário cadastra até P-55 pares e o programa alterna automaticamente para a próxima
/// quando a atual devolve erro de cota ou de autenticação. Não há tentativa de adivinhar
/// qual chave "está boa": a que falhou é marcada, e a próxima elegível assume.
/// </summary>
public sealed class TranslationKeyStore
{
    private readonly List<TranslationKey> _keys = new();

    public IReadOnlyList<TranslationKey> Keys => _keys;

    /// <summary>RF-250 / P-55 — Teto de chaves no rodízio.</summary>
    public static int Capacity => P.MaxRotatingApiKeys;

    public bool IsFull => _keys.Count >= Capacity;

    public TranslationKey? Find(string id)
        => _keys.FirstOrDefault(k => string.Equals(k.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// RF-253 — Acrescenta uma chave, ou ATUALIZA a de mesmo identificador.
    ///
    /// É o que RF-538 pede da interface: o botão alterna entre "adicionar" e "editar"
    /// conforme o identificador digitado já exista. A decisão é do repositório, e não do
    /// botão — assim o comportamento é o mesmo por qualquer caminho.
    /// </summary>
    public TranslationKey? Set(string id, string secret, bool isFree)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var existing = Find(id);
        if (existing is not null)
        {
            existing.Secret = secret;
            existing.IsFree = isFree;

            // Editar uma chave devolve-a ao rodízio: o usuário acabou de corrigir o que
            // provavelmente causou o erro, e mantê-la marcada a deixaria de fora sem motivo.
            existing.State = KeyState.Normal;
            return existing;
        }

        if (IsFull) return null;

        var key = new TranslationKey { Id = id, Secret = secret, IsFree = isFree };
        _keys.Add(key);
        return key;
    }

    public bool Remove(string id)
    {
        var key = Find(id);
        return key is not null && _keys.Remove(key);
    }

    public void Clear() => _keys.Clear();

    /// <summary>
    /// RF-252 — A ordem de exibição e de uso: as GRATUITAS antes das PAGAS, começando pela
    /// primeira em estado NORMAL.
    ///
    /// A ordem não é estética: é a ordem em que o rodízio consome as chaves, e gastar as
    /// gratuitas primeiro é o que deixa as pagas para quando não há alternativa.
    /// </summary>
    public IReadOnlyList<TranslationKey> Ordered()
        => _keys
            .OrderBy(k => k.IsFree ? 0 : 1)
            .ThenBy(k => k.State == KeyState.Normal ? 0 : 1)
            .ThenBy(k => _keys.IndexOf(k))
            .ToList();

    /// <summary>A chave que o serviço deve usar agora, ou nula se não há nenhuma utilizável.</summary>
    public TranslationKey? Current()
        => Ordered().FirstOrDefault(k => k.State == KeyState.Normal);

    /// <summary>
    /// RF-250 / RF-251 — Marca a chave atual e passa para a próxima.
    ///
    /// Devolve a chave que assumiu, para que o chamador possa anexar ao resultado a nota de
    /// RF-251 dizendo qual passou a ser usada. Devolve nula quando acabaram: o serviço
    /// precisa saber a diferença entre "troquei" e "não há mais".
    /// </summary>
    public TranslationKey? Rotate(TranslationKey failed, KeyState reason)
    {
        failed.State = reason == KeyState.Normal ? KeyState.Error : reason;
        return Current();
    }

    /// <summary>Devolve todas ao rodízio — usado quando a cota mensal vira.</summary>
    public void ResetStates()
    {
        foreach (var key in _keys) key.State = KeyState.Normal;
    }

    // ── Persistência (RF-035) ────────────────────────────────────────────────

    public static TranslationKeyStore Load(string path)
    {
        var store = new TranslationKeyStore();
        try
        {
            if (!File.Exists(path)) return store;

            var toml = TomlStore.Load(path, out bool recovered);
            if (recovered) return store;

            foreach (var table in toml.GetTables("chave"))
            {
                var section = new TomlStore(table);
                store.Set(section.GetString("id", ""),
                          section.GetString("segredo", ""),
                          section.GetBool("gratuita", true));
            }
        }
        catch
        {
            // RF-024 — leitura tolerante: sem chaves é melhor que sem programa.
        }
        return store;
    }

    /// <summary>
    /// O ESTADO não é gravado: ele descreve a última sessão, não a chave. Uma cota estourada
    /// ontem pode ter virado hoje, e abrir o programa com a chave já marcada faria o rodízio
    /// pular uma chave boa sem nunca tentar.
    /// </summary>
    public void Save(string path)
    {
        var toml = new TomlStore { SchemaVersion = 1 };
        toml.SetTables("chave", _keys.Select(k =>
        {
            var t = new TomlStore();
            t.Set("id", k.Id);
            t.Set("segredo", k.Secret);
            t.Set("gratuita", k.IsFree);
            return t.Table;
        }));
        toml.Save(path);
    }
}
