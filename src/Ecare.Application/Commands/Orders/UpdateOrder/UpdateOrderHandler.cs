using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecare.Shared; // for IUnitOfWork

public sealed record UpdateOrderCommand(int OrderId, string ImageName) : IRequest<bool>;

public sealed class UpdateOrderHandler : IRequestHandler<UpdateOrderCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateOrderHandler> _log;

    public UpdateOrderHandler(IUnitOfWork uow, ILogger<UpdateOrderHandler> log)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _log = log;
    }

    public async Task<bool> Handle(UpdateOrderCommand request, CancellationToken ct)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        const string sql = @"
            UPDATE dbo.Orders
            SET ChequeImage = @ImageName
            WHERE Id = @OrderId;";

        await _uow.BeginAsync(ct);

        try
        {
            var conn = _uow.Connection
                       ?? throw new InvalidOperationException("UnitOfWork.Connection is null after BeginAsync.");

            var affected = await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new { ImageName = request.ImageName, OrderId = request.OrderId },
                    transaction: _uow.Transaction,
                    cancellationToken: ct));

            if (affected > 0)
            {
                await _uow.CommitAsync(ct);
                _log.LogInformation("Updated cheque image for Order {OrderId}.", request.OrderId);
                return true;
            }

            await _uow.RollbackAsync(ct);
            _log.LogWarning("No order found for Id {OrderId} when updating cheque image.", request.OrderId);
            return false;
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync(ct);
            _log.LogError(ex, "Error updating cheque image for Order {OrderId}.", request.OrderId);
            throw;
        }
    }
}
