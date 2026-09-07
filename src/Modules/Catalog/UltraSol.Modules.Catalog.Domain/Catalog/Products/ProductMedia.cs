using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Guards;
namespace UltraSol.Modules.Catalog.Domain.Catalog.Products;

/// <summary>Media entity owned exclusively by Product.</summary>
public sealed class ProductMedia : BaseEntity
{
    // Used only for persistence materialization; public creation still enforces business rules.
    private ProductMedia() { Url = null!; }
    public string Url { get; private set; }
    public string? AltText { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    internal ProductMedia(string url, string? altText, int sortOrder, bool isPrimary)
    {
        Url = Guard.MediaUrl(url);
        AltText = altText?.Trim();
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }
    internal void Update(string url, string? altText)
    {
        var validatedUrl = Guard.MediaUrl(url);
        Url = validatedUrl;
        AltText = altText?.Trim();
    }
    internal void SetOrder(int order)
    {
        SortOrder = order;
    }
    internal void SetPrimary(bool primary)
    {
        IsPrimary = primary;
    }
}