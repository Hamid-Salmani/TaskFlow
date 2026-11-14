using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Label : BaseEntity
{
  public Guid BoardId { get; set; }
  public string Name { get; set; } = string.Empty;

  /// <summary>Hex color, e.g. #3b82f6.</summary>
  public string Color { get; set; } = "#64748b";

  public Board Board { get; set; } = null!;
}

/// <summary>Explicit join entity between Card and Label.</summary>
public class CardLabel
{
  public Guid CardId { get; set; }
  public Guid LabelId { get; set; }

  public Card Card { get; set; } = null!;
  public Label Label { get; set; } = null!;
}
