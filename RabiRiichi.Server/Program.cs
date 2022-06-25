using Microsoft.AspNetCore.Server.Kestrel.Core;
using RabiRiichi.Server.Binders;
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

// Add services to the container.
/*
builder.Services.AddControllers(options => {
    options.ModelBinderProviders.Insert(0, new AuthBinderProvider());
    options.ModelBinderProviders.Insert(0, new RoomBinderProvider());
});

builder.Services.AddResponseCompression(options => {
    options.EnableForHttps = true;
});*/

builder.Services.AddGrpc();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<RoomList>();
builder.Services.AddSingleton<UserList>();

builder.Services.AddSingleton(new Random(builder.Environment.IsDevelopment()
    ? 0 : (int)(DateTimeOffset.Now.ToUnixTimeMilliseconds() & 0xffffffff)));

var app = builder.Build();

// Configure the HTTP request pipeline.
/*
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}
*/
app.UseCors("AllowAll");


// app.UseResponseCompression();
// app.UseWebSockets(new WebSocketOptions {
//    KeepAliveInterval = TimeSpan.FromSeconds(30),
// });

app.UseRouting();

// app.MapControllers();
app.UseEndpoints(endpoints => {
    endpoints.MapGrpcService<InfoServiceImpl>();
    endpoints.MapGet("/", async context => {
        await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
    });
});

app.Run();
