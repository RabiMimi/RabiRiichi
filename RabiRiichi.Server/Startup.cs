using Microsoft.AspNetCore.Authentication.JwtBearer;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => {
  options.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromSeconds(10);
  options.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromMinutes(5);
});
var services = builder.Services;

// Cors
services.AddCors(options => {
  options.AddPolicy("AllowAll",
      builder => {
        builder.AllowAnyOrigin()
                 .AllowAnyHeader()
                 .AllowAnyMethod();
      });
});

// Auth
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
      options.TokenValidationParameters = TokenService.ValidationParameters;
      options.Events = new JwtBearerEvents {
        OnTokenValidated = context => {
          var tokenService = context.HttpContext.RequestServices
              .GetRequiredService<TokenService>();
          if (!tokenService.IsCurrentServerToken(context.Principal)) {
            context.Fail("Token belongs to a previous server instance");
          }
          return Task.CompletedTask;
        },
      };
    });
services.AddAuthorization();

// gRPC
services.AddGrpc();
services.AddControllers();

// LLM AI: outbound HTTP + provider factory + config validator.
services.AddHttpClient();
services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
services.AddSingleton<LlmValidator>();

// Objects for DI
services.AddSingleton<RoomList>();
services.AddSingleton<UserList>();
services.AddSingleton<TokenService>();
services.AddSingleton<RoomTaskQueue>();
services.AddSingleton<InfoServiceImpl>();
services.AddSingleton<UserServiceImpl>();
services.AddSingleton<RoomServiceImpl>();
services.AddSingleton<GameServiceImpl>();
var replayOptions = new ReplayOptions();
services.AddSingleton(replayOptions);
services.AddSingleton<ReplayStore>();
if (replayOptions.IsEnabled && replayOptions.TTL.HasValue) {
  services.AddHostedService<ReplayCleanupService>();
}

services.AddSingleton(new Random(builder.Environment.IsDevelopment()
    ? 0 : (int)(DateTimeOffset.Now.ToUnixTimeMilliseconds() & 0xffffffff)));

// Build app
var app = builder.Build();

// Wire the LLM provider factory into the process-wide runtime so AI agents
// (created outside a DI scope) share the app's HttpClient factory.
LlmRuntime.Factory = app.Services.GetRequiredService<ILlmProviderFactory>();

app.UseRouting();

// Use cors
app.UseCors("AllowAll");

// WebSocket
app.UseWebSockets(new WebSocketOptions {
  KeepAliveInterval = TimeSpan.FromSeconds(10),
});

// Use auth
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map gRPC services
/*
app.UseEndpoints(endpoints => {
    endpoints.MapGrpcService<InfoServiceImpl>();
    endpoints.MapGrpcService<UserServiceImpl>();
    endpoints.MapGrpcService<RoomServiceImpl>();
    endpoints.MapGrpcService<GameServiceImpl>();
});
*/

// Run app
app.Run();
