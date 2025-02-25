using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
ApiEasyPay.Seguridad.Helpers.ConfigurationOptions.Initialize(builder.Configuration);
builder.Services.AddSingleton(sp => new ConexionMongo(
    ConfigurationOptions.Instance.StrConexBdMongo,
    ConfigurationOptions.Instance.DatabaseNameMongo
));
builder.Services.AddScoped<LoginService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/AppEasyPayV4957*_ConexBd", () =>
{
    // Accede a la cadena de conexión según el entorno
    string cadenaConexion = ApiEasyPay.Seguridad.Helpers.ConfigurationOptions.Instance.StrConexBdSql;
    return Results.Ok(cadenaConexion);
});

app.Run();
