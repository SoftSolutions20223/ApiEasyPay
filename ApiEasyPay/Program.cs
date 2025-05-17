using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using ApiEasyPay.Helpers;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;

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
builder.Services.AddScoped<UsuariosService>();
builder.Services.AddScoped<CustomJsonSerializer>();
builder.Services.AddScoped<CustomJsonDeserializer>();
builder.Services.AddScoped<SincronizacionService>();
builder.Services.AddScoped<CreditosService>();
builder.Services.AddScoped<BolsasService>();
builder.Services.AddScoped<VersionAppService>();



builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });
builder.Services.AddHttpContextAccessor();

// Registrar el servicio inicializador como singleton
builder.Services.AddSingleton<SesionInicializadorService>();
// Registrar el servicio hosteado directamente
builder.Services.AddHostedService<SesionHostedService>();


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