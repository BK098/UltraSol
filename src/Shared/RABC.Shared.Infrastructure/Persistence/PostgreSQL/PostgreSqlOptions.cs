namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL
{
    public class PostgreSqlOptions
    {
        public const string SectionName = "Postgres";
        public string ConnectionString { get; set; } = string.Empty;
    }
}