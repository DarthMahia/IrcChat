using IrcChat.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configuration des services via les extensions
builder.Services
    .AddDatabaseServices(builder.Configuration)
    .AddApplicationServices(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddIrcChatAuthorization()
    .AddCorsConfiguration(builder.Configuration)
    .AddSwaggerConfiguration();

var app = builder.Build();

// Initialisation et configuration du pipeline
await app.InitializeDatabaseAsync();

app.ConfigurePipeline()
   .MapApiEndpoints();

await app.RunAsync();