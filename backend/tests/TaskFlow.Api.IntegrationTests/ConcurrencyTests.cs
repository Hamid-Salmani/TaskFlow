using System.Net;
using FluentAssertions;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.IntegrationTests.Support;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.IntegrationTests;

/// <summary>Real simultaneous requests against separate database connections.</summary>
public sealed class ConcurrencyTests(FileDatabaseFactory factory) : IClassFixture<FileDatabaseFactory>
{
  private const string Api = "/api/v1";

  private async Task<(TestUser Owner, TestUser Member, BoardDetailDto Board)> CreateSetupAsync()
  {
    var owner = await factory.RegisterUserAsync("Owner");
    var member = await factory.RegisterUserAsync("Member");
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);
    return (owner, member, await owner.CreateBoardAsync(workspace.Id));
  }

  [Fact]
  public async Task TwoParallelEdits_OfTheSameVersion_OnlyOneWins()
  {
    var (owner, member, board) = await CreateSetupAsync();

    for (var round = 0; round < 5; round++)
    {
      var card = await owner.CreateCardAsync(board.Columns[0].Id, $"Round {round}");

      var responses = await Task.WhenAll(
        owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}", new UpdateCardRequest(card.Version, "Owner edit", null, null)),
        member.Client.PutJsonAsync($"{Api}/cards/{card.Id}", new UpdateCardRequest(card.Version, "Member edit", null, null)));

      responses.Select(a => a.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict], $"round {round}");
      var loser = responses.Single(a => a.StatusCode == HttpStatusCode.Conflict);
      (await loser.ReadProblemAsync()).Code.Should().NotBeNullOrEmpty();
      var stored = await (await owner.Client.GetAsync($"{Api}/cards/{card.Id}")).ReadAsAsync<CardDto>();
      stored.Version.Should().Be(2, "exactly one write was applied");
      stored.Should().BeEquivalentTo(await responses.Single(a => a.StatusCode == HttpStatusCode.OK).ReadAsAsync<CardDto>());
    }
  }

  [Fact]
  public async Task ParallelAppendsToTheSameColumn_NeverFail_AndTheNextMoveRepairsAnyTiedPositions()
  {
    var (owner, _, board) = await CreateSetupAsync();
    var todo = board.Columns[0];
    var doing = board.Columns[1];
    var cards = new List<CardDto>();
    for (var i = 0; i < 6; i++)
    {
      cards.Add(await owner.CreateCardAsync(todo.Id, $"Card {i}"));
    }

    // Every move appends to "Doing" and computes its slot from the same snapshot, so positions may tie.
    var responses = await Task.WhenAll(cards.Select(card =>
      owner.Client.PutJsonAsync($"{Api}/cards/{card.Id}/move", new MoveCardRequest(card.Version, doing.Id, null, null))));

    responses.Should().OnlyContain(a => a.StatusCode == HttpStatusCode.OK, "different cards have different versions, so nothing conflicts");
    async Task<List<CardDto>> InDoing() =>
      (await (await owner.Client.GetAsync($"{Api}/boards/{board.Id}/cards?pageSize=200")).ReadAsAsync<PagedResult<CardDto>>())
      .Items.Where(a => a.ColumnId == doing.Id).OrderBy(a => a.Position).ThenBy(a => a.Id).ToList();
    var column = await InDoing();
    column.Should().HaveCount(6);

    // Moving a card between two neighbours (even tied ones) must work and leave strictly ordered positions.
    var mover = await owner.CreateCardAsync(todo.Id, "Mover");
    var move = await owner.Client.PutJsonAsync(
      $"{Api}/cards/{mover.Id}/move",
      new MoveCardRequest(mover.Version, doing.Id, column[2].Id, column[3].Id));

    move.StatusCode.Should().Be(HttpStatusCode.OK);
    var after = await InDoing();
    after.Select(a => a.Id).Should().Equal(column.Take(3).Select(a => a.Id).Append(mover.Id).Concat(column.Skip(3).Select(a => a.Id)));
    after.Select(a => a.Position).Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
  }
}
