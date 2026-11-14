namespace TaskFlow.Domain.Enums;

/// <summary>Numeric values are ordered so that a higher value always means more privileges.</summary>
public enum WorkspaceRole
{
  Member = 1,
  Admin = 2,
  Owner = 3
}

public enum NotificationType
{
  Assigned = 1,
  Mentioned = 2
}

public enum ActivityType
{
  BoardCreated = 1,
  ColumnCreated = 2,
  ColumnRenamed = 3,
  ColumnMoved = 4,
  ColumnDeleted = 5,
  CardCreated = 6,
  CardUpdated = 7,
  CardMoved = 8,
  CardDeleted = 9,
  CardAssigned = 10,
  CardLabelsChanged = 11,
  CommentAdded = 12
}
