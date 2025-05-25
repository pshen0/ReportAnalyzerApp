using Yarp.ReverseProxy;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://storage:5000/health"), name: "storage")
    .AddUrlGroup(new Uri("http://analysis:6000/health"), name: "analysis");

var app0 = builder.Build();

app0.UseSwagger();
app0.UseSwaggerUI(); 
app0.MapHealthChecks("/health");
app0.MapReverseProxy();

app0.Run();
