using RabiRiichi.Server.Models;
using RabiRiichi.Server.Rpc;

var builder = WebApplication.CreateBuilder(args);

// Cors
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll",
        builder => {
            builder.AllowAnyOrigin()
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

builder.Services.AddGrpc();

builder.Services.AddSingleton<RoomList>();
builder.Services.AddSingleton<UserList>();

builder.Services.AddSingleton(new Random(builder.Environment.IsDevelopment()
    ? 0 : (int)(DateTimeOffset.Now.ToUnixTimeMilliseconds() & 0xffffffff)));

var app = builder.Build();

app.UseCors("AllowAll");

app.MapGrpcService<InfoServiceImpl>();

app.Run();
