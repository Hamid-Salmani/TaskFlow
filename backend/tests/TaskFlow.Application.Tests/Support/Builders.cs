using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Support;

/// <summary>Test data builders: sensible defaults, override only what the test cares about.</summary>
public sealed class UserBuilder(string displayName = "Test User")
{
  private static int _counter;

  public User Build()
  {
    var number = Interlocked.Increment(ref _counter);
    var handle = $"{displayName.ToLowerInvariant().Replace(' ', '.')}{number}";
    return new User
    {
      Id = Guid.NewGuid(),
      UserName = $"{handle}@test.dev",
      Email = $"{handle}@test.dev",
      NormalizedUserName = $"{handle}@test.dev".ToUpperInvariant(),
      NormalizedEmail = $"{handle}@test.dev".ToUpperInvariant(),
      DisplayName = displayName,
      Handle = handle,
      CreatedAt = DateTime.UtcNow
    };
  }
}

public sealed class WorkspaceBuilder
{
  private readonly Workspace _workspace = new() { Name = "Acme" };

  public WorkspaceBuilder WithName(string name)
  {
    _workspace.Name = name;
    return this;
  }

  public WorkspaceBuilder WithMember(User user, WorkspaceRole role)
  {
    _workspace.Members.Add(new WorkspaceMember { UserId = user.Id, Role = role, JoinedAt = DateTime.UtcNow });
    return this;
  }

  public Workspace Build() => _workspace;
}

public sealed class BoardBuilder(Workspace workspace)
{
  private readonly Board _board = new() { WorkspaceId = workspace.Id, Name = "Roadmap" };

  public BoardBuilder Archived()
  {
    _board.IsArchived = true;
    return this;
  }

  public Board Build() => _board;
}

public sealed class ColumnBuilder(Board board, string title = "Column")
{
  private double _position = 1024;

  public ColumnBuilder At(double position)
  {
    _position = position;
    return this;
  }

  public Column Build() => new() { BoardId = board.Id, Title = title, Position = _position };
}

public sealed class CardBuilder(Board board, Column column, User creator)
{
  private readonly Card _card = new()
  {
    BoardId = board.Id,
    ColumnId = column.Id,
    Title = "Card",
    Position = 1024,
    CreatedById = creator.Id
  };

  public CardBuilder Titled(string title)
  {
    _card.Title = title;
    return this;
  }

  public CardBuilder At(double position)
  {
    _card.Position = position;
    return this;
  }

  public CardBuilder DueOn(DateTime? dueDate)
  {
    _card.DueDate = dueDate;
    return this;
  }

  public CardBuilder AssignedTo(User user)
  {
    _card.Assignees.Add(new CardAssignee { UserId = user.Id, AssignedAt = DateTime.UtcNow });
    return this;
  }

  public CardBuilder Labeled(Label label)
  {
    _card.Labels.Add(new CardLabel { LabelId = label.Id });
    return this;
  }

  public CardBuilder WithVersion(int version)
  {
    _card.Version = version;
    return this;
  }

  public Card Build() => _card;
}
