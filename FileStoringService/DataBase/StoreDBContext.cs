using Microsoft.EntityFrameworkCore;
using FileStoringService.Model;

namespace FileStoringService.DataBase;

public class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<FileStoreModel> Files => Set<FileStoreModel>();
}