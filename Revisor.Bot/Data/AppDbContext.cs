using Microsoft.EntityFrameworkCore;
using RevisorBot.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(x => x.TelegramChatId)
            .IsUnique();

        modelBuilder.Entity<Product>()
            
            .HasOne(p => p.User)
            .WithMany(u => u.Products)
            .HasForeignKey(p => p.UserId);
        
        modelBuilder.Entity<Product>()
            .Property(p => p.ExpiryDate)
            .HasColumnType("date");
        
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.UserId, p.Status });
    }
}