using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inicializar configuración
ConfigurationOptions.Initialize(builder.Configuration);

// Registrar servicios de conexión
builder.Services.AddScoped<ConexionSql>(_ => {
    var conexion = new ConexionSql();
    conexion.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;
    return conexion;
});

builder.Services.AddSingleton<ConexionMongo>(_ =>
    new ConexionMongo(
        ConfigurationOptions.Instance.StrConexBdMongo,
        ConfigurationOptions.Instance.DatabaseNameMongo
    )
);

// Registrar servicios de aplicación
builder.Services.AddScoped<LoginService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowAllOrigins");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/AppEasyPayV4957*_ConexBd", () => {
    string cadenaConexion = ConfigurationOptions.Instance.StrConexBdSql;
    return Results.Ok(cadenaConexion);
});

app.Run();