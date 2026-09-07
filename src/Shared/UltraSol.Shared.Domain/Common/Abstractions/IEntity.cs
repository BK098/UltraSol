namespace UltraSol.Shared.Domain.Common.Abstractions
{
    public interface IEntity
    {
        object GetId();
    }

    public interface IEntity<TKey> : IEntity where TKey : notnull
    {
        TKey Id { get; }
    }
}