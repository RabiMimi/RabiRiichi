using Microsoft.AspNetCore.Authentication.JwtBearer;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;

var builder = WebApplication.CreateBuilder(args);
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
var GrpcBuilder = services.AddGrpc();

// Objects for DI
services.AddSingleton<RoomList>();
services.AddSingleton<UserList>();
services.AddSingleton<TokenService>();

services.AddSingleton(new Random(builder.Environment.IsDevelopment()
    ? 0 : (int)(DateTimeOffset.Now.ToUnixTimeMilliseconds() & 0xffffffff)));

// Build app
var app = builder.Build();

// Use cors
app.UseCors("AllowAll");

// Use auth
app.UseAuthentication();
app.UseAuthorization();

// Map gRPC services
app.MapGrpcService<InfoServiceImpl>();
app.MapGrpcService<UserServiceImpl>();
app.MapGrpcService<RoomServiceImpl>();

// Run app
app.Run();
