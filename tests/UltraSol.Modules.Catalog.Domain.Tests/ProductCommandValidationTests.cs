using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class ProductCommandValidationTests
{
    [Fact]
    public void CreateRejectsMissingBody()
    {
        var result = new CreateProductValidator().Validate(new CreateProductCommand(null));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateRejectsBlankName(string? name)
    {
        var result = new CreateProductValidator().Validate(new CreateProductCommand(new CreateProductDto(name, null)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void LifecycleCommandsRejectEmptyProductId()
    {
        Assert.False(new PublishProductValidator().Validate(new PublishProductCommand(Guid.Empty)).IsValid);
        Assert.False(new UnpublishProductValidator().Validate(new UnpublishProductCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void ProductRelationshipCommandsRejectEmptyIds()
    {
        Assert.False(new AssignProductCategoryValidator().Validate(new AssignProductCategoryCommand(Guid.Empty, Guid.NewGuid())).IsValid);
        Assert.False(new RemoveProductCategoryValidator().Validate(new RemoveProductCategoryCommand(Guid.NewGuid(), Guid.Empty)).IsValid);
        Assert.False(new RemoveProductMediaValidator().Validate(new RemoveProductMediaCommand(Guid.Empty, Guid.Empty)).IsValid);
        Assert.False(new DiscardDraftProductValidator().Validate(new DiscardDraftProductCommand(Guid.Empty)).IsValid);
        Assert.False(new ChangeProductBrandValidator().Validate(new ChangeProductBrandCommand(Guid.NewGuid(), new ChangeProductBrandDto(Guid.Empty))).IsValid);
    }

    [Fact]
    public void AddMediaRequiresProductAndUrl()
    {
        var validator = new AddProductMediaValidator();

        Assert.False(validator.Validate(new AddProductMediaCommand(Guid.Empty, new AddProductMediaDto("https://cdn.example/image.jpg", null))).IsValid);
        Assert.False(validator.Validate(new AddProductMediaCommand(Guid.NewGuid(), new AddProductMediaDto("   ", null))).IsValid);
        Assert.False(validator.Validate(new AddProductMediaCommand(Guid.NewGuid(), null)).IsValid);
    }
}
