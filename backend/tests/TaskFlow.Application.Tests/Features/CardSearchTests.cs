using FluentAssertions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Cards;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Tests.Features;

public sealed class CardSearchTests : IDisposable
{
  private static readonly DateTime Jan10 = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime Feb10 = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

  private readonly DbFixture _fixture = new();
  private readonly World _world;
  private readonly Card _login;
  private readonly Card _signup;
  private readonly Card _docs;
  private readonly Card _perf;

  public CardSearchTests()
  {
    _world = _fixture.CreateWorld();
    _login = Add("Fix login bug", 1000, c => c.Labeled(_world.Bug).AssignedTo(_world.Member).DueOn(Feb10));
    _signup = Add("Add signup form", 2000, c => c.Labeled(_world.Feature).AssignedTo(_world.Admin).DueOn(Jan10));
    _docs = Add("Write docs", 3000, c => c);
    _perf = Add("Speed up 100% of pages_", 4000, c => c.Labeled(_world.Bug).Labeled(_world.Feature));
    // Same title on another board must never leak into results.
    var other = _fixture.Add(new BoardBuilder(_world.Workspace).Build());
    var otherColumn = _fixture.Add(new ColumnBuilder(other).Build());
    _fixture.Add(new CardBuilder(other, otherColumn, _world.Owner).Titled("Fix login bug").Build());
  }

  public void Dispose() => _fixture.Dispose();

  private Card Add(string title, double position, Func<CardBuilder, CardBuilder> configure) =>
    _fixture.Add(configure(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Titled(title).At(position)).Build());

  private async Task<PagedResult<CardDto>> Search(
    Guid? labelId = null,
    Guid? assigneeId = null,
    bool? hasDueDate = null,
    DateTime? dueFrom = null,
    DateTime? dueTo = null,
    string? text = null,
    CardSortField sortBy = CardSortField.Position,
    SortDirection direction = SortDirection.Asc,
    int page = 1,
    int pageSize = 50)
  {
    var query = new SearchCardsQuery(_world.Board.Id, labelId, assigneeId, hasDueDate, dueFrom, dueTo, text, sortBy, direction, page, pageSize);
    var result = await new SearchCardsQueryHandler(_fixture.Db).Handle(query, default);
    return result.Value;
  }

  private static string[] Titles(PagedResult<CardDto> page) => page.Items.Select(a => a.Title).ToArray();

  [Fact]
  public async Task WithoutFilters_ReturnsEveryCardOfTheBoardOnlyInPositionOrder()
  {
    var page = await Search();

    page.TotalCount.Should().Be(4);
    Titles(page).Should().Equal("Fix login bug", "Add signup form", "Write docs", "Speed up 100% of pages_");
  }

  [Fact]
  public async Task FiltersByLabelAndAssignee()
  {
    Titles(await Search(labelId: _world.Bug.Id)).Should().Equal("Fix login bug", "Speed up 100% of pages_");
    Titles(await Search(assigneeId: _world.Admin.Id)).Should().Equal("Add signup form");
    Titles(await Search(labelId: _world.Bug.Id, assigneeId: _world.Member.Id)).Should().Equal("Fix login bug");
    (await Search(labelId: Guid.NewGuid())).Items.Should().BeEmpty();
  }

  [Fact]
  public async Task FiltersByDueDate()
  {
    Titles(await Search(hasDueDate: true)).Should().Equal("Fix login bug", "Add signup form");
    Titles(await Search(hasDueDate: false)).Should().Equal("Write docs", "Speed up 100% of pages_");
    Titles(await Search(dueFrom: Feb10)).Should().Equal("Fix login bug");
    Titles(await Search(dueTo: Jan10)).Should().Equal("Add signup form");
    Titles(await Search(dueFrom: Jan10, dueTo: Feb10)).Should().Equal("Fix login bug", "Add signup form");
  }

  [Fact]
  public async Task TextSearchMatchesTitleAndDescriptionIgnoringCase()
  {
    _docs.Description = "Explain the LOGIN flow";
    _fixture.Db.SaveChanges();

    Titles(await Search(text: "login")).Should().Equal("Fix login bug", "Write docs");
    Titles(await Search(text: "  SIGNUP ")).Should().Equal("Add signup form");
  }

