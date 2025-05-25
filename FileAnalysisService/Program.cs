using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using FileAnalysisService.DataBase;
using FileAnalysisService.Model;

var builder2 = WebApplication.CreateBuilder(args);

builder2.Services.AddEndpointsApiExplorer();
builder2.Services.AddSwaggerGen();

builder2.Services.AddDbContext<AnalysisDbContext>(o =>
    o.UseNpgsql(builder2.Configuration.GetConnectionString("PostgreSQL"), npgsqlOpts =>
        npgsqlOpts.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null
        )
    )
);

builder2.Services.AddHttpClient();

builder2.Services.AddHealthChecks()
    .AddNpgSql(builder2.Configuration.GetConnectionString("PostgreSQL") ?? string.Empty);

var app2 = builder2.Build();

app2.UseSwagger();
app2.UseSwaggerUI(); 

using (var scope = app2.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();
    
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex) {
        throw;
    }
}

static int CountParagraphs(string text) => Regex.Matches(text, "(\r?\n){2,}").Count + 1;
static int CountWords(string text) => Regex.Matches(text, "[A-Za-zА-Яа-я0-9_-]+", RegexOptions.Multiline).Count;

app2.MapPost("/v1/analysis/{fileId:guid}", async (Guid fileId, AnalysisDbContext db, IHttpClientFactory factory, IConfiguration cfg) =>
{
    var existing = await db.Analysis.FirstOrDefaultAsync(x => x.FileId == fileId);
    if (existing != null) return Results.Ok(existing);
    var client = factory.CreateClient();
    var storageResponse = await client.GetAsync($"http://storage:5000/v1/files/{fileId}");
    if (storageResponse.StatusCode == HttpStatusCode.NotFound)
        return Results.NotFound();
    storageResponse.EnsureSuccessStatusCode();
    var text = await storageResponse.Content.ReadAsStringAsync();
    var paragraphs = CountParagraphs(text);
    var words = CountWords(text);
    var chars = text.Length;
    string wcUrl;
    try
    {
        var wcResponse = await client.PostAsync(cfg["WordCloud:Url"], new StringContent(text, Encoding.UTF8, "text/plain"));
        wcUrl = await wcResponse.Content.ReadAsStringAsync();
    }
    catch
    {
        wcUrl = string.Empty;
    }
    var analysis = new AnalysisModel
    {
        Id = Guid.NewGuid(),
        FileId = fileId,
        Paragraphs = paragraphs,
        Words = words,
        Characters = chars,
        WordCloudUrl = wcUrl,
        AnalysedAt = DateTime.UtcNow
    };
    db.Analysis.Add(analysis);
    await db.SaveChangesAsync();
    return Results.Ok(analysis);
});

app2.MapGet("/v1/analysis/{fileId:guid}", async (Guid fileId, AnalysisDbContext db) =>
{
    var analysis = await db.Analysis.FirstOrDefaultAsync(x => x.FileId == fileId);
    return analysis != null ? Results.Ok(analysis) : Results.NotFound();
});

app2.MapHealthChecks("/health");

app2.Run();

namespace FileAnalysisService
{
    public partial class Program { }
}