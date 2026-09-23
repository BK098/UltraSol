namespace UltraSol.Shared.Application.Authentication;

public interface ICurrentAccount
{
    Guid? UserId { get; }
    Guid? SessionId { get; }
}