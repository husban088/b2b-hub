using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.GraphQL;
using B2BIntegrationHub.Services;
 
var builder = WebApplication.CreateBuilder(args);
 
// ---------- Logging ----------
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());
 
// ---------- Configuration binding ----------
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
 
// ---------- Data + Services (DI) ----------
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
 
// ---------- AWS (S3 client available via DI for large file / backup storage) ----------
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
 
// ---------- JWT Authentication ----------
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
 
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});
builder.Services.AddAuthorization();
 
// ---------- CORS (Angular dev server + production origin) ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
 
// ---------- GraphQL (HotChocolate) ----------
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddInMemorySubscriptions()
    .AddUploadType() // registers the Upload scalar (needed for the IFile logo-upload mutation)
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());
 
// ---------- REST fallback (health checks, Swagger docs, simple webhook receivers) ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
 
var app = builder.Build();
 
// ---------- Ensure Mongo indexes on boot ----------
using (var scope = app.Services.CreateScope())
{
    var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    try
    {
        await mongoContext.EnsureIndexesAsync();
    }
    catch (Exception ex)
    {
        // Don't crash the whole API if MongoDB is unreachable - log a clear reason instead.
        app.Logger.LogError(ex,
            "Could not connect to MongoDB. Check MongoDb:ConnectionString (and Atlas Network Access / IP whitelist).");
    }
}
 
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
 
app.UseSerilogRequestLogging();
app.UseCors("AngularClient");
 
// REQUIRED for GraphQL subscriptions (live activity feed) - without this the WebSocket connection is rejected.
app.UseWebSockets();
 
app.UseAuthentication();
app.UseAuthorization();
 
app.MapControllers();
app.MapGraphQL("/graphql");
app.MapHealthChecks("/health");
 
app.Run();