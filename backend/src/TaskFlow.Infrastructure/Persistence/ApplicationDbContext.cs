using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, TimeProvider timeProvider)
  : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
  public DbSet<Workspace> Workspaces => Set<Workspace>();
  public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
  public DbSet<Board> Boards => Set<Board>();
  public DbSet<Column> Columns => Set<Column>();
  public DbSet<Card> Cards => Set<Card>();
  public DbSet<CardAssignee> CardAssignees => Set<CardAssignee>();
  public DbSet<CardLabel> CardLabels => Set<CardLabel>();
  public DbSet<Label> Labels => Set<Label>();
  public DbSet<CardComment> CardComments => Set<CardComment>();
  public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
  public DbSet<Notification> Notifications => Set<Notification>();
  public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
    builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    builder.Entity<User>().ToTable("Users");

    // Soft delete: every BaseEntity is hidden once IsDeleted is set.
    foreach (var entityType in builder.Model.GetEntityTypes().Where(a => typeof(BaseEntity).IsAssignableFrom(a.ClrType)))
    {
      var parameter = Expression.Parameter(entityType.ClrType, "a");
      var isDeleted = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
      var filter = Expression.Lambda(Expression.Equal(isDeleted, Expression.Constant(false)), parameter);
      builder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }
  }

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    // SQLite drops DateTime.Kind; without this the API would serialize UTC values without the trailing "Z".
    configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
  }

  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    var now = timeProvider.GetUtcNow().UtcDateTime;
    foreach (var entry in ChangeTracker.Entries<BaseEntity>())
    {
      if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
      {
        entry.Entity.CreatedAt = now;
        continue;
      }

      if (entry.State == EntityState.Modified)
      {
        entry.Entity.UpdatedAt = now;
      }
    }

    return base.SaveChangesAsync(cancellationToken);
  }
}
