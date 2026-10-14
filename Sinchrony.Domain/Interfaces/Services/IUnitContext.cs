namespace Sinchrony.Domain.Interfaces.Services
{
    public interface IUnitContext
    {
        Guid? UnitId { get; }
        bool IsGlobalAdmin { get; }
        Guid? UserId { get; }

        // Unidades que o usuário logado gerencia: User.UnitId (claim) mais as marcadas em
        // "Unidades (filiais)" do cadastro (TeacherUnits). Admin cadastrado pelo ERP só tem as segundas.
        Task<IReadOnlyList<Guid>> GetManagedUnitIdsAsync(CancellationToken ct = default);
    }
}
