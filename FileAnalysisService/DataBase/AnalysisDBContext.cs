using Microsoft.EntityFrameworkCore;
using FileAnalysisService.Model;

namespace FileAnalysisService.DataBase;

public class AnalysisDbContext(DbContextOptions<AnalysisDbContext> options) : DbContext(options)
{
    public DbSet<AnalysisModel> Analysis => Set<AnalysisModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisModel>(entity =>
        {
            entity.ToTable("AnalysisModels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WordCloudUrl).HasColumnType("text");
            entity.Property(e => e.AnalysedAt).HasColumnType("timestamp with time zone");
        });
    }
}