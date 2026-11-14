using StockDesk.Application.Abstractions;
using StockDesk.Application.Common;
using StockDesk.Domain.Common;

namespace StockDesk.Application.Alerts;

public sealed class AlertService(IUnitOfWork uow, TimeProvider clock)
{
    public async Task<Result> AcknowledgeAsync(int alertId, string by, CancellationToken ct = default)
    {
        var alert = await uow.LowStockAlerts.GetByIdAsync(alertId, ct);
        if (alert is null)
            return Result.Failure("Alert not found.");

        try
        {
            alert.Acknowledge(by, clock.GetUtcNow().UtcDateTime);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (PersistenceException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
