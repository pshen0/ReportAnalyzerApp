using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using FileStoringService.DataBase;
using FileStoringService.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

builder.Services.AddDbContext<StoreDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"), npgsqlOpts =>
        npgsqlOpts.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null
        )
    )
);

var minioClient = new MinioClient()
    .WithEndpoint(configuration["Minio:Endpoint"])
    .WithCredentials(configuration["Minio:AccessKey"], configuration["Minio:SecretKey"])
    .Build();
builder.Services.AddSingleton<IMinioClient>(minioClient);

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? string.Empty);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(); 

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var retries = 5;
    for (int i = 0; i < retries; i++)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Postgres не готов, пытаюсь ещё раз через 2 секунды...");
            await Task.Delay(2000);
        }
    }
}

app.MapPost("/v1/files", async (HttpRequest req, StoreDbContext db, IMinioClient minio) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null) return Results.BadRequest();

    using var sha = SHA256.Create();
    var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var hash = Convert.ToHexString(sha.ComputeHash(ms.ToArray()));
    var existing = await db.Files.FirstOrDefaultAsync(x => x.Hash == hash);
    if (existing != null)
    {
        return Results.Ok(new { fileId = existing.Id, duplicate = true });
    }
    var id = Guid.NewGuid();
    var bucket = configuration["Minio:Bucket"];
    if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
    ms.Position = 0;
    await minio.PutObjectAsync(new PutObjectArgs()
        .WithBucket(bucket)
        .WithObject(id.ToString())
        .WithStreamData(ms)
        .WithObjectSize(ms.Length));
    var store = new FileStoreModel
    {
        Id = id,
        Name = file.FileName,
        Hash = hash,
        Location = id.ToString(),
        Size = ms.Length,
        UploadedAt = DateTime.UtcNow
    };
    db.Files.Add(store);
    await db.SaveChangesAsync();
    return Results.Ok(new { fileId = id, duplicate = false });
});

app.MapGet("/v1/files/{fileId:guid}", async (Guid fileId, StoreDbContext db, IMinioClient minio) =>
{
    var rec = await db.Files.FindAsync(fileId);
    if (rec == null) return Results.NotFound();
    var ms = new MemoryStream();
    await minio.GetObjectAsync(new GetObjectArgs()
        .WithBucket(configuration["Minio:Bucket"])
        .WithObject(rec.Location)
        .WithCallbackStream(stream => stream.CopyTo(ms)));
    ms.Position = 0;
    return Results.File(ms, "text/plain");
});

app.MapHealthChecks("/health");

app.Run();