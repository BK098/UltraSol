namespace UltraSol.Modules.Organization.Application.Messaging;
public interface IOrganizationOutbox
{
    void Add<T>(T message);
}