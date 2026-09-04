using Gort.Core.Configuration;
using Gort.Core.Model;
using Tomlyn.Model;

namespace Gort.Core.Shortcuts;

/// <summary>
/// RF-037 / RF-453 — Persistência dos atalhos em arquivo próprio, como uma lista de
/// registros com o identificador da ação, a combinação de teclas e o parâmetro opcional.
///
/// RF-026 — Ações e teclas são persistidas por IDENTIFICADOR TEXTUAL, nunca por posição:
/// é isso que permite acrescentar ou reordenar ações sem invalidar os arquivos do usuário.
/// </summary>
public static class ShortcutStore
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// RF-024 — Leitor tolerante: um registro inválido é ignorado, e qualquer exceção
    /// restaura os padrões de RF-444 em vez de impedir a abertura.
    /// </summary>
    public static ShortcutSet Load(string path)
    {
        var set = ShortcutSet.WithDefaults();

        try
        {
            var store = TomlStore.Load(path, out bool recovered);
            if (recovered) return set;

            Migrations.Migrate(store, CurrentSchemaVersion);

            var records = store.GetTables("shortcut");
            if (records.Count == 0) return set;

            // Havendo arquivo, ele manda: um atalho limpo pelo usuário tem de continuar
            // limpo, e não voltar ao padrão na próxima abertura (RF-446).
            var loaded = new ShortcutSet();

            foreach (var record in records)
            {
                string? actionText = record.TryGetValue("action", out var a) ? a as string : null;
                if (actionText is null) continue;

                // RF-028 — um identificador desconhecido não impede o carregamento.
                if (!Enum.TryParse<ShortcutAction>(actionText, ignoreCase: true, out var action))
                    continue;

                var keys = record.TryGetValue("keys", out var k) && k is TomlArray array
                    ? array.OfType<string>().ToList()
                    : new List<string>();

                int index = record.TryGetValue("index", out var i) ? Convert.ToInt32(i) : 0;
                string? data = record.TryGetValue("data", out var d) ? d as string : null;

                loaded.Set(action, keys, index, data);
            }

            // Ações que o arquivo não menciona ganham o padrão: é o que faz um atalho novo
            // do programa aparecer configurado para quem já tinha o arquivo (RF-025).
            foreach (var (action, keys) in ShortcutSet.Defaults)
            {
                if (loaded.Find(action) is null) loaded.Set(action, keys);
            }

            return loaded;
        }
        catch
        {
            // RF-024 — qualquer exceção restaura os padrões e continua.
            return ShortcutSet.WithDefaults();
        }
    }

    public static void Save(string path, ShortcutSet set)
    {
        var store = new TomlStore();
        store.SchemaVersion = CurrentSchemaVersion;

        store.SetTables("shortcut", set.All.Select(s =>
        {
            var keys = new TomlArray();
            foreach (var key in s.Keys) keys.Add(key);

            var table = new TomlTable
            {
                ["action"] = s.Action.ToString(),
                ["keys"] = keys,
                ["index"] = (long)s.Index,
            };
            if (s.Data is not null) table["data"] = s.Data;
            return table;
        }));

        store.Save(path);
    }
}
