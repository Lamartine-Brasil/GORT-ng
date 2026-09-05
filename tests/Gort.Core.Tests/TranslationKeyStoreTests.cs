using Gort.Core.Calibration;
using Gort.Core.Translation.Keys;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-250 a RF-253 — O rodízio de chaves.</summary>
public class TranslationKeyStoreTests
{
    private static TranslationKeyStore With(params (string Id, bool Free)[] keys)
    {
        var store = new TranslationKeyStore();
        foreach (var (id, free) in keys) store.Set(id, $"segredo-{id}", free);
        return store;
    }

    /// <summary>RF-250 / P-55 — Até P-55 pares; o excedente é recusado.</summary>
    [Fact]
    public void RF_250_o_rodizio_tem_teto_de_P55()
    {
        var store = new TranslationKeyStore();
        for (int i = 0; i < P.MaxRotatingApiKeys; i++) store.Set($"k{i}", "s", true);

        Assert.True(store.IsFull);
        Assert.Null(store.Set("excedente", "s", true));
        Assert.Equal(P.MaxRotatingApiKeys, store.Keys.Count);

        // Editar uma existente continua funcionando com o repositório cheio.
        Assert.NotNull(store.Set("k0", "outro", false));
    }

    /// <summary>
    /// RF-538 — O mesmo identificador ATUALIZA em vez de duplicar. É o que faz o botão da
    /// janela alternar entre "adicionar" e "editar": a decisão é do repositório.
    /// </summary>
    [Fact]
    public void RF_538_o_mesmo_identificador_atualiza_em_vez_de_duplicar()
    {
        var store = With(("alfa", true));
        store.Set("alfa", "novo-segredo", isFree: false);

        var key = Assert.Single(store.Keys);
        Assert.Equal("novo-segredo", key.Secret);
        Assert.False(key.IsFree);
    }

    /// <summary>
    /// Editar devolve a chave ao rodízio: o usuário acabou de corrigir o que provavelmente
    /// causou o erro, e mantê-la marcada a deixaria de fora sem motivo.
    /// </summary>
    [Fact]
    public void Editar_uma_chave_com_erro_devolve_ela_ao_rodizio()
    {
        var store = With(("alfa", true));
        store.Find("alfa")!.State = KeyState.Error;

        store.Set("alfa", "segredo-corrigido", true);

        Assert.Equal(KeyState.Normal, store.Find("alfa")!.State);
    }

    /// <summary>
    /// RF-252 — A ordem é: gratuitas antes das pagas, começando pela primeira em estado
    /// NORMAL. Gastar as gratuitas primeiro deixa as pagas para quando não há alternativa.
    /// </summary>
    [Fact]
    public void RF_252_gratuitas_antes_das_pagas_e_normais_antes_das_marcadas()
    {
        var store = With(("paga1", false), ("gratis1", true), ("gratis2", true),
                         ("paga2", false));
        store.Find("gratis1")!.State = KeyState.Limit;

        var ordered = store.Ordered().Select(k => k.Id).ToList();

        Assert.Equal(new[] { "gratis2", "gratis1", "paga1", "paga2" }, ordered);
    }

    [Fact]
    public void A_chave_corrente_e_a_primeira_normal_da_ordem()
    {
        var store = With(("gratis", true), ("paga", false));
        Assert.Equal("gratis", store.Current()!.Id);

        store.Find("gratis")!.State = KeyState.Limit;
        Assert.Equal("paga", store.Current()!.Id);
    }

    /// <summary>
    /// RF-250 / RF-251 — Trocar marca a que falhou e devolve a que assumiu, para o serviço
    /// poder anexar a nota dizendo qual passou a ser usada.
    /// </summary>
    [Fact]
    public void RF_251_a_troca_devolve_a_chave_que_assumiu()
    {
        var store = With(("gratis", true), ("paga", false));
        var atual = store.Current()!;

        var proxima = store.Rotate(atual, KeyState.Limit);

        Assert.Equal("paga", proxima!.Id);
        Assert.Equal(KeyState.Limit, store.Find("gratis")!.State);
    }

    /// <summary>
    /// Quando acabam, a troca devolve NULA: o serviço precisa distinguir "troquei" de
    /// "não há mais", porque a segunda é um erro para o usuário e a primeira não é.
    /// </summary>
    [Fact]
    public void Sem_chaves_sobrando_a_troca_devolve_nula()
    {
        var store = With(("unica", true));

        Assert.Null(store.Rotate(store.Current()!, KeyState.Error));
    }

    [Fact]
    public void Reiniciar_estados_devolve_todas_ao_rodizio()
    {
        var store = With(("a", true), ("b", true));
        store.Find("a")!.State = KeyState.Limit;
        store.Find("b")!.State = KeyState.Error;

        store.ResetStates();

        Assert.All(store.Keys, k => Assert.Equal(KeyState.Normal, k.State));
    }

    /// <summary>
    /// O ESTADO não é gravado: ele descreve a última sessão, não a chave. Uma cota estourada
    /// ontem pode ter virado hoje, e abrir o programa com a chave já marcada faria o rodízio
    /// pular uma chave boa sem nunca tentar.
    /// </summary>
    [Fact]
    public void O_estado_nao_sobrevive_a_gravacao()
    {
        string file = Path.Combine(Path.GetTempPath(), "gort-chaves",
                                   Guid.NewGuid().ToString("N") + ".toml");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var store = With(("alfa", true), ("beta", false));
        store.Find("alfa")!.State = KeyState.Limit;
        store.Save(file);

        var reloaded = TranslationKeyStore.Load(file);

        Assert.Equal(2, reloaded.Keys.Count);
        Assert.Equal("segredo-alfa", reloaded.Find("alfa")!.Secret);
        Assert.False(reloaded.Find("beta")!.IsFree);
        Assert.All(reloaded.Keys, k => Assert.Equal(KeyState.Normal, k.State));
    }

    [Fact]
    public void Um_arquivo_ausente_ou_ilegivel_devolve_um_rodizio_vazio()
    {
        Assert.Empty(TranslationKeyStore.Load("/caminho/que/nao/existe.toml").Keys);

        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".toml");
        File.WriteAllText(file, "isto = não [é toml");
        Assert.Empty(TranslationKeyStore.Load(file).Keys);
    }

    [Fact]
    public void Um_identificador_vazio_nao_vira_chave()
    {
        var store = new TranslationKeyStore();
        Assert.Null(store.Set("", "segredo", true));
        Assert.Null(store.Set("   ", "segredo", true));
        Assert.Empty(store.Keys);
    }
}
