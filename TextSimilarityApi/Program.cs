using TextSimilarityApi.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar el servicio CORS (permite cualquier origen, método y header)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()    // Permite cualquier dominio (ej: http://localhost:3000)
              .AllowAnyMethod()    // GET, POST, PUT, DELETE, etc.
              .AllowAnyHeader();   // Headers como Content-Type, Authorization, etc.
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<EmbeddingRepository>();

builder.Services.AddSingleton<AzureOcrService>();
builder.Services.AddScoped<TextExtractor>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.Run();
