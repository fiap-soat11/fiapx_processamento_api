using Microsoft.EntityFrameworkCore;
using fiapx_processamento_api.Worker.Domain.Entities;

namespace fiapx_processamento_api.Worker.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<VideoProcessing> VideoProcessings => Set<VideoProcessing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255).HasColumnName("name");
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255).HasColumnName("email");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255).HasColumnName("password_hash");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("updated_at");
            entity.ToTable("users");
        });

        modelBuilder.Entity<VideoProcessing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255).HasColumnName("original_file_name");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.S3InputPath).IsRequired().HasMaxLength(2048).HasColumnName("s3_input_path");
            entity.Property(e => e.S3OutputPath).HasMaxLength(2048).HasColumnName("s3_output_path");
            entity.Property(e => e.FailureReason).HasColumnType("text").HasColumnName("failure_reason");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable("video_processings");
        });
    }
}
