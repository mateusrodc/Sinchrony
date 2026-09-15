namespace Sinchrony.Domain.Entities;

// Concessão direta usuário→permissão (sem tabela de perfis/grupos intermediária — ver
// justificativa em PLANEJAMENTO_CONTROLE_TOTAL_ADMIN_PERMISSOES.md, seção 3.2). Todo usuário
// Role.admin existente recebe automaticamente todas as permissões do catálogo no seed da
// migration (GrantedByUserId = null identifica essa concessão automática).
public class UserPermission
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime GrantedAt { get; private set; } = DateTime.UtcNow;
    public Guid? GrantedByUserId { get; private set; } // null = concessão automática (seed de migração)

    public User? User { get; private set; }
    public Permission? Permission { get; private set; }

    protected UserPermission() { }

    public static UserPermission Grant(Guid userId, Guid permissionId, Guid? grantedByUserId)
        => new() { UserId = userId, PermissionId = permissionId, GrantedByUserId = grantedByUserId };
}
