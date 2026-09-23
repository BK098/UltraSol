using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Modules.Ordering.Domain.Events;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Ordering.Domain.Carts;

public enum CartStatus { Active, CheckingOut, CheckedOut }

public sealed class ShoppingCart : AggregateRoot
{
    private readonly List<CartItem> _items = [];
    private ShoppingCart() { }
    public string OwnerKey { get; private set; } = "";
    public Guid? IdentityUserId { get; private set; }
    public string? GuestTokenHash { get; private set; }
    public string Currency { get; private set; } = "";
    public CartStatus Status { get; private set; }
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public static ShoppingCart Create(string ownerKey, Guid? userId, string? guestTokenHash, string currency, DateTimeOffset now)
    {
        OrderingRule.Text(ownerKey, 100, "Owner");
        var cart = new ShoppingCart { Id = Guid.CreateVersion7(), OwnerKey = ownerKey, IdentityUserId = userId,
            GuestTokenHash = guestTokenHash, Currency = OrderingRule.Currency(currency) };
        cart.MarkCreated(userId?.ToString(), now);
        return cart;
    }

    public void AddItem(Guid productItemId, int quantity, DateTimeOffset now)
    {
        Editable();
        OrderingRule.Require(productItemId != Guid.Empty && quantity > 0, "Product and positive quantity are required.");
        var item = _items.SingleOrDefault(value => value.ProductItemId == productItemId);
        if (item is null)
        {
            OrderingRule.Require(_items.Count < 100, "A cart supports at most 100 lines.");
            _items.Add(new CartItem(productItemId, quantity));
        }
        else
        {
            item.Change(checked(item.Quantity + quantity));
        }
        MarkUpdated(IdentityUserId?.ToString(), now);
    }

    public void ChangeItem(Guid itemId, int quantity, DateTimeOffset now)
    {
        Editable();
        OrderingRule.Require(quantity > 0, "Quantity must be positive.");
        var item = _items.SingleOrDefault(value => value.Id == itemId);
        OrderingRule.Require(item is not null, "Cart item does not exist.");
        item!.Change(quantity);
        MarkUpdated(IdentityUserId?.ToString(), now);
    }

    public void RemoveItem(Guid itemId, DateTimeOffset now)
    {
        Editable();
        OrderingRule.Require(_items.RemoveAll(value => value.Id == itemId) == 1, "Cart item does not exist.");
        MarkUpdated(IdentityUserId?.ToString(), now);
    }

    public void Clear(DateTimeOffset now)
    {
        Editable();
        _items.Clear();
        MarkUpdated(IdentityUserId?.ToString(), now);
    }

    public void BeginCheckout(DateTimeOffset now)
    {
        Editable();
        OrderingRule.Require(_items.Count > 0, "Cart is empty.");
        Status = CartStatus.CheckingOut;
        MarkUpdated(IdentityUserId?.ToString(), now);
    }

    public void EndCheckout(bool succeeded, DateTimeOffset now)
    {
        OrderingRule.Require(Status == CartStatus.CheckingOut, "Cart is not checking out.");
        Status = succeeded ? CartStatus.CheckedOut : CartStatus.Active;
        MarkUpdated(IdentityUserId?.ToString(), now);
        if (succeeded)
        {
            AddDomainEvent(new CartCheckedOut(Id));
        }
    }

    private void Editable() => OrderingRule.Require(Status == CartStatus.Active, "Cart is not editable.", "CartNotActive");
}