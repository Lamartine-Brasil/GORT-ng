using Gort.Platform.Lifecycle;
using Xunit;

namespace Gort.Platform.Tests;

/// <summary>RF-001 / RF-002 — Uma instância por vez, com desativação explícita.</summary>
public class SingleInstanceGuardTests
{
    private static string TempFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-instancia",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "trava");
    }

    /// <summary>RF-001 — A primeira instância toma a trava; a segunda não.</summary>
    [Fact]
    public void RF_001_a_segunda_instancia_nao_toma_a_trava()
    {
        string path = TempFile();

        using var first = SingleInstanceGuard.Acquire(path);
        Assert.True(first.Acquired);

        using var second = SingleInstanceGuard.Acquire(path);
        Assert.False(second.Acquired);
    }

    /// <summary>Soltar a trava libera a vez para a próxima.</summary>
    [Fact]
    public void Soltar_a_trava_libera_a_vez()
    {
        string path = TempFile();

        var first = SingleInstanceGuard.Acquire(path);
        Assert.True(first.Acquired);
        first.Dispose();

        using var second = SingleInstanceGuard.Acquire(path);
        Assert.True(second.Acquired);
    }

    /// <summary>
    /// Uma trava ÓRFÃ — deixada por um encerramento abrupto, com o identificador de um
    /// processo que não existe mais — não pode bloquear o programa para sempre.
    /// </summary>
    [Fact]
    public void Uma_trava_orfa_nao_bloqueia_o_programa()
    {
        string path = TempFile();

        // Um identificador que seguramente não corresponde a processo vivo.
        File.WriteAllText(path, "2147483646");

        using var guard = SingleInstanceGuard.Acquire(path);
        Assert.True(guard.Acquired);
    }

    /// <summary>A trava guarda QUAL processo é o dono, não só que há um.</summary>
    [Fact]
    public void A_trava_guarda_o_identificador_do_dono()
    {
        string path = TempFile();

        using var guard = SingleInstanceGuard.Acquire(path);

        Assert.True(guard.Acquired);
        Assert.Equal(Environment.ProcessId, guard.OwnerProcessId);
    }

    /// <summary>
    /// RF-002 — O marcador desativa a restrição. Ele vale tanto na pasta do executável, que
    /// é onde RF-002 sugere, quanto na de dados do usuário, que é onde ele consegue criar
    /// um arquivo sem permissão de administrador.
    /// </summary>
    [Fact]
    public void RF_002_o_marcador_vale_nas_duas_pastas()
    {
        string a = Path.Combine(Path.GetTempPath(), "gort-marca-a", Guid.NewGuid().ToString("N"));
        string b = Path.Combine(Path.GetTempPath(), "gort-marca-b", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        Assert.False(SingleInstanceGuard.MarkerPresent(a, b));

        File.WriteAllText(Path.Combine(a, SingleInstanceGuard.Marker), "");
        Assert.True(SingleInstanceGuard.MarkerPresent(a, b));

        File.Delete(Path.Combine(a, SingleInstanceGuard.Marker));
        File.WriteAllText(Path.Combine(b, SingleInstanceGuard.Marker), "");
        Assert.True(SingleInstanceGuard.MarkerPresent(a, b));
    }

    /// <summary>
    /// Uma pasta que não dá para escrever não pode impedir o programa de abrir: a restrição
    /// é conveniência, não garantia. Na dúvida, deixa passar.
    /// </summary>
    [Fact]
    public void Um_caminho_impossivel_nao_impede_o_programa_de_abrir()
    {
        using var guard = SingleInstanceGuard.Acquire("/pasta/que/nao/existe/trava");
        Assert.True(guard.Acquired);
    }
}
