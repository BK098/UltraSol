namespace UltraSol.Shared.Infrastructure.Repositories;

/// <summary>Optional module-specific preparation performed after BEGIN and before business reads.</summary>
public interface ITransactionPreparation
{
    Task PrepareTransactionAsync(CancellationToken cancellationToken = default);
}