  [Fact]
  public async Task TextSearchTreatsWildcardsLiterally()
  {
    Titles(await Search(text: "100%")).Should().Equal("Speed up 100% of pages_");
    Titles(await Search(text: "pages_")).Should().Equal("Speed up 100% of pages_");
    (await Search(text: "%")).TotalCount.Should().Be(1, "only the card that really contains a percent sign");
    (await Search(text: "_ogin")).Items.Should().BeEmpty("underscore is not a single-character wildcard");
  }

  [Fact]
  public async Task CombinesFiltersWithAnd()
  {
    Titles(await Search(labelId: _world.Feature.Id, hasDueDate: true, text: "signup")).Should().Equal("Add signup form");
    (await Search(labelId: _world.Feature.Id, text: "login")).Items.Should().BeEmpty();
  }

  [Fact]
  public async Task SortsByTitleDueDateAndCreation()
  {
    Titles(await Search(sortBy: CardSortField.Title)).Should().Equal("Add signup form", "Fix login bug", "Speed up 100% of pages_", "Write docs");
    Titles(await Search(sortBy: CardSortField.Title, direction: SortDirection.Desc)).First().Should().Be("Write docs");
    Titles(await Search(sortBy: CardSortField.Position, direction: SortDirection.Desc)).First().Should().Be("Speed up 100% of pages_");
  }

  [Fact]
  public async Task DueDateSortPutsCardsWithoutADueDateLastInBothDirections()
  {
    Titles(await Search(sortBy: CardSortField.DueDate)).Take(2).Should().Equal("Add signup form", "Fix login bug");
    Titles(await Search(sortBy: CardSortField.DueDate, direction: SortDirection.Desc)).Take(2).Should().Equal("Fix login bug", "Add signup form");
    foreach (var direction in new[] { SortDirection.Asc, SortDirection.Desc })
    {
      (await Search(sortBy: CardSortField.DueDate, direction: direction)).Items.TakeLast(2).Should().OnlyContain(a => a.DueDate == null);
    }
  }

  [Fact]
  public async Task Paging_ReturnsStableDisjointPagesAndTotals()
  {
    var first = await Search(page: 1, pageSize: 3);
    var second = await Search(page: 2, pageSize: 3);
    var beyond = await Search(page: 3, pageSize: 3);

    first.Items.Should().HaveCount(3);
    second.Items.Should().HaveCount(1);
    beyond.Items.Should().BeEmpty();
    new[] { first, second, beyond }.Should().OnlyContain(a => a.TotalCount == 4 && a.PageSize == 3);
    first.TotalPages.Should().Be(2);
    first.Items.Concat(second.Items).Select(a => a.Id).Should().OnlyHaveUniqueItems();
  }

  [Fact]
  public async Task Paging_IsStableWhenTheSortKeyHasDuplicates()
  {
    for (var i = 0; i < 6; i++)
    {
      Add("Same title", 5000, c => c);
    }

    var ids = new List<Guid>();
    for (var page = 1; page <= 5; page++)
    {
      ids.AddRange((await Search(sortBy: CardSortField.Title, page: page, pageSize: 2)).Items.Select(a => a.Id));
    }

    ids.Should().OnlyHaveUniqueItems().And.HaveCount(10);
  }

  [Fact]
  public async Task DoesNotReturnDeletedCards()
  {
    _login.SoftDelete(DateTime.UtcNow);
    _fixture.Db.SaveChanges();

    (await Search()).TotalCount.Should().Be(3);
  }

  [Fact]
  public void Validator_EnforcesPagingAndDateRangeLimits()
  {
    var validator = new SearchCardsQueryValidator();
    SearchCardsQuery Query(int page = 1, int size = 50, DateTime? from = null, DateTime? to = null, string? text = null) =>
      new(Guid.NewGuid(), null, null, null, from, to, text, CardSortField.Position, SortDirection.Asc, page, size);

    validator.Validate(Query()).IsValid.Should().BeTrue();
    validator.Validate(Query(page: 0)).IsValid.Should().BeFalse();
    validator.Validate(Query(size: 0)).IsValid.Should().BeFalse();
    validator.Validate(Query(size: 201)).IsValid.Should().BeFalse();
    validator.Validate(Query(size: 200)).IsValid.Should().BeTrue();
    validator.Validate(Query(from: Feb10, to: Jan10)).IsValid.Should().BeFalse();
    validator.Validate(Query(from: Jan10, to: Jan10)).IsValid.Should().BeTrue();
    validator.Validate(Query(text: new string('x', 201))).IsValid.Should().BeFalse();
  }
}
