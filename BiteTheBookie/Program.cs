using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

class Program
{
    static async Task Main(string[] args)
    {
        // The Key Vault URI (set as a non-secret environment variable in the container app).
        var kvUri = Environment.GetEnvironmentVariable("KEY_VAULT_URI");
        if (string.IsNullOrEmpty(kvUri))
        {
            throw new InvalidOperationException("KEY_VAULT_URI is not set");
        }

        var secretName = "SqlPassword"; // Name of the secret in Key Vault

        var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
        KeyVaultSecret secret = await client.GetSecretAsync(secretName);
        var sqlPassword = secret.Value;

        // Example: build connection string using password retrieved from Key Vault
        var sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "your-sql-server.database.windows.net";
        var sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "dbuser";
        var connString = $"Server=tcp:{sqlServer},1433;Initial Catalog=YourDatabase;Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        Console.WriteLine($"Got secret length: {sqlPassword?.Length ?? 0}"); // avoid printing secret value
        // Use connString with your DB client (e.g., Dapper, EF Core, SqlClient)
    }
}
