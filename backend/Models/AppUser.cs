using HotChocolate;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BIntegrationHub.Models;

public enum UserRole
{
    Admin,
    Operator,
    Viewer
}

/// <summary>
/// A staff account that can log into the hub's dashboard.
/// </summary>
public class AppUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Never expose the password hash through the GraphQL API.
    [GraphQLIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Viewer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}