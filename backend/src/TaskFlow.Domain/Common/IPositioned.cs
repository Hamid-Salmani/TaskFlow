namespace TaskFlow.Domain.Common;

/// <summary>An item that lives in an ordered container (cards in a column, columns in a board).</summary>
public interface IPositioned
{
  Guid Id { get; }
  double Position { get; set; }
}
