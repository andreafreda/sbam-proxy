
using Microsoft.Data.SqlClient;
using System.Xml.Linq;

namespace SbamProxy;

public class CountsFetcher
{
    private readonly string _connectionString;
    private const string DbName = "SbMessageContainerDatabase00001";

    public CountsFetcher()
    {
        var sqlHost = Environment.GetEnvironmentVariable("SQL_HOST") ?? "sqledge";
        var sqlPass = "YourStrong!Passw0rd";
        _connectionString = $"Server={sqlHost},1433;Database={DbName};User Id=sa;Password={sqlPass};TrustServerCertificate=True;Timeout=5;";
        Console.WriteLine($"[CountsFetcher] Ready to query sqledge as sa.");
    }

    public async Task<(long active, long dlq)> GetQueueCountsAsync(string queueName)
    {
        return await QuerySqlCount($"SBEMULATORNS:QUEUE:{queueName.ToUpper()}");
    }

    public async Task<(long active, long dlq)> GetSubscriptionCountsAsync(string topicName, string subscriptionName)
    {
        var entityKey = $"SBEMULATORNS:TOPIC:{topicName.ToUpper()}|{subscriptionName.ToUpper()}";
        return await QuerySqlCount(entityKey);
    }

    private async Task<(long active, long dlq)> QuerySqlCount(string entityName)
    {
        try 
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // SubqueueType 0 = Active
            // SubqueueType 1 or 3 = DeadLetter
            // State 0 = Active/Ready (not expired or scheduled)
            var sql = @"
                SELECT 
                    SUM(CASE WHEN m.SubqueueType = 0 AND m.State = 0 THEN 1 ELSE 0 END) as Active,
                    SUM(CASE WHEN m.SubqueueType IN (1, 3) THEN 1 ELSE 0 END) as DLQ
                FROM (
                    SELECT EntityId, EntityGroupId, SubqueueType, State FROM MessagesTable
                    UNION ALL
                    SELECT EntityId, EntityGroupId, SubqueueType, State FROM MessageReferencesTable
                ) m
                INNER JOIN EntityLookupTable e ON m.EntityId = e.Id AND m.EntityGroupId = e.EntityGroupId
                WHERE e.Name = @name";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", entityName);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                long active = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader[0]);
                long dlq = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader[1]);
                
                Console.WriteLine($"[CountsFetcher] SQL Result for {entityName}: Active={active}, DLQ={dlq}");
                return (active, dlq);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CountsFetcher] SQL ERROR for {entityName}: {ex.Message}");
        }
        return (0, 0);
    }
}
