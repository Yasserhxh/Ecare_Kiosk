// Ecare.Application/Commands/Orders/AffectOrder/CreateOrderFromFormHandler.cs
using System.Data;
using Dapper;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Orders.AffectOrder;



public sealed class CreateOrderFromFormHandler(IUnitOfWork uow)
    : IRequestHandler<CreateOrderFromFormCommand, Result<int>>
{
    // Minimal insert: only columns you have values for + required NOT NULLs
    private const string InsertSql = @"
INSERT INTO dbo.Orders
(
    ShippingId,         -- required; we force 1 as requested
    NumeroCommande,     -- from form
    DateCommande,       -- set server-side
    ChauffeurNom,       -- from form (nullable)
    PlaqueCamion,       -- from form (Matricule)
    Statut,             -- required; choose a sensible initial value
    UserId,             -- required; from command
    NomComplet          -- client name from form
)
OUTPUT INSERTED.Id
VALUES
(
    @ShippingId,
    @NumeroCommande,
    SYSUTCDATETIME(),
    @ChauffeurNom,
    @PlaqueCamion,
    @Statut,
    @UserId,
    @NomComplet
);";

    public async Task<Result<int>> Handle(CreateOrderFromFormCommand cmd, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var p = new DynamicParameters();
            p.Add("ShippingId", 1, DbType.Int32);                     // ✅ force 1
            p.Add("NumeroCommande", cmd.NumeroCommande, DbType.String);
            p.Add("ChauffeurNom", cmd.ChauffeurNom, DbType.String);
            p.Add("PlaqueCamion", cmd.Matricule, DbType.String);
            p.Add("Statut", "EnTraitement", DbType.String);           // pick an initial status
            p.Add("UserId", cmd.UserId, DbType.String);
            p.Add("NomComplet", cmd.ClientName, DbType.String);

            var newId = await uow.Connection.ExecuteScalarAsync<int>(
                new CommandDefinition(InsertSql, p, uow.Transaction, cancellationToken: ct));

            await uow.CommitAsync(ct);
            return Result<int>.Ok(newId);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { /* ignore */ }
            throw;
        }
    }
}
