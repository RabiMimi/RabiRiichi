using Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Rpc;

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

// gRPC
var GrpcBuilder = services.AddGrpc();

IGrpcServerBuilder AddJwtInterceptor<TService>() where TService: class
    => GrpcBuilder.AddServiceOptions<TService>(o => o.Interceptors.Add<JwtInterceptor>());

AddJwtInterceptor<UserServiceImpl>();

// Objects for DI
services.AddSingleton<RoomList>();
services.AddSingleton<UserList>();
services.TryAddSingleton<TokenService>();

services.AddSingleton(new Random(builder.Environment.IsDevelopment()
    ? 0 : (int)(DateTimeOffset.Now.ToUnixTimeMilliseconds() & 0xffffffff)));

// Build app
var app = builder.Build();

// Use cors
app.UseCors("AllowAll");

// Add gRPC services
app.MapGrpcService<PublicServiceImpl>();
app.MapGrpcService<UserServiceImpl>();

// Run app
app.Run();
