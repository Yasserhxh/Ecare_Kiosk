using Dapper;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Flux.Create;

public sealed class CreateFluxEntryHandler(IUnitOfWork uow)
    : IRequestHandler<CreateFluxEntryCommand, Result<int>>
{
    private const string InsertSql = @"
        INSERT INTO dbo.EcareFlux
        (
            BonDeCommande,
            Quantity,
            Matricule,
            CarteSlv,
            DriverName,
            ClientName,
            ParkedAt,
            OrderId           
        )
        OUTPUT INSERTED.Id
        VALUES
        (
            @BonDeCommande,
            @Quantity,
            @Matricule,
            @CarteSlv,
            @DriverName,
            @ClientName,
            @ParkedAt,
            @OrderId         
        );";

    public async Task<Result<int>> Handle(CreateFluxEntryCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var newId = await uow.Connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    InsertSql,
                    new
                    {
                        request.BonDeCommande,
                        request.Quantity,
                        request.Matricule,
                        request.CarteSlv,
                        request.DriverName,
                        request.ClientName,
                        request.ParkedAt,
                        request.OrderId 
                    },
                    uow.Transaction,
                    cancellationToken: ct));

            await uow.CommitAsync(ct);
            return Result<int>.Ok(newId);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
