namespace Domain.Common.Exceptions
{
    public class DomainException : Exception
    {
        public string? Code { get; }

        public DomainException(string message, string? code = null)
            : base(message)
        {
            Code = code;
        }

        public DomainException(string message, Exception innerException, string? code = null)
            : base(message, innerException)
        {
            Code = code;
        }
    }

    public sealed class EntityNotFoundException : DomainException
    {
        public string EntityName { get; }
        public object? EntityId { get; }

        public EntityNotFoundException(string entityName, object? entityId)
            : base($"{entityName} '{entityId}' was not found.", "entity_not_found")
        {
            EntityName = entityName;
            EntityId = entityId;
        }

        public static EntityNotFoundException For<TEntity>(object? id) =>
            new(typeof(TEntity).Name, id);
    }

    public sealed class DomainValidationException : DomainException
    {
        public IReadOnlyDictionary<string, string[]> Errors { get; }

        public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.", "validation_error")
        {
            Errors = errors;
        }
    }
}