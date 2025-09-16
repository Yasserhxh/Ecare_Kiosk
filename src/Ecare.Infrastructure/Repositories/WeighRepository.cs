using Dapper;
using Ecare.Domain.Entities;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IWeighRepository
{
    Task<Guid> InsertAsync(WeighRecord w, IUnitOfWork uow);
    Task<(int? Gross, int? Tare)> GetPaBWeightsAsync(Guid orderId, IUnitOfWork uow);
}

public sealed class WeighRepository : IWeighRepository
{
    public async Task<Guid> InsertAsync(WeighRecord w, IUnitOfWork uow)
    {
        w.Id = Guid.NewGuid();
        await uow.Connection.ExecuteAsync($@"INSERT INTO {DbTableNames.Weighings} (Id,OrderId,Stage,GrossKg,TareKg,NetKg,TakenAt)
            VALUES (@Id,@OrderId,@Stage,@GrossKg,@TareKg,@NetKg,@TakenAt)", w, uow.Transaction);
        return w.Id;
    }

    public async Task<(int? Gross, int? Tare)> GetPaBWeightsAsync(Guid orderId, IUnitOfWork uow)
    {
        var rows = await uow.Connection.QueryAsync<WeighRecord>($"SELECT * FROM {DbTableNames.Weighings} WHERE OrderId=@orderId",
            new { orderId }, uow.Transaction);
        var g = rows.FirstOrDefault(r => r.Stage == 1)?.GrossKg;
        var t = rows.FirstOrDefault(r => r.Stage == 2)?.TareKg;
        return (g, t);
    }
}
