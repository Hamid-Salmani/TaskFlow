using FluentAssertions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Ordering;
using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Tests.Ordering;

public sealed class PositionServiceTests
{
  private readonly PositionService _service = new();

  [Fact]
  public void Between_WithNoNeighbours_StartsAtStep()
  {
    var result = _service.Between(null, null);

    result.Position.Should().Be(PositionService.Step);
    result.RebalanceRequired.Should().BeFalse();
  }

  [Fact]
  public void Between_AtTheEnd_AddsStepToLastPosition()
  {
    _service.Between(3072, null).Position.Should().Be(3072 + PositionService.Step);
  }

  [Fact]
  public void Between_AtTheStart_SubtractsStepFromFirstPosition()
  {
    _service.Between(null, 1024).Position.Should().Be(1024 - PositionService.Step);
  }

  [Fact]
  public void Between_AtTheStart_CanGoNegativeAndKeepsOrder()
  {
    var first = _service.Between(null, 0.5).Position;
    var second = _service.Between(null, first).Position;

    second.Should().BeLessThan(first);
    first.Should().BeLessThan(0.5);
  }

  [Fact]
  public void Between_TwoNeighbours_ReturnsMidpoint()
  {
    var result = _service.Between(1024, 2048);

    result.Position.Should().Be(1536);
    result.RebalanceRequired.Should().BeFalse();
  }

  [Fact]
  public void Between_RepeatedInsertsAtTheSameSpot_StayStrictlyOrdered()
  {
    double lower = 1024, upper = 2048;
    for (var i = 0; i < 20; i++)
    {
      var next = _service.Between(lower, upper);
      next.Position.Should().BeGreaterThan(lower).And.BeLessThan(upper);
      upper = next.Position;
    }
  }

  [Fact]
  public void Between_WhenGapIsExhausted_RequestsRebalance()
  {
    var result = _service.Between(1, 1 + PositionService.MinGap / 2);

    result.RebalanceRequired.Should().BeTrue();
  }

  [Fact]
  public void Between_WhenGapIsJustLargeEnough_DoesNotRebalance()
  {
    _service.Between(1, 1 + PositionService.MinGap * 2).RebalanceRequired.Should().BeFalse();
  }

  [Theory]
  [InlineData(5, 5)]
  [InlineData(6, 5)]
  public void Between_WithNeighboursInWrongOrder_Throws(double after, double before)
  {
    var act = () => _service.Between(after, before);

    act.Should().Throw<ArgumentException>();
  }

  [Fact]
  public void Rebalance_ReturnsEvenlySpacedPositions()
  {
    _service.Rebalance(3).Should().Equal(1024, 2048, 3072);
  }

  [Fact]
  public void Rebalance_WithNoItems_ReturnsEmpty()
  {
    _service.Rebalance(0).Should().BeEmpty();
  }

  // ---------- PositionPlanner (neighbour ids -> position) ----------

  private sealed class Item(double position) : IPositioned
  {
    public Guid Id { get; } = Guid.NewGuid();
    public double Position { get; set; } = position;
  }

  private static List<Item> Items(params double[] positions) => positions.Select(a => new Item(a)).ToList();

  [Fact]
  public void Plan_WithoutNeighbours_AppendsAfterTheLastSibling()
  {
    var siblings = Items(1024, 2048);

    var result = PositionPlanner.Plan(_service, siblings, null, null);

    result.Value.Position.Should().Be(3072);
  }

  [Fact]
  public void Plan_IntoAnEmptyContainer_UsesTheFirstSlot()
  {
    PositionPlanner.Plan(_service, Items(), null, null).Value.Position.Should().Be(PositionService.Step);
  }

  [Fact]
  public void Plan_WithOnlyBeforeId_OfTheFirstItem_PlacesAtTheStart()
  {
    var siblings = Items(1024, 2048);

    var result = PositionPlanner.Plan(_service, siblings, null, siblings[0].Id);

    result.Value.Position.Should().Be(0);
  }

