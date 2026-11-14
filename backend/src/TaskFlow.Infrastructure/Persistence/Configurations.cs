using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
  public void Configure(EntityTypeBuilder<User> builder)
  {
    builder.Property(a => a.DisplayName).HasMaxLength(100).IsRequired();
    builder.Property(a => a.Handle).HasMaxLength(50).IsRequired();
    builder.HasIndex(a => a.Handle).IsUnique();
  }
}

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
  public void Configure(EntityTypeBuilder<Workspace> builder)
  {
    builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
    builder.Property(a => a.Description).HasMaxLength(500);
  }
}

public sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
  public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
  {
    builder.HasKey(a => new { a.WorkspaceId, a.UserId });
    builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(20);
    builder.HasIndex(a => a.UserId);
    builder.HasOne(a => a.Workspace).WithMany(a => a.Members).HasForeignKey(a => a.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
  public void Configure(EntityTypeBuilder<Board> builder)
  {
    builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
    builder.Property(a => a.Description).HasMaxLength(1000);
    builder.HasIndex(a => a.WorkspaceId);
    builder.HasOne(a => a.Workspace).WithMany(a => a.Boards).HasForeignKey(a => a.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class ColumnConfiguration : IEntityTypeConfiguration<Column>
{
  public void Configure(EntityTypeBuilder<Column> builder)
  {
    builder.Property(a => a.Title).HasMaxLength(100).IsRequired();
    builder.HasIndex(a => new { a.BoardId, a.Position });
    builder.HasOne(a => a.Board).WithMany(a => a.Columns).HasForeignKey(a => a.BoardId).OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
  public void Configure(EntityTypeBuilder<Card> builder)
  {
    builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
    builder.Property(a => a.Description).HasMaxLength(10000);
    builder.Property(a => a.Version).IsConcurrencyToken();
    builder.HasIndex(a => new { a.ColumnId, a.Position });
    builder.HasIndex(a => a.BoardId);
    builder.HasOne(a => a.Column).WithMany(a => a.Cards).HasForeignKey(a => a.ColumnId).OnDelete(DeleteBehavior.Cascade);
    // Restrict avoids multiple cascade paths (Board -> Column -> Card and Board -> Card) on SQL Server.
    builder.HasOne(a => a.Board).WithMany().HasForeignKey(a => a.BoardId).OnDelete(DeleteBehavior.Restrict);
  }
}

public sealed class CardAssigneeConfiguration : IEntityTypeConfiguration<CardAssignee>
{
  public void Configure(EntityTypeBuilder<CardAssignee> builder)
  {
    builder.HasKey(a => new { a.CardId, a.UserId });
    builder.HasIndex(a => a.UserId);
    builder.HasOne(a => a.Card).WithMany(a => a.Assignees).HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
  public void Configure(EntityTypeBuilder<Label> builder)
  {
    builder.Property(a => a.Name).HasMaxLength(50).IsRequired();
    builder.Property(a => a.Color).HasMaxLength(9).IsRequired();
    builder.HasIndex(a => a.BoardId);
    builder.HasOne(a => a.Board).WithMany(a => a.Labels).HasForeignKey(a => a.BoardId).OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class CardLabelConfiguration : IEntityTypeConfiguration<CardLabel>
{
  public void Configure(EntityTypeBuilder<CardLabel> builder)
  {
    builder.HasKey(a => new { a.CardId, a.LabelId });
    builder.HasIndex(a => a.LabelId);
    builder.HasOne(a => a.Card).WithMany(a => a.Labels).HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne(a => a.Label).WithMany().HasForeignKey(a => a.LabelId).OnDelete(DeleteBehavior.Restrict);
  }
}

public sealed class CardCommentConfiguration : IEntityTypeConfiguration<CardComment>
{
  public void Configure(EntityTypeBuilder<CardComment> builder)
  {
    builder.Property(a => a.Body).HasMaxLength(4000).IsRequired();
    builder.HasIndex(a => new { a.CardId, a.CreatedAt });
    builder.HasOne(a => a.Card).WithMany(a => a.Comments).HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne(a => a.Author).WithMany().HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
  }
}

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
  public void Configure(EntityTypeBuilder<ActivityLog> builder)
  {
    builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
    builder.Property(a => a.Message).HasMaxLength(500).IsRequired();
    builder.HasIndex(a => new { a.BoardId, a.CreatedAt });
    builder.HasIndex(a => new { a.CardId, a.CreatedAt });
    builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
  }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
  public void Configure(EntityTypeBuilder<Notification> builder)
  {
    builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
    builder.Property(a => a.Message).HasMaxLength(500).IsRequired();
    builder.HasIndex(a => new { a.UserId, a.IsRead, a.CreatedAt });
    builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
  public void Configure(EntityTypeBuilder<RefreshToken> builder)
  {
    builder.Property(a => a.TokenHash).HasMaxLength(64).IsRequired();
    builder.Property(a => a.ReplacedByTokenHash).HasMaxLength(64);
    builder.HasIndex(a => a.TokenHash).IsUnique();
    builder.HasIndex(a => a.FamilyId);
    builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
  }
}
