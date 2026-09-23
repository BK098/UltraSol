namespace UltraSol.Modules.Pricing.Application.Features.SkuPrices.Models;

public sealed record ProposePriceCancellationDto(Guid PeriodId, string? Reason);