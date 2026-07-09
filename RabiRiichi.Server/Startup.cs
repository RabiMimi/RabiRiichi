using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    });
services.AddAuthorization();

// gRPC
services.AddGrpc();
services.AddControllers();

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
