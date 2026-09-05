using System.Diagnostics;

namespace Gort.Platform.Lifecycle;

/// <summary>
/// RF-001 / RF-002 — Uma instância por vez, com uma forma explícita de desativar a
/// restrição.
///
/// O mecanismo é um ARQUIVO DE TRAVA mantido aberto enquanto o programa vive, e não um
/// mutex nomeado: mutex nomeado é conceito do Windows, e a abstração de RF-577 não teria o
/// que oferecer nos outros sistemas. Um arquivo com trava exclusiva funciona nos três, e
/// tem uma propriedade que o mutex não tem — ele guarda o identificador do processo dono,
/// então uma trava órfã (deixada por um encerramento abrupto) é reconhecível e removível
/// em vez de bloquear o programa para sempre.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private FileStream? _lock;

    private SingleInstanceGuard(FileStream? handle, bool acquired, int? ownerPid)
    {
        _lock = handle;
        Acquired = acquired;
        OwnerProcessId = ownerPid;
    }

    /// <summary>Verdadeiro quando ESTA instância pode seguir.</summary>
    public bool Acquired { get; }

    /// <summary>Identificador do processo que já está rodando, quando dá para saber.</summary>
    public int? OwnerProcessId { get; }

    /// <summary>
    /// RF-002 — A restrição é desativada pela presença de um arquivo marcador.
    ///
    /// A busca é na pasta do EXECUTÁVEL e na pasta de dados do usuário: a primeira é onde
    /// RF-002 sugere, e a segunda é onde o usuário consegue criar um arquivo sem permissão
    /// de administrador. Qualquer uma das duas serve.
    /// </summary>
    public static bool MarkerPresent(string executableDirectory, string userDirectory)
        => File.Exists(Path.Combine(executableDirectory, Marker))
           || File.Exists(Path.Combine(userDirectory, Marker));

    public const string Marker = "multi-instancia";

    /// <summary>
    /// Tenta tomar a trava. Devolve um guarda com <see cref="Acquired"/> falso quando já há
    /// outra instância — o chamador informa o usuário e encerra (RF-001).
    /// </summary>
    public static SingleInstanceGuard Acquire(string lockFilePath)
    {
        try
        {
            var handle = new FileStream(
                lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            // O identificador do processo vai para dentro da trava: é o que permite a uma
            // segunda instância dizer QUAL processo está rodando, e não só que há um.
            handle.SetLength(0);
            using (var writer = new StreamWriter(handle, leaveOpen: true))
            {
                writer.Write(Environment.ProcessId);
            }
            handle.Flush();

            return new SingleInstanceGuard(handle, acquired: true, Environment.ProcessId);
        }
        catch (DirectoryNotFoundException)
        {
            // Uma pasta ausente NÃO é outra instância. `DirectoryNotFoundException` deriva
            // de `IOException`, então precisa vir antes: sem esta cláusula um caminho
            // inválido faria o programa se recusar a abrir, alegando que já está aberto.
            return new SingleInstanceGuard(null, acquired: true, null);
        }
        catch (IOException)
        {
            // A trava está com outro processo — ou ficou órfã.
            int? owner = ReadOwner(lockFilePath);

            if (owner is null || IsAlive(owner.Value))
                return new SingleInstanceGuard(null, acquired: false, owner);

            // Trava órfã: o dono não existe mais. Apagar e tentar UMA vez.
            try
            {
                File.Delete(lockFilePath);
                return Acquire(lockFilePath);
            }
            catch
            {
                return new SingleInstanceGuard(null, acquired: false, owner);
            }
        }
        catch
        {
            // Qualquer outra falha (pasta somente-leitura, por exemplo) não pode impedir o
            // programa de abrir: a restrição é uma conveniência, não uma garantia de
            // segurança. Na dúvida, deixa passar.
            return new SingleInstanceGuard(null, acquired: true, null);
        }
    }

    private static int? ReadOwner(string path)
    {
        try
        {
            string text = File.ReadAllText(path).Trim();
            return int.TryParse(text, out int pid) ? pid : null;
        }
        catch
        {
            // Em alguns sistemas a trava exclusiva impede até a leitura; nesse caso a
            // ausência de resposta já é a resposta: há alguém segurando o arquivo.
            return null;
        }
    }

    private static bool IsAlive(int pid)
    {
        if (pid == Environment.ProcessId) return true;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            // Sem certeza, trata como vivo: abrir uma segunda instância por engano é pior
            // que recusar por engano — duas instâncias disputariam o mesmo perfil.
            return true;
        }
    }

    public void Dispose()
    {
        var handle = _lock;
        _lock = null;
        if (handle is null) return;

        string? path = handle.Name;
        try { handle.Dispose(); } catch { /* P8 */ }
        try { if (path is not null) File.Delete(path); } catch { /* P8 */ }
    }
}
