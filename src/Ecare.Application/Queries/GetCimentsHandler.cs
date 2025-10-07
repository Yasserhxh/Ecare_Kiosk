using MediatR;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using Ecare.Application.Dtos;

namespace Ecare.Application.Queries;
public sealed class GetCimentsHandler(IEcareCimentRepository repo, IUnitOfWork uow)
    : IRequestHandler<GetCimentsQuery, Result<IEnumerable<CimentDto>>>
{
    public async Task<Result<IEnumerable<CimentDto>>> Handle(GetCimentsQuery request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            var items = await repo.GetAllAsync(uow, ct);
            var dtos = items.Select(x => new CimentDto(x.Id, x.Name, x.ImageUrl, x.Details, x.Type, x.Description));
            await uow.CommitAsync(ct);
            return Result<IEnumerable<CimentDto>>.Ok(dtos);
        }
        catch
        {
            try { await uow.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}


