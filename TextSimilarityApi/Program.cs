using TextSimilarityApi.Services;

var builder = WebApplication.CreateBuilder(args);

var voskModelPath = builder.Configuration["Vosk:ModelPath"];
var ffmpegPath = builder.Configuration["FFmpeg:Path"];

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
builder.Services.AddSingleton<InMemoryDocumentStore>();

builder.Services.AddSingleton<TextExtractor>();
builder.Services.AddSingleton<VideoProcessor>(sp =>
{
    var embeddingService = sp.GetRequiredService<EmbeddingService>();
    var textExtractor = sp.GetRequiredService<TextExtractor>();

    return new VideoProcessor(embeddingService, textExtractor, ffmpegPath, voskModelPath);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.Run();
