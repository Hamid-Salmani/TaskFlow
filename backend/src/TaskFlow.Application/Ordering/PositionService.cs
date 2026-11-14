using TaskFlow.Application.Common;
using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Ordering;

public readonly record struct PositionResult(double Position, bool RebalanceRequired);

/// <summary>Pure fractional-index calculations. No I/O, no state, trivially testable.</summary>
public interface IPositionService
{
  /// <param name="after">Position of the neighbour that will be directly before the item (null = start).</param>
  /// <param name="before">Position of the neighbour that will be directly after the item (null = end).</param>
  PositionResult Between(double? after, double? before);

  /// <summary>Evenly spaced positions for <paramref name="count"/> items in their current order.</summary>
  IReadOnlyList<double> Rebalance(int count);
}

public sealed class PositionService : IPositionService
{
  public const double Step = 1024d;
  public const double MinGap = 1e-6;

  public PositionResult Between(double? after, double? before)
  {
    if (after is null && before is null)
    {
      return new PositionResult(Step, false);
    }

    if (before is null)
    {
      return new PositionResult(after!.Value + Step, false);
    }

    if (after is null)
    {
      return new PositionResult(before.Value - Step, false);
    }

    if (after.Value >= before.Value)
    {
      throw new ArgumentException("The 'after' neighbour must have a smaller position than the 'before' neighbour.");
    }

    var gap = before.Value - after.Value;
    var middle = after.Value + gap / 2;
    return new PositionResult(middle, gap < MinGap);
  }

  public IReadOnlyList<double> Rebalance(int count) =>
    Enumerable.Range(1, count).Select(a => a * Step).ToList();
}

public sealed record PlacementResult(double Position, bool Rebalanced);

public static class PositionPlanner
{
  /// <summary>
  /// Translates "put it after X / before Y" into a position. <paramref name="siblings"/> are the tracked entities of the
  /// target container, ordered by (position, id) and <b>excluding</b> the item that is being placed. When the gap is exhausted
  /// the siblings are renumbered in place (the caller saves them together with the moved item).
  /// </summary>
  public static Result<PlacementResult> Plan<T>(
    IPositionService positions,
    IReadOnlyList<T> siblings,
    Guid? afterId,
    Guid? beforeId) where T : class, IPositioned
  {
    T? after = null;
    T? before = null;

    if (afterId is null && beforeId is null)
    {
      after = siblings.Count > 0 ? siblings[^1] : null;
    }
    else
    {
      var afterIndex = -1;
      var beforeIndex = -1;

      if (afterId is not null)
      {
        afterIndex = IndexOf(siblings, afterId.Value);
        if (afterIndex < 0)
        {
          return Error.Validation("position.neighbor_not_found", "The 'afterId' item is not in the target container.");
        }
      }

      if (beforeId is not null)
      {
        beforeIndex = IndexOf(siblings, beforeId.Value);
        if (beforeIndex < 0)
        {
          return Error.Validation("position.neighbor_not_found", "The 'beforeId' item is not in the target container.");
        }
      }

      if (afterIndex >= 0 && beforeIndex >= 0 && afterIndex >= beforeIndex)
      {
        return Error.Validation("position.neighbors_out_of_order", "'afterId' must come before 'beforeId'.");
      }

      after = afterIndex >= 0 ? siblings[afterIndex] : beforeIndex > 0 ? siblings[beforeIndex - 1] : null;
      before = beforeIndex >= 0 ? siblings[beforeIndex] : afterIndex >= 0 && afterIndex < siblings.Count - 1 ? siblings[afterIndex + 1] : null;
    }

    // Two concurrent appends can leave neighbours with equal positions (nothing serializes them across requests).
    // That is not an error: renumber the container so the order the clients see (position, then id) becomes strict.
    var neighboursCollide = after is not null && before is not null && after.Position >= before.Position;
    var result = neighboursCollide ? new PositionResult(0, true) : positions.Between(after?.Position, before?.Position);
    if (result.RebalanceRequired == false)
    {
      return new PlacementResult(result.Position, false);
    }

    var fresh = positions.Rebalance(siblings.Count);
    for (var i = 0; i < siblings.Count; i++)
    {
      siblings[i].Position = fresh[i];
    }

    result = positions.Between(after?.Position, before?.Position);
    return new PlacementResult(result.Position, true);
  }

  private static int IndexOf<T>(IReadOnlyList<T> siblings, Guid id) where T : IPositioned
  {
    for (var i = 0; i < siblings.Count; i++)
    {
      if (siblings[i].Id == id)
      {
        return i;
      }
    }

    return -1;
  }
}
