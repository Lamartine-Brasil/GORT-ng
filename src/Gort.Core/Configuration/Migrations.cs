namespace Gort.Core.Configuration;

/// <summary>
/// RF-038 — Cadeia de migrações.
///
/// Cada arquivo de dados do usuário registra a versão do seu esquema, e o programa contém
/// uma cadeia de migrações que leva qualquer versão anterior DESTE programa até a atual,
/// executada na leitura e gravada de volta.
///
/// Chaves desconhecidas de uma versão MAIS NOVA são preservadas intactas na regravação —
/// garantido pelo <see cref="TomlStore"/>, que só escreve por cima das chaves conhecidas.
/// Motivo: o usuário pode alternar entre uma versão nova e uma antiga do programa, e não
/// pode perder configuração por isso.
///
/// RF-564 — Não há migração de nenhum produto anterior. A cadeia só cobre versões deste
/// programa.
/// </summary>
public static class Migrations
{
    /// <summary>Uma etapa da cadeia: leva o arquivo da versão <see cref="From"/> para From+1.</summary>
    public delegate void Step(TomlStore store);

    private static readonly Dictionary<int, Step> Chain = new()
    {
        // Ainda não há versões anteriores deste programa: a versão 1 é a primeira.
        // Quando a versão 2 existir, acrescente aqui:
        //   [1] = store => { ... renomeia/converte chaves da v1 para a v2 ... },
    };

    /// <summary>
    /// Migra o armazenamento até <paramref name="targetVersion"/>.
    ///
    /// Um arquivo SEM versão (0) é tratado como já estando na versão atual: é o caso de um
    /// arquivo recém-criado ou editado à mão pelo usuário, e recusá-lo contrariaria RF-024.
    /// Um arquivo de versão MAIOR que a atual é deixado como está — suas chaves
    /// desconhecidas serão preservadas na regravação.
    /// </summary>
    public static void Migrate(TomlStore store, int targetVersion)
    {
        int version = store.SchemaVersion;
        if (version <= 0)
        {
            store.SchemaVersion = targetVersion;
            return;
        }

        while (version < targetVersion && Chain.TryGetValue(version, out var step))
        {
            step(store);
            version++;
            store.SchemaVersion = version;
        }

        if (version < targetVersion)
        {
            // Sem etapa registrada para esta versão: assume-se compatível e apenas se
            // atualiza o número, em vez de recusar o arquivo (P7).
            store.SchemaVersion = targetVersion;
        }
    }
}
