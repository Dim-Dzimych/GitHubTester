using Microsoft.EntityFrameworkCore;
using TelegramEarningBot.Api.Data.Entities;
using Task = TelegramEarningBot.Api.Data.Entities.Task;

namespace TelegramEarningBot.Api.Data;

public class EarningBotContext : DbContext
{
    public EarningBotContext(DbContextOptions<EarningBotContext> options) : base(options)
    {
    }

    public virtual DbSet<CashById> CashByIds { get; set; } = null!;
    public virtual DbSet<PrivateGroup> PrivateGroups { get; set; } = null!;
    public virtual DbSet<RefferalCashById> RefferalCashByIds { get; set; } = null!;
    public virtual DbSet<Task> Tasks { get; set; } = null!;
    public virtual DbSet<SendingPost> SendingPosts { get; set; } = null!;
    public virtual DbSet<PostVisitor> PostVisitors { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CashById>(entity =>
        {
            entity.ToTable("CashById");

            entity.HasIndex(e => e.Id, "IX_CashById__id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("_id");
        });

        modelBuilder.Entity<PrivateGroup>(entity =>
        {
            entity.ToTable("PrivateGroup");

            entity.HasIndex(e => e.Id, "IX_PrivateGroup__id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("_id");
            entity.Property(e => e.GroupName).IsRequired();
        });

        modelBuilder.Entity<RefferalCashById>(entity =>
        {
            entity.ToTable("RefferalCashById");

            entity.HasIndex(e => e.Id, "IX_RefferalCashById__id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("_id");
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasIndex(e => e.Id, "IX_Tasks__id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("_id");
            entity.Property(e => e.Link).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.IsVisited).IsRequired();
            entity.Property(e => e.Code);
        });
        
        modelBuilder.Entity<SendingPost>(entity =>
        {
            entity.HasIndex(e => e.Id, "IX_SendingPosts_Id").IsUnique();

            entity.Property(e => e.Id);
            entity.Property(e => e.Link);
            entity.Property(e => e.Message);
        });
        
        modelBuilder.Entity<PostVisitor>(entity =>
        {
            entity.HasIndex(e => e.Id, "IX_PostVisitors_Id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId);
            entity.Property(e => e.SendingPostId);
            entity.HasOne(d => d.SendingPost)
                .WithMany(p => p.PostVisitors)
                .HasForeignKey(d => d.SendingPostId)
                .HasConstraintName("FK_PostVisitors_SendingPost");
        });
    }
}
