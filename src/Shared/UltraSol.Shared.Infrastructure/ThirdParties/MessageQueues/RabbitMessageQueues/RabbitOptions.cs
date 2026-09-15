namespace UltraSol.Shared.Infrastructure.ThirdParties.MessageQueues.RabbitMessageQueues
{
    public class RabbitOptions
    {
        public const string SectionName = "RabbitMQ";
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string VirtualHost { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Prefix { get; set; } = string.Empty;
    }
}