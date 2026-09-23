namespace B2BIntegrationHub.Data;

/// <summary>
/// Strongly typed binding for the "MongoDb" section of appsettings.json / environment variables.
/// </summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "b2b_integration_hub";
}
