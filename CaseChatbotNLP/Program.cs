using CaseChatbotNLP.Data;
using CaseChatbotNLP.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuración de la base de datos
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Servicios NLP
builder.Services.AddScoped<INLPService, NLPService>();
builder.Services.AddScoped<QueryExecutor>();

// Configurar el cargador de modelos como singleton
builder.Services.AddSingleton<ModelLoaderService>();
// Configurar el servicio NLP
builder.Services.AddScoped<INLPService>(provider =>
{
    var modelLoader = provider.GetRequiredService<ModelLoaderService>();
    var queryExecutor = provider.GetRequiredService<QueryExecutor>();
    return new NLPService(modelLoader.IntentModel, modelLoader.EntityModel, queryExecutor);
});

builder.Services.AddHttpClient<OpenAIService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.Run();
