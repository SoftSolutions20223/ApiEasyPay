var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
ApiEasyPay.Seguridad.Helpers.ConfigurationOptions.Initialize(builder.Configuration);
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
    string cadenaConexion = ApiEasyPay.Seguridad.Helpers.ConfigurationOptions.Instance.StrConexBd;
    return Results.Ok(cadenaConexion);
});

app.Run();
