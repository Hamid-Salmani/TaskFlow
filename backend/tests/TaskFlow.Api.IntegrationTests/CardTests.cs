using System.Net;
using FluentAssertions;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.IntegrationTests.Support;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.IntegrationTests;

public sealed class CardTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
  private const string Api = "/api/v1";

  private sealed record Setup(TestUser Owner, TestUser Member, BoardDetailDto Board, ColumnDto Todo, ColumnDto Doing, ColumnDto Done);

  private async Task<Setup> CreateSetupAsync()
  {
    var owner = await factory.RegisterUserAsync("Owner");
    var member = await factory.RegisterUserAsync("Member");
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);
    var board = await owner.CreateBoardAsync(workspace.Id);
    return new Setup(owner, member, board, board.Columns[0], board.Columns[1], board.Columns[2]);
  }

  private static async Task<List<CardDto>> CardsOfAsync(TestUser user, Guid boardId, Guid columnId, string query = "")
  {
    var page = await (await user.Client.GetAsync($"{Api}/boards/{boardId}/cards?pageSize=200{query}")).ReadAsAsync<PagedResult<CardDto>>();
    return page.Items.Where(a => a.ColumnId == columnId).OrderBy(a => a.Position).ToList();
  }

  private static Task<HttpResponseMessage> MoveAsync(TestUser user, CardDto card, Guid columnId, Guid? after = null, Guid? before = null, int? version = null) =>
    user.Client.PutJsonAsync($"{Api}/cards/{card.Id}/move", new MoveCardRequest(version ?? card.Version, columnId, after, before));

  [Fact]
  public async Task CreateCard_AppendsToTheColumnAndCardsKeepTheirOrder()
  {
    var s = await CreateSetupAsync();

    var first = await s.Member.CreateCardAsync(s.Todo.Id, "First");
    var second = await s.Member.CreateCardAsync(s.Todo.Id, "Second");

    first.Version.Should().Be(1);
    second.Position.Should().BeGreaterThan(first.Position);
    (await CardsOfAsync(s.Owner, s.Board.Id, s.Todo.Id)).Select(a => a.Title).Should().Equal("First", "Second");
  }

  [Fact]
  public async Task CreateCard_ValidatesTheTitle()
  {
    var s = await CreateSetupAsync();

    var response = await s.Member.Client.PostJsonAsync($"{Api}/columns/{s.Todo.Id}/cards", new CreateCardRequest("", null, null));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    (await response.ReadProblemAsync()).Errors.Should().ContainKey("title");
  }

  [Fact]
  public async Task MoveCard_ReordersWithinAColumnUsingNeighbourIds()
  {
    var s = await CreateSetupAsync();
    var a = await s.Owner.CreateCardAsync(s.Todo.Id, "A");
    var b = await s.Owner.CreateCardAsync(s.Todo.Id, "B");
    var c = await s.Owner.CreateCardAsync(s.Todo.Id, "C");

    var response = await MoveAsync(s.Owner, c, s.Todo.Id, after: a.Id, before: b.Id);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.ReadAsAsync<MoveCardResultDto>();
    result.Should().BeEquivalentTo(new { Id = c.Id, ColumnId = s.Todo.Id, Version = 2, Rebalanced = false });
    result.Position.Should().BeInRange(a.Position, b.Position);
    (await CardsOfAsync(s.Owner, s.Board.Id, s.Todo.Id)).Select(x => x.Title).Should().Equal("A", "C", "B");
  }

  [Fact]
  public async Task MoveCard_AcrossColumnsToTopMiddleAndEnd()
  {
    var s = await CreateSetupAsync();
    var x = await s.Owner.CreateCardAsync(s.Doing.Id, "X");
    var y = await s.Owner.CreateCardAsync(s.Doing.Id, "Y");
    var m1 = await s.Owner.CreateCardAsync(s.Todo.Id, "M1");
    var m2 = await s.Owner.CreateCardAsync(s.Todo.Id, "M2");
    var m3 = await s.Owner.CreateCardAsync(s.Todo.Id, "M3");

    (await MoveAsync(s.Owner, m1, s.Doing.Id, before: x.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    (await MoveAsync(s.Owner, m2, s.Doing.Id, after: x.Id, before: y.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    (await MoveAsync(s.Owner, m3, s.Doing.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

    (await CardsOfAsync(s.Owner, s.Board.Id, s.Doing.Id)).Select(a => a.Title).Should().Equal("M1", "X", "M2", "Y", "M3");
    (await CardsOfAsync(s.Owner, s.Board.Id, s.Todo.Id)).Should().BeEmpty();
  }

  [Fact]
  public async Task MoveCard_RepeatedInsertionsAtTheSameSpotRebalanceAndKeepOrder()
  {
    var s = await CreateSetupAsync();
    var anchorTop = await s.Owner.CreateCardAsync(s.Doing.Id, "Top");
    var anchorBottom = await s.Owner.CreateCardAsync(s.Doing.Id, "Bottom");
    var inserted = new List<string>();
    var rebalanced = false;

    // Always insert right below "Top": every insertion halves the gap, so the 1024 gap is exhausted after ~30 moves.
    for (var i = 0; i < 40; i++)
    {
      var card = await s.Owner.CreateCardAsync(s.Todo.Id, $"Card {i}");
      var response = await MoveAsync(s.Owner, card, s.Doing.Id, after: anchorTop.Id);
      response.StatusCode.Should().Be(HttpStatusCode.OK, $"move {i}");
      rebalanced |= (await response.ReadAsAsync<MoveCardResultDto>()).Rebalanced;
      inserted.Insert(0, $"Card {i}");
    }

    rebalanced.Should().BeTrue("the gap below 'Top' must have been exhausted at some point");
    var column = await CardsOfAsync(s.Owner, s.Board.Id, s.Doing.Id);
    column.Select(a => a.Title).Should().Equal(new[] { "Top" }.Concat(inserted).Append("Bottom"));
    column.Select(a => a.Position).Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
    anchorBottom.Title.Should().Be("Bottom");
  }

  [Fact]
  public async Task MoveCard_WithAStaleVersionReturns409AndLeavesTheCardAlone()
  {
    var s = await CreateSetupAsync();
    var card = await s.Owner.CreateCardAsync(s.Todo.Id, "Card");
    (await MoveAsync(s.Owner, card, s.Doing.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

    var stale = await MoveAsync(s.Member, card, s.Done.Id);

    stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    stale.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    var problem = await stale.ReadProblemAsync();
    problem.Should().BeEquivalentTo(new { Status = 409, Code = "card.version_conflict" });
    (await CardsOfAsync(s.Owner, s.Board.Id, s.Doing.Id)).Should().ContainSingle(a => a.Id == card.Id);
  }

  [Fact]
  public async Task UpdateCard_ChangesFieldsAndTheNextEditNeedsTheNewVersion()
  {
    var s = await CreateSetupAsync();
    var card = await s.Owner.CreateCardAsync(s.Todo.Id);
    var due = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    var first = await s.Owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}", new UpdateCardRequest(1, "Renamed", "Body", due));
    var again = await s.Owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}", new UpdateCardRequest(1, "Again", null, null));

    first.StatusCode.Should().Be(HttpStatusCode.OK);
    (await first.ReadAsAsync<CardDto>()).Should().BeEquivalentTo(new { Title = "Renamed", Description = "Body", DueDate = due, Version = 2 });
    again.StatusCode.Should().Be(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task MoveCard_CannotTargetAColumnOfAnotherBoard()
  {
    var s = await CreateSetupAsync();
    var otherWorkspace = await s.Owner.CreateWorkspaceAsync("Other");
    var otherBoard = await s.Owner.CreateBoardAsync(otherWorkspace.Id);
    var card = await s.Owner.CreateCardAsync(s.Todo.Id);

    var response = await MoveAsync(s.Owner, card, otherBoard.Columns[0].Id);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    (await response.ReadProblemAsync()).Code.Should().Be("card.target_column_invalid");
  }

  [Fact]
  public async Task MoveCard_ValidatesNeighbours()
  {
    var s = await CreateSetupAsync();
    var a = await s.Owner.CreateCardAsync(s.Todo.Id, "A");
    var b = await s.Owner.CreateCardAsync(s.Todo.Id, "B");
    var elsewhere = await s.Owner.CreateCardAsync(s.Doing.Id, "Elsewhere");
    var moving = await s.Owner.CreateCardAsync(s.Doing.Id, "Moving");

    var notInColumn = await MoveAsync(s.Owner, moving, s.Todo.Id, after: elsewhere.Id);
    var wrongOrder = await MoveAsync(s.Owner, moving, s.Todo.Id, after: b.Id, before: a.Id);

    (await notInColumn.ReadProblemAsync()).Code.Should().Be("position.neighbor_not_found");
    (await wrongOrder.ReadProblemAsync()).Code.Should().Be("position.neighbors_out_of_order");
  }

  [Fact]
  public async Task NonMembers_CannotSeeOrTouchCards_AndReceive404()
  {
    var s = await CreateSetupAsync();
    var outsider = await factory.RegisterUserAsync("Outsider");
    var card = await s.Owner.CreateCardAsync(s.Todo.Id);

    var responses = new[]
    {
      await outsider.Client.GetAsync($"{Api}/cards/{card.Id}"),
      await outsider.Client.GetAsync($"{Api}/boards/{s.Board.Id}/cards"),
      await outsider.Client.PutJsonAsync($"{Api}/cards/{card.Id}", new UpdateCardRequest(1, "x", null, null)),
      await MoveAsync(outsider, card, s.Doing.Id),
      await outsider.Client.DeleteAsync($"{Api}/cards/{card.Id}"),
      await outsider.Client.PostJsonAsync($"{Api}/columns/{s.Todo.Id}/cards", new CreateCardRequest("x", null, null)),
      await outsider.Client.PutJsonAsync($"{Api}/cards/{card.Id}/labels", new SetCardLabelsRequest(1, [])),
      await outsider.Client.PutJsonAsync($"{Api}/cards/{card.Id}/assignees", new SetCardAssigneesRequest(1, [outsider.Id]))
    };

    responses.Should().OnlyContain(a => a.StatusCode == HttpStatusCode.NotFound);
    (await s.Owner.Client.GetAsync($"{Api}/cards/{card.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task DeleteCard_RemovesItFromTheBoard()
  {
    var s = await CreateSetupAsync();
    var card = await s.Member.CreateCardAsync(s.Todo.Id);

    (await s.Member.Client.DeleteAsync($"{Api}/cards/{card.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

    (await s.Owner.Client.GetAsync($"{Api}/cards/{card.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await CardsOfAsync(s.Owner, s.Board.Id, s.Todo.Id)).Should().BeEmpty();
  }

  [Fact]
  public async Task ArchivedBoards_AreReadOnly()
  {
    var s = await CreateSetupAsync();
    var card = await s.Owner.CreateCardAsync(s.Todo.Id);
    await s.Owner.Client.PutJsonAsync($"{Api}/boards/{s.Board.Id}/archive", new ArchiveBoardRequest(true));

    var create = await s.Owner.Client.PostJsonAsync($"{Api}/columns/{s.Todo.Id}/cards", new CreateCardRequest("x", null, null));
    var move = await MoveAsync(s.Owner, card, s.Doing.Id);

    create.StatusCode.Should().Be(HttpStatusCode.Conflict);
    (await create.ReadProblemAsync()).Code.Should().Be("board.archived");
    move.StatusCode.Should().Be(HttpStatusCode.Conflict);
    (await s.Owner.Client.GetAsync($"{Api}/boards/{s.Board.Id}/cards")).StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task LabelsAndAssignees_AreSetThroughDedicatedEndpointsAndBumpTheVersion()
  {
    var s = await CreateSetupAsync();
    var label = await (await s.Owner.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/labels", new LabelRequest("Urgent", "#ff0000"))).ReadAsAsync<LabelDto>();
    var card = await s.Owner.CreateCardAsync(s.Todo.Id);

    var labeled = await (await s.Owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}/labels", new SetCardLabelsRequest(1, [label.Id]))).ReadAsAsync<CardDto>();
    var assigned = await (await s.Owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}/assignees", new SetCardAssigneesRequest(2, [s.Member.Id]))).ReadAsAsync<CardDto>();

    labeled.LabelIds.Should().Equal(label.Id);
    assigned.Should().BeEquivalentTo(new { Version = 3, LabelIds = new[] { label.Id }, AssigneeIds = new[] { s.Member.Id } });
    var outsider = await factory.RegisterUserAsync();
    var rejected = await s.Owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}/assignees", new SetCardAssigneesRequest(3, [outsider.Id]));
    rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    (await rejected.ReadProblemAsync()).Code.Should().Be("card.assignee_not_member");
  }

  [Fact]
  public async Task Search_FiltersSortsAndPages()
  {
    var s = await CreateSetupAsync();
    var bug = await (await s.Owner.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/labels", new LabelRequest("Bug", "#ff0000"))).ReadAsAsync<LabelDto>();
    var titles = new[] { "Delta", "Alpha", "Charlie", "Bravo", "Echo" };
    var cards = new List<CardDto>();
    foreach (var title in titles)
    {
      cards.Add(await s.Owner.CreateCardAsync(s.Todo.Id, title, title is "Alpha" or "Echo" ? new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) : null));
    }

    await s.Owner.Client.PutJsonAsync($"{Api}/cards/{cards[0].Id}/labels", new SetCardLabelsRequest(1, [bug.Id]));
    await s.Owner.Client.PutJsonAsync($"{Api}/cards/{cards[2].Id}/labels", new SetCardLabelsRequest(1, [bug.Id]));

    async Task<PagedResult<CardDto>> Get(string query) =>
      await (await s.Member.Client.GetAsync($"{Api}/boards/{s.Board.Id}/cards?{query}")).ReadAsAsync<PagedResult<CardDto>>();

    (await Get($"labelId={bug.Id}&sortBy=Title")).Items.Select(a => a.Title).Should().Equal("Charlie", "Delta");
    (await Get("hasDueDate=true&sortBy=Title&sortDirection=Desc")).Items.Select(a => a.Title).Should().Equal("Echo", "Alpha");
    (await Get("q=ALP")).Items.Should().ContainSingle().Which.Title.Should().Be("Alpha");

    var page1 = await Get("sortBy=Title&page=1&pageSize=2");
    var page3 = await Get("sortBy=Title&page=3&pageSize=2");
    page1.Items.Select(a => a.Title).Should().Equal("Alpha", "Bravo");
    page1.Should().BeEquivalentTo(new { TotalCount = 5, TotalPages = 3, Page = 1, PageSize = 2 });
    page3.Items.Select(a => a.Title).Should().Equal("Echo");
  }

  [Theory]
  [InlineData("page=0")]
  [InlineData("pageSize=0")]
  [InlineData("pageSize=201")]
  [InlineData("sortBy=Nonsense")]
  [InlineData("dueFrom=2026-02-01&dueTo=2026-01-01")]
  public async Task Search_RejectsInvalidParameters(string query)
  {
    var s = await CreateSetupAsync();

    var response = await s.Owner.Client.GetAsync($"{Api}/boards/{s.Board.Id}/cards?{query}");

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Columns_CanBeCreatedRenamedReorderedAndDeleted_WithRoleRules()
  {
    var s = await CreateSetupAsync();

    var created = await (await s.Member.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/columns", new CreateColumnRequest("Review"))).ReadAsAsync<ColumnDto>();
    var renamed = await s.Member.Client.PutJsonAsync($"{Api}/columns/{created.Id}", new UpdateColumnRequest("Code review"));
    var moved = await s.Member.Client.PutJsonAsync($"{Api}/columns/{created.Id}/move", new MoveColumnRequest(null, s.Todo.Id));
    var memberDelete = await s.Member.Client.DeleteAsync($"{Api}/columns/{created.Id}");

    renamed.StatusCode.Should().Be(HttpStatusCode.OK);
    (await moved.ReadAsAsync<MoveColumnResultDto>()).Rebalanced.Should().BeFalse();
    memberDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var board = await (await s.Member.Client.GetAsync($"{Api}/boards/{s.Board.Id}")).ReadAsAsync<BoardDetailDto>();
    board.Columns.Select(a => a.Title).Should().Equal("Code review", "To do", "In progress", "Done");

    var card = await s.Owner.CreateCardAsync(created.Id);
    (await s.Owner.Client.DeleteAsync($"{Api}/columns/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    (await s.Owner.Client.GetAsync($"{Api}/cards/{card.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Labels_AreManagedByMembersButOnlyAdminsDeleteThem()
  {
    var s = await CreateSetupAsync();
    var created = await s.Member.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/labels", new LabelRequest("Bug", "#ff0000"));
    var label = await created.ReadAsAsync<LabelDto>();

    var duplicate = await s.Member.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/labels", new LabelRequest("Bug", "#00ff00"));
    var badColor = await s.Member.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/labels", new LabelRequest("Other", "red"));
    var update = await s.Member.Client.PutJsonAsync($"{Api}/labels/{label.Id}", new LabelRequest("Defect", "#112233"));
    var memberDelete = await s.Member.Client.DeleteAsync($"{Api}/labels/{label.Id}");
    var ownerDelete = await s.Owner.Client.DeleteAsync($"{Api}/labels/{label.Id}");

    created.StatusCode.Should().Be(HttpStatusCode.Created);
    duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    badColor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    update.StatusCode.Should().Be(HttpStatusCode.OK);
    memberDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    ownerDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
  }
}
