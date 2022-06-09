using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => {
    options.ModelBinderProviders.Insert(0, new AuthBinderProvider());
    options.ModelBinderProviders.Insert(0, new RoomBinderProvider());
});

builder.Services.AddResponseCompression(options => {
    options.EnableForHttps = true;
});

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

app.UseAuthorization();
app.UseResponseCompression();
app.UseWebSockets();

app.MapControllers();

app.Run();
