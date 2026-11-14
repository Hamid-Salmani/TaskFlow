using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.IntegrationTests.Support;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.IntegrationTests;

public sealed class BoardHubTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
  private const string Api = "/api/v1";

  private sealed record Setup(TestUser Owner, TestUser Member, BoardDetailDto Board);

  private async Task<Setup> CreateSetupAsync()
  {
    var owner = await factory.RegisterUserAsync("Owner");
    var member = await factory.RegisterUserAsync("Member");
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);
    return new Setup(owner, member, await owner.CreateBoardAsync(workspace.Id));
  }

  [Fact]
  public async Task AMemberWhoJoinedTheBoardReceivesTheMoveMadeOverHttp_WithTheActorsId()
  {
    var s = await CreateSetupAsync();
    var card = await s.Owner.CreateCardAsync(s.Board.Columns[0].Id, "Move me");
    await using var hub = await HubClient.ConnectAsync(factory, s.Member);
    await hub.Connection.InvokeAsync("JoinBoard", s.Board.Id);

    var response = await s.Owner.Client.PutJsonAsync(
      $"{Api}/cards/{card.Id}/move",
      new MoveCardRequest(card.Version, s.Board.Columns[1].Id, null, null));
    response.EnsureSuccessStatusCode();

    var (name, payload) = await hub.NextAsync();
    name.Should().Be("CardMoved");
    payload.GetProperty("actorUserId").GetGuid().Should().Be(s.Owner.Id);
    payload.GetProperty("boardId").GetGuid().Should().Be(s.Board.Id);
    payload.GetProperty("cardId").GetGuid().Should().Be(card.Id);
    payload.GetProperty("fromColumnId").GetGuid().Should().Be(s.Board.Columns[0].Id);
    payload.GetProperty("toColumnId").GetGuid().Should().Be(s.Board.Columns[1].Id);
    payload.GetProperty("version").GetInt32().Should().Be(card.Version + 1);
  }

  [Fact]
  public async Task CreatingAndDeletingACardArePublished()
  {
    var s = await CreateSetupAsync();
    await using var hub = await HubClient.ConnectAsync(factory, s.Member);
    await hub.Connection.InvokeAsync("JoinBoard", s.Board.Id);

    var card = await s.Owner.CreateCardAsync(s.Board.Columns[0].Id, "Fresh");
    var created = await hub.NextAsync();
    created.Name.Should().Be("CardCreated");
    created.Payload.GetProperty("card").GetProperty("id").GetGuid().Should().Be(card.Id);

    (await s.Owner.Client.DeleteAsync($"{Api}/cards/{card.Id}")).EnsureSuccessStatusCode();
    var deleted = await hub.NextAsync();
    deleted.Name.Should().Be("CardDeleted");
    deleted.Payload.GetProperty("cardId").GetGuid().Should().Be(card.Id);
  }

  [Fact]
  public async Task ChangingTheColumnsIsPublished()
  {
    var s = await CreateSetupAsync();
    await using var hub = await HubClient.ConnectAsync(factory, s.Member);
    await hub.Connection.InvokeAsync("JoinBoard", s.Board.Id);

    var response = await s.Owner.Client.PostJsonAsync($"{Api}/boards/{s.Board.Id}/columns", new CreateColumnRequest("Review"));
    response.EnsureSuccessStatusCode();

    (await hub.NextAsync()).Name.Should().Be("ColumnsChanged");
  }

  [Fact]
  public async Task ANonMemberCannotJoinAndGetsTheSameAnswerAsForAMissingBoard()
  {
    var s = await CreateSetupAsync();
    var outsider = await factory.RegisterUserAsync("Outsider");
    await using var hub = await HubClient.ConnectAsync(factory, outsider);

    var existing = async () => await hub.Connection.InvokeAsync("JoinBoard", s.Board.Id);
    var missing = async () => await hub.Connection.InvokeAsync("JoinBoard", Guid.NewGuid());

    var a = (await existing.Should().ThrowAsync<HubException>()).Which.Message;
    var b = (await missing.Should().ThrowAsync<HubException>()).Which.Message;
    a.Should().Be(b).And.Contain("Board not found");

    // and nothing leaks afterwards
    await s.Owner.CreateCardAsync(s.Board.Columns[0].Id, "Secret");
    (await hub.StaysQuietAsync()).Should().BeTrue();
  }

  [Fact]
  public async Task ConnectionsWithoutAValidTokenAreRejected()
  {
    await using var anonymous = new HubClient(factory, accessToken: null);
    await using var forged = new HubClient(factory, accessToken: "not-a-jwt");

    var noToken = async () => await anonymous.Connection.StartAsync();
    var badToken = async () => await forged.Connection.StartAsync();

    await noToken.Should().ThrowAsync<HttpRequestException>();
    await badToken.Should().ThrowAsync<HttpRequestException>();
  }

  [Fact]
  public async Task OnlyConnectionsOfThatBoardGetItsEvents_AndLeavingStopsThem()
  {
    var s = await CreateSetupAsync();
    var workspace = await s.Owner.CreateWorkspaceAsync("Other");
    var otherBoard = await s.Owner.CreateBoardAsync(workspace.Id, "Other board");
    await using var hub = await HubClient.ConnectAsync(factory, s.Owner);
    await hub.Connection.InvokeAsync("JoinBoard", s.Board.Id);

    await s.Owner.CreateCardAsync(otherBoard.Columns[0].Id, "Elsewhere");
    (await hub.StaysQuietAsync()).Should().BeTrue("the connection never joined the other board");

    await hub.Connection.InvokeAsync("LeaveBoard", s.Board.Id);
    await s.Owner.CreateCardAsync(s.Board.Columns[0].Id, "After leaving");
    (await hub.StaysQuietAsync()).Should().BeTrue("the connection left the board");
  }
}
