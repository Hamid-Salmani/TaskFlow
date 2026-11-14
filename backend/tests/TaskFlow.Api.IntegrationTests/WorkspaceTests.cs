using System.Net;
using FluentAssertions;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.IntegrationTests.Support;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests;

public sealed class WorkspaceTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
  private const string Api = "/api/v1";

  [Fact]
  public async Task Anonymous_RequestsAreRejected()
  {
    var response = await factory.CreateClient().GetAsync($"{Api}/workspaces");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Create_MakesTheCallerOwnerAndListsOnlyOwnWorkspaces()
  {
    var ada = await factory.RegisterUserAsync("Ada");
    var bob = await factory.RegisterUserAsync("Bob");

    var created = await ada.CreateWorkspaceAsync("Ada's team");
    await bob.CreateWorkspaceAsync("Bob's team");

    created.MyRole.Should().Be(WorkspaceRole.Owner);
    var list = await (await ada.Client.GetAsync($"{Api}/workspaces")).ReadAsAsync<List<WorkspaceDto>>();
    list.Should().ContainSingle().Which.Id.Should().Be(created.Id);
  }

  [Fact]
  public async Task Create_ValidatesInputWithFieldErrors()
  {
    var ada = await factory.RegisterUserAsync();

    var response = await ada.Client.PostJsonAsync($"{Api}/workspaces", new CreateWorkspaceRequest("", null));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var problem = await response.ReadProblemAsync();
    problem.Code.Should().Be("validation.failed");
    problem.Errors.Should().ContainKey("name");
  }

  [Fact]
  public async Task NonMembers_GetNotFoundForEveryWorkspaceEndpoint_SoExistenceIsNotLeaked()
  {
    var owner = await factory.RegisterUserAsync();
    var outsider = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    var id = workspace.Id;

    var responses = new[]
    {
      await outsider.Client.GetAsync($"{Api}/workspaces/{id}"),
      await outsider.Client.PutJsonAsync($"{Api}/workspaces/{id}", new UpdateWorkspaceRequest("x", null)),
      await outsider.Client.DeleteAsync($"{Api}/workspaces/{id}"),
      await outsider.Client.GetAsync($"{Api}/workspaces/{id}/members"),
      await outsider.Client.PostJsonAsync($"{Api}/workspaces/{id}/members", new InviteMemberRequest(owner.Email, WorkspaceRole.Member)),
      await outsider.Client.GetAsync($"{Api}/workspaces/{id}/boards"),
      await outsider.Client.PostJsonAsync($"{Api}/workspaces/{id}/boards", new CreateBoardRequest("x", null)),
      await outsider.Client.GetAsync($"{Api}/workspaces/{Guid.NewGuid()}")
    };

    responses.Should().OnlyContain(a => a.StatusCode == HttpStatusCode.NotFound);
    (await responses[0].ReadProblemAsync()).Code.Should().Be("resource.not_found");
  }

  [Fact]
  public async Task Members_CanReadButNotAdminister()
  {
    var owner = await factory.RegisterUserAsync();
    var member = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);

    (await member.Client.GetAsync($"{Api}/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    var update = await member.Client.PutJsonAsync($"{Api}/workspaces/{workspace.Id}", new UpdateWorkspaceRequest("Hijacked", null));
    var delete = await member.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}");
    var invite = await member.Client.PostJsonAsync($"{Api}/workspaces/{workspace.Id}/members", new InviteMemberRequest(owner.Email, WorkspaceRole.Member));

    update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    invite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await update.ReadProblemAsync()).Code.Should().Be("resource.forbidden");
  }

  [Fact]
  public async Task Admins_CanUpdateButOnlyTheOwnerCanDelete()
  {
    var owner = await factory.RegisterUserAsync();
    var admin = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, admin, WorkspaceRole.Admin);

    var update = await admin.Client.PutJsonAsync($"{Api}/workspaces/{workspace.Id}", new UpdateWorkspaceRequest("Renamed", "desc"));
    var adminDelete = await admin.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}");
    var ownerDelete = await owner.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}");

    update.StatusCode.Should().Be(HttpStatusCode.OK);
    (await update.ReadAsAsync<WorkspaceDto>()).Should().BeEquivalentTo(new { Name = "Renamed", MyRole = WorkspaceRole.Admin });
    adminDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    ownerDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    (await owner.Client.GetAsync($"{Api}/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Invite_AddsMemberAndMembersAreListedByRole()
  {
    var owner = await factory.RegisterUserAsync("Zed Owner");
    var member = await factory.RegisterUserAsync("Amy Member");
    var workspace = await owner.CreateWorkspaceAsync();

    var response = await owner.Client.PostJsonAsync($"{Api}/workspaces/{workspace.Id}/members", new InviteMemberRequest(member.Email, WorkspaceRole.Member));

    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var members = await (await member.Client.GetAsync($"{Api}/workspaces/{workspace.Id}/members")).ReadAsAsync<List<WorkspaceMemberDto>>();
    members.Select(a => a.Role).Should().Equal(WorkspaceRole.Owner, WorkspaceRole.Member);
    (await member.Client.GetAsync($"{Api}/workspaces")).StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Invite_ReportsUnknownEmailsAndDuplicates()
  {
    var owner = await factory.RegisterUserAsync();
    var member = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);

    var unknown = await owner.Client.PostJsonAsync($"{Api}/workspaces/{workspace.Id}/members", new InviteMemberRequest("ghost@test.dev", WorkspaceRole.Member));
    var duplicate = await owner.Client.PostJsonAsync($"{Api}/workspaces/{workspace.Id}/members", new InviteMemberRequest(member.Email, WorkspaceRole.Member));
    var asOwner = await owner.Client.PostJsonAsync($"{Api}/workspaces/{workspace.Id}/members", new InviteMemberRequest(member.Email, WorkspaceRole.Owner));

    unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await unknown.ReadProblemAsync()).Code.Should().Be("user.not_found");
    duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    (await duplicate.ReadProblemAsync()).Code.Should().Be("member.already_exists");
    asOwner.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task ChangeRole_UpdatesTheRoleAndTheNewAdminGainsRights()
  {
    var owner = await factory.RegisterUserAsync();
    var member = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);
    var rename = new UpdateWorkspaceRequest("By promoted member", null);

    (await member.Client.PutJsonAsync($"{Api}/workspaces/{workspace.Id}", rename)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var change = await owner.Client.PutJsonAsync($"{Api}/workspaces/{workspace.Id}/members/{member.Id}", new ChangeMemberRoleRequest(WorkspaceRole.Admin));

    change.StatusCode.Should().Be(HttpStatusCode.OK);
    (await member.Client.PutJsonAsync($"{Api}/workspaces/{workspace.Id}", rename)).StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task ChangeRole_TheOwnerCannotBeDemoted()
  {
    var owner = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();

    var response = await owner.Client.PutJsonAsync($"{Api}/workspaces/{workspace.Id}/members/{owner.Id}", new ChangeMemberRoleRequest(WorkspaceRole.Member));

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await response.ReadProblemAsync()).Code.Should().Be("member.owner_immutable");
  }

  [Fact]
  public async Task Remove_AdminRemovesMember_WhoThenLosesAllAccess()
  {
    var owner = await factory.RegisterUserAsync();
    var member = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);
    var board = await owner.CreateBoardAsync(workspace.Id);

    (await member.Client.GetAsync($"{Api}/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    var removed = await owner.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}/members/{member.Id}");

    removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    (await member.Client.GetAsync($"{Api}/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await member.Client.GetAsync($"{Api}/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Remove_MembersCanLeaveButNotRemoveOthers()
  {
    var owner = await factory.RegisterUserAsync();
    var first = await factory.RegisterUserAsync();
    var second = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, first);
    await owner.AddMemberAsync(workspace.Id, second);

    var kick = await first.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}/members/{second.Id}");
    var leave = await first.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}/members/{first.Id}");

    kick.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    leave.StatusCode.Should().Be(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task Boards_CreateStartsWithDefaultColumns_AndFollowsRoleRules()
  {
    var owner = await factory.RegisterUserAsync();
    var member = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, member);

    var board = await member.CreateBoardAsync(workspace.Id, "Roadmap");

    board.Name.Should().Be("Roadmap");
    board.MyRole.Should().Be(WorkspaceRole.Member);
    board.Columns.Select(a => a.Title).Should().Equal("To do", "In progress", "Done");
    board.Members.Should().HaveCount(2);

    var rename = new UpdateBoardRequest("Renamed", null);
    (await member.Client.PutJsonAsync($"{Api}/boards/{board.Id}", rename)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await member.Client.DeleteAsync($"{Api}/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await member.Client.PutJsonAsync($"{Api}/boards/{board.Id}/archive", new ArchiveBoardRequest(true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await owner.Client.PutJsonAsync($"{Api}/boards/{board.Id}", rename)).StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Boards_AreInvisibleToOutsiders()
  {
    var owner = await factory.RegisterUserAsync();
    var outsider = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    var board = await owner.CreateBoardAsync(workspace.Id);

    (await outsider.Client.GetAsync($"{Api}/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await outsider.Client.PutJsonAsync($"{Api}/boards/{board.Id}", new UpdateBoardRequest("x", null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await outsider.Client.DeleteAsync($"{Api}/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Boards_ArchivedAreHiddenFromTheDefaultListAndDeletedOnesVanish()
  {
    var owner = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    var keep = await owner.CreateBoardAsync(workspace.Id, "Keep");
    var archive = await owner.CreateBoardAsync(workspace.Id, "Archive me");
    var delete = await owner.CreateBoardAsync(workspace.Id, "Delete me");

    (await owner.Client.PutJsonAsync($"{Api}/boards/{archive.Id}/archive", new ArchiveBoardRequest(true))).StatusCode.Should().Be(HttpStatusCode.OK);
    (await owner.Client.DeleteAsync($"{Api}/boards/{delete.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

    var active = await (await owner.Client.GetAsync($"{Api}/workspaces/{workspace.Id}/boards")).ReadAsAsync<List<BoardSummaryDto>>();
    var all = await (await owner.Client.GetAsync($"{Api}/workspaces/{workspace.Id}/boards?includeArchived=true")).ReadAsAsync<List<BoardSummaryDto>>();
    active.Select(a => a.Id).Should().Equal(keep.Id);
    all.Select(a => a.Id).Should().Equal(keep.Id, archive.Id);
    (await owner.Client.GetAsync($"{Api}/boards/{delete.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task DeletingAWorkspace_HidesItsBoards()
  {
    var owner = await factory.RegisterUserAsync();
    var workspace = await owner.CreateWorkspaceAsync();
    var board = await owner.CreateBoardAsync(workspace.Id);

    await owner.Client.DeleteAsync($"{Api}/workspaces/{workspace.Id}");

    (await owner.Client.GetAsync($"{Api}/boards/{board.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }
}
