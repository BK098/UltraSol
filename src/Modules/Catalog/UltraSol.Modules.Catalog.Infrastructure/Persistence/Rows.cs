namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

// Relational join rows are persistence details, not domain entities.
internal sealed class ProductCategoryRow
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }
}
internal sealed class SelectionRow
{
    public Guid ProductItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid VariationId { get; set; }
    public Guid OptionId { get; set; }
}
internal sealed class BundleRow
{
    public Guid BundleItemId { get; set; }
    public Guid ComponentItemId { get; set; }
    public int Quantity { get; set; }
}
internal sealed class CollectionEntryRow
{
    public Guid CollectionId { get; set; }
    public Guid ProductId { get; set; }
    public int SortOrder { get; set; }
}
internal sealed class RuleBrandRow
{
    public Guid CollectionId { get; set; }
    public Guid BrandId { get; set; }
}
internal sealed class RuleCategoryRow
{
    public Guid CollectionId { get; set; }
    public Guid CategoryId { get; set; }
}