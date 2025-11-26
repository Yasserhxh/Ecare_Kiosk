using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using Ecare.Application.Dtos;
using Dapper;

namespace Ecare.Application.Queries;

public sealed class GetFluxQualiteHandler(IUnitOfWork uow)
    : IRequestHandler<GetFluxQualiteQuery, Result<IEnumerable<FluxQualiteDto>>>
{
    public async Task<Result<IEnumerable<FluxQualiteDto>>> Handle(GetFluxQualiteQuery request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var sql = @"
                SELECT 
                    'ATCLK' as Flux,
                    'Asment Clinker' as NombreFlux,
                    CAST(ec.Id AS VARCHAR(20)) as Produit,
                    ec.Name as NomProduit,
                    'true' as Actif
                FROM [dbo].[EcareCiments] ec
                WHERE ec.Type LIKE '%CLINKER%'

                UNION ALL

                SELECT 
                    'ATSAC' as Flux,
                    'Asment - Sac + Pal' as NombreFlux,
                    CAST(ec.Id AS VARCHAR(20)) as Produit,
                    ec.Name as NomProduit,
                    'true' as Actif
                FROM [dbo].[EcareCiments] ec
                WHERE ec.Type LIKE '%SAC%' OR ec.Name LIKE '%SAC%'

                UNION ALL

                SELECT 
                    'ATVR' as Flux,
                    'Asment Temara VRAC' as NombreFlux,
                    CAST(ec.Id AS VARCHAR(20)) as Produit,
                    ec.Name as NomProduit,
                    'true' as Actif
                FROM [dbo].[EcareCiments] ec
                WHERE ec.Type LIKE '%VRAC%' OR ec.Name LIKE '%VRAC%'

                UNION ALL

                SELECT 
                    'GRB' as Flux,
                    'Grabemaro' as NombreFlux,
                    CAST(ag.Article_Id AS VARCHAR(20)) as Produit,
                    ag.Designation as NomProduit,
                    'true' as Actif
                FROM [dbo].[ArticleGranulats] ag
                WHERE ag.Designation LIKE '%GRAV%' 
                   OR ag.Designation LIKE '%SABLE%' 
                   OR ag.Designation LIKE '%CALCAIRE%'
                   OR ag.Designation LIKE '%GNA%'

                ORDER BY Flux, NomProduit";

            var cmd = new CommandDefinition(sql, transaction: uow.Transaction, cancellationToken: ct);
            var items = await uow.Connection.QueryAsync<FluxQualiteDto>(cmd);

            await uow.CommitAsync(ct);
            return Result<IEnumerable<FluxQualiteDto>>.Ok(items);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
