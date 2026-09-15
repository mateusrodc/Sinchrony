namespace Sinchrony.Domain.Entities;

// Catálogo fixo de permissões (Resource × Action), seedado via migration. Não é editável
// pelo usuário — é o dicionário de todas as combinações possíveis que UserPermission pode
// conceder. Ver DEMANDA_CONTROLE_ADMIN_PERMISSOES_BACKEND.md, Fase 0.
public class Permission
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Resource { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty; // create | edit | view | delete

    protected Permission() { }

    public static Permission Create(string resource, string action)
        => new() { Resource = resource, Action = action };
}
