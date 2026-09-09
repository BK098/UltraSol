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
}
