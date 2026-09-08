namespace Annap.CoffeeQrOrdering.Application.Abstractions.KiotViet;

public interface IKiotVietIntegrationHealth
{
    string CircuitState { get; }
    void RecordSuccess();
    void RecordFailure();
}
