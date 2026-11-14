using System.Text.Json.Serialization;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Application.Events;

/// <summary>Base of every realtime event. See NOTES.md for the wire format.</summary>
public abstract record BoardEvent(Guid BoardId, Guid ActorUserId)
{
  [JsonIgnore]
  public abstract string Name { get; }
}

public sealed record CardCreatedEvent(Guid BoardId, Guid ActorUserId, CardDto Card) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "CardCreated";
}

public sealed record CardUpdatedEvent(Guid BoardId, Guid ActorUserId, CardDto Card) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "CardUpdated";
}

public sealed record CardMovedEvent(
  Guid BoardId,
  Guid ActorUserId,
  Guid CardId,
  Guid FromColumnId,
  Guid ToColumnId,
  double Position,
  int Version,
  bool Rebalanced) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "CardMoved";
}

public sealed record CardDeletedEvent(Guid BoardId, Guid ActorUserId, Guid CardId, Guid ColumnId) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "CardDeleted";
}

public sealed record CommentAddedEvent(Guid BoardId, Guid ActorUserId, Guid CardId, CommentDto Comment) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "CommentAdded";
}

public sealed record ColumnsChangedEvent(Guid BoardId, Guid ActorUserId) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "ColumnsChanged";
}

public sealed record CommentDeletedEvent(Guid BoardId, Guid ActorUserId, Guid CardId, Guid CommentId) : BoardEvent(BoardId, ActorUserId)
{
  public override string Name => "CommentDeleted";
}
