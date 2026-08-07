namespace API.Interfaces;

public interface IStatusRefreshService
{
    Task RefreshAsync(CancellationToken ct);
}