  [Fact]
  public void Plan_WithOnlyBeforeId_PlacesDirectlyBeforeThatItem()
  {
    var siblings = Items(1024, 2048, 3072);

    var result = PositionPlanner.Plan(_service, siblings, null, siblings[2].Id);

    result.Value.Position.Should().Be(2560);
  }

  [Fact]
  public void Plan_WithOnlyAfterId_PlacesDirectlyAfterThatItem()
  {
    var siblings = Items(1024, 2048, 3072);

    var result = PositionPlanner.Plan(_service, siblings, siblings[0].Id, null);

    result.Value.Position.Should().Be(1536);
  }

  [Fact]
  public void Plan_WithOnlyAfterId_OfTheLastItem_PlacesAtTheEnd()
  {
    var siblings = Items(1024, 2048);

    PositionPlanner.Plan(_service, siblings, siblings[1].Id, null).Value.Position.Should().Be(3072);
  }

  [Fact]
  public void Plan_WithBothNeighbours_ReturnsTheMidpoint()
  {
    var siblings = Items(1024, 2048, 4096);

    var result = PositionPlanner.Plan(_service, siblings, siblings[1].Id, siblings[2].Id);

    result.Value.Position.Should().Be(3072);
    result.Value.Rebalanced.Should().BeFalse();
  }

  [Fact]
  public void Plan_WithUnknownNeighbour_ReturnsValidationError()
  {
    var result = PositionPlanner.Plan(_service, Items(1024), Guid.NewGuid(), null);

    result.Error!.Type.Should().Be(ErrorType.Validation);
    result.Error.Code.Should().Be("position.neighbor_not_found");
  }

  [Fact]
  public void Plan_WithUnknownBeforeNeighbour_ReturnsValidationError()
  {
    PositionPlanner.Plan(_service, Items(1024), null, Guid.NewGuid()).Error!.Code.Should().Be("position.neighbor_not_found");
  }

  [Fact]
  public void Plan_WithNeighboursInWrongOrder_ReturnsValidationError()
  {
    var siblings = Items(1024, 2048);

    var result = PositionPlanner.Plan(_service, siblings, siblings[1].Id, siblings[0].Id);

    result.Error!.Code.Should().Be("position.neighbors_out_of_order");
  }

  [Fact]
  public void Plan_WhenGapIsExhausted_RenumbersSiblingsAndStillReturnsAValidSlot()
  {
    var siblings = Items(1024, 1024 + 1e-9, 5000);

    var result = PositionPlanner.Plan(_service, siblings, siblings[0].Id, siblings[1].Id);

    result.Value.Rebalanced.Should().BeTrue();
    siblings.Select(a => a.Position).Should().Equal(1024, 2048, 3072);
    result.Value.Position.Should().BeGreaterThan(siblings[0].Position).And.BeLessThan(siblings[1].Position);
  }

  [Fact]
  public void Plan_BetweenNeighboursWithEqualPositions_RebalancesInsteadOfThrowing()
  {
    // Concurrent appends can persist two items at the same position.
    var siblings = Items(1024, 2048, 2048, 4096);

    var result = PositionPlanner.Plan(_service, siblings, siblings[1].Id, siblings[2].Id);

    result.IsSuccess.Should().BeTrue();
    result.Value.Rebalanced.Should().BeTrue();
    siblings.Select(a => a.Position).Should().Equal(1024, 2048, 3072, 4096);
    result.Value.Position.Should().BeGreaterThan(2048).And.BeLessThan(3072);
  }

  [Fact]
  public void Plan_AfterAnItemWhoseSuccessorSharesItsPosition_IsRepaired()
  {
    var siblings = Items(1000, 1000);

    var result = PositionPlanner.Plan(_service, siblings, siblings[0].Id, null);

    result.Value.Rebalanced.Should().BeTrue();
    siblings.Select(a => a.Position).Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
    result.Value.Position.Should().BeGreaterThan(siblings[0].Position).And.BeLessThan(siblings[1].Position);
  }
}
