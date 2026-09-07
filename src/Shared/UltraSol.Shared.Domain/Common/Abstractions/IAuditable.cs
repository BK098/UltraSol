namespace UltraSol.Shared.Domain.Common.Abstractions;

/// <summary>
///  Marker interface for entity classes that support auditing (creation, update, deletion, soft delete)
/// </summary>
public interface IAuditable;

/// <summary>
/// Marker interface for entity classes that support creation auditing
/// </summary>
public interface ICreated : IAuditable
{
    string? CreatedBy { get; }
    DateTimeOffset CreatedAt { get; }
    void MarkCreated(string? createdBy, DateTimeOffset createdAt);
}
/// <summary>
/// Marker interface for entity classes that support update auditing
/// </summary>
public interface IUpdated : IAuditable
{
    string? UpdatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    void MarkUpdated(string? updatedBy, DateTimeOffset updatedAt);
}
/// <summary>
/// Marker interface for entity classes that support soft delete auditing
/// </summary>
public interface ISoftDeleted
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    string? DeletedBy { get; }
    void MarkDeleted(string? deletedBy, DateTimeOffset deleteAt);
    void Restore();
}

public interface IHasConcurrencyStamp
{
    string ConcurrencyStamp { get; }
    void RefreshConcurrencyStamp();
}
/// <summary>
/// Maker for entity classes that support creation and update auditing
/// </summary>
public interface ICreatedUpdated : ICreated, IUpdated
{
}
/// <summary>
/// Maker for entity classes that support creation, update and soft delete auditing
/// </summary>
public interface IFullAuditedEntity : ICreatedUpdated, ISoftDeleted, IHasConcurrencyStamp
{
}
/// <summary>
/// Maker for entity classes that support creation, update and hard delete auditing
/// </summary>