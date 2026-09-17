using Gort.Core.Configuration;
using Gort.Core.Model;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-039 — Carregar um perfil: aplicar, normalizar, salvar.</summary>
public class ProfileCopyTests
{
    /// <summary>
    /// RF-039 — A cópia é por reflexão, e não campo a campo: uma lista escrita à mão ficaria
    /// desatualizada em silêncio no dia em que alguém acrescentasse uma propriedade.
    ///
    /// Este teste guarda essa promessa: TODA propriedade gravável do perfil é copiada.
    /// </summary>
    [Fact]
    public void RF_039_toda_propriedade_gravavel_e_copiada()
    {
        var origem = Profile.Defaults();
        var destino = Profile.Defaults();

        // Marca cada propriedade com um valor distinto do padrão.
        foreach (var property in typeof(Profile).GetProperties())
        {
            if (!property.CanRead || !property.CanWrite) continue;

            object? novo = property.PropertyType switch
            {
                var t when t == typeof(bool) => !(bool)property.GetValue(origem)!,
                var t when t == typeof(int) => (int)property.GetValue(origem)! + 7,
                var t when t == typeof(double) => (double)property.GetValue(origem)! + 0.25,
                var t when t == typeof(string) => "marcado",
                _ => null,
            };

            if (novo is not null) property.SetValue(origem, novo);
        }

        destino.CopyFrom(origem);

        foreach (var property in typeof(Profile).GetProperties())
        {
            if (!property.CanRead || !property.CanWrite) continue;
            if (property.PropertyType.IsGenericType) continue;   // listas, conferidas abaixo

            Assert.Equal(property.GetValue(origem), property.GetValue(destino));
        }
    }

    /// <summary>
    /// As coleções são COPIADAS, não compartilhadas: sem isso os dois perfis apontariam para
    /// a mesma lista, e mexer num mexeria no outro.
    /// </summary>
    [Fact]
    public void As_colecoes_sao_copiadas_e_nao_compartilhadas()
    {
        var origem = Profile.Defaults();
        origem.ColorGroups.Clear();
        origem.ColorGroups.Add(new ColorGroup { R = 10, G = 20, B = 30 });

        var destino = Profile.Defaults();
        destino.CopyFrom(origem);

        Assert.Single(destino.ColorGroups);
        Assert.Equal(10, destino.ColorGroups[0].R);

        // Mexer na cópia não mexe no original.
        destino.ColorGroups[0].R = 200;
        Assert.Equal(10, origem.ColorGroups[0].R);

        destino.ColorGroups.Add(new ColorGroup());
        Assert.Single(origem.ColorGroups);
    }
}
