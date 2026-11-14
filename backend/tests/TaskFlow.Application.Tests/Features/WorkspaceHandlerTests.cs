using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Workspaces;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class WorkspaceHandlerTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;

  public WorkspaceHandlerTests() => _world = _fixture.CreateWorld();

  public void Dispose() => _fixture.Dispose();

  private Task<List<WorkspaceMember>> MembersAsync()
  {
    _fixture.ClearTracking();
    return _fixture.Db.WorkspaceMembers.Where(a => a.WorkspaceId == _world.Workspace.Id).ToListAsync();
  }

  // ----- create / read -----

  [Fact]
  public async Task Create_MakesTheCallerTheOwnerAndTrimsInput()
  {
    var creator = _fixture.Add(new UserBuilder("Creator").Build());
    _fixture.ActAs(creator);
    var handler = new CreateWorkspaceCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Time);

    var result = await handler.Handle(new CreateWorkspaceCommand("  Team  ", " about "), default);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeEquivalentTo(new { Name = "Team", Description = "about", MyRole = WorkspaceRole.Owner, MemberCount = 1, BoardCount = 0 });
    var member = await _fixture.Db.WorkspaceMembers.SingleAsync(a => a.WorkspaceId == result.Value.Id);
    member.UserId.Should().Be(creator.Id);
    member.JoinedAt.Should().Be(_fixture.Time.GetUtcNow().UtcDateTime);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void CreateValidator_RejectsBlankNames(string name) =>
    new CreateWorkspaceCommandValidator().Validate(new CreateWorkspaceCommand(name, null)).IsValid.Should().BeFalse();

  [Fact]
  public void CreateValidator_RejectsTooLongValues()
  {
    var validator = new CreateWorkspaceCommandValidator();

    validator.Validate(new CreateWorkspaceCommand(new string('x', 101), null)).IsValid.Should().BeFalse();
    validator.Validate(new CreateWorkspaceCommand("ok", new string('x', 1001))).IsValid.Should().BeFalse();
    validator.Validate(new CreateWorkspaceCommand(new string('x', 100), new string('x', 1000))).IsValid.Should().BeTrue();
  }

  [Fact]
  public async Task List_ReturnsOnlyWorkspacesOfTheCallerWithTheirRole()
  {
    var other = _fixture.Add(new WorkspaceBuilder().WithName("Other").WithMember(_world.Outsider, WorkspaceRole.Owner).Build());
    _fixture.ActAs(_world.Member);
    var handler = new ListWorkspacesQueryHandler(_fixture.Db, _fixture.CurrentUser);

    var result = await handler.Handle(new ListWorkspacesQuery(), default);

    result.Value.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new { Id = _world.Workspace.Id, MyRole = WorkspaceRole.Member, MemberCount = 3, BoardCount = 1 });
    result.Value.Should().NotContain(a => a.Id == other.Id);
  }

  [Fact]
  public async Task List_HidesSoftDeletedWorkspaces()
  {
    _world.Workspace.SoftDelete(_fixture.Time.GetUtcNow().UtcDateTime);
    _fixture.Db.SaveChanges();
    _fixture.ActAs(_world.Owner);

    var result = await new ListWorkspacesQueryHandler(_fixture.Db, _fixture.CurrentUser).Handle(new ListWorkspacesQuery(), default);

    result.Value.Should().BeEmpty();
  }

  [Fact]
  public async Task Get_ReturnsTheWorkspaceWithTheCallersRole()
  {
    _fixture.ActAs(_world.Admin);

    var result = await new GetWorkspaceQueryHandler(_fixture.Db, _fixture.CurrentUser)
      .Handle(new GetWorkspaceQuery(_world.Workspace.Id), default);

    result.Value.MyRole.Should().Be(WorkspaceRole.Admin);
  }

  // ----- update / delete -----

  [Fact]
  public async Task Update_ChangesNameAndDescription()
  {
    _fixture.ActAs(_world.Admin);

    var result = await new UpdateWorkspaceCommandHandler(_fixture.Db, _fixture.CurrentUser)
      .Handle(new UpdateWorkspaceCommand(_world.Workspace.Id, " Renamed ", null), default);

    result.Value.Name.Should().Be("Renamed");
    _fixture.ClearTracking();
    (await _fixture.Db.Workspaces.SingleAsync()).UpdatedAt.Should().NotBeNull();
  }

  [Fact]
  public void UpdateValidator_RejectsBlankName() =>
    new UpdateWorkspaceCommandValidator().Validate(new UpdateWorkspaceCommand(Guid.NewGuid(), "", null)).IsValid.Should().BeFalse();

  [Fact]
  public async Task Delete_SoftDeletesTheWorkspace()
  {
    var result = await new DeleteWorkspaceCommandHandler(_fixture.Db, _fixture.Time)
      .Handle(new DeleteWorkspaceCommand(_world.Workspace.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    (await _fixture.Db.Workspaces.AnyAsync()).Should().BeFalse("the global filter hides deleted rows");
    var raw = await _fixture.Db.Workspaces.IgnoreQueryFilters().SingleAsync();
    raw.IsDeleted.Should().BeTrue();
    raw.DeleteTime.Should().Be(_fixture.Time.GetUtcNow().UtcDateTime);
  }

  // ----- invite -----

  [Fact]
  public async Task Invite_AddsARegisteredUserByEmailCaseInsensitively()
  {
    var invitee = _fixture.Add(new UserBuilder("Invitee").Build());
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var result = await Invite(invitee.Email!.ToUpperInvariant(), WorkspaceRole.Member);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeEquivalentTo(new { UserId = invitee.Id, Role = WorkspaceRole.Member, DisplayName = "Invitee" });
    (await MembersAsync()).Should().HaveCount(4);
  }

  [Fact]
  public async Task Invite_FailsForUnknownEmails()
  {
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var result = await Invite("nobody@test.dev", WorkspaceRole.Member);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.NotFound, Code = "user.not_found" });
  }

  [Fact]
  public async Task Invite_FailsWhenAlreadyAMember()
  {
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var result = await Invite(_world.Member.Email!, WorkspaceRole.Member);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Conflict, Code = "member.already_exists" });
  }

  [Fact]
  public async Task Invite_OnlyTheOwnerMayGrantTheAdminRole()
  {
    var invitee = _fixture.Add(new UserBuilder("Invitee").Build());
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var asAdmin = await Invite(invitee.Email!, WorkspaceRole.Admin);

    asAdmin.Error!.Code.Should().Be("member.owner_required");

    _fixture.ActAs(_world.Owner, WorkspaceRole.Owner);
    var asOwner = await Invite(invitee.Email!, WorkspaceRole.Admin);

    asOwner.Value.Role.Should().Be(WorkspaceRole.Admin);
  }

  [Fact]
  public void InviteValidator_RejectsOwnerRoleBadEmailAndUndefinedRoles()
  {
    var validator = new InviteMemberCommandValidator();
    var id = Guid.NewGuid();

    validator.Validate(new InviteMemberCommand(id, "a@b.dev", WorkspaceRole.Owner)).IsValid.Should().BeFalse();
    validator.Validate(new InviteMemberCommand(id, "not-an-email", WorkspaceRole.Member)).IsValid.Should().BeFalse();
    validator.Validate(new InviteMemberCommand(id, "a@b.dev", (WorkspaceRole)99)).IsValid.Should().BeFalse();
    validator.Validate(new InviteMemberCommand(id, "a@b.dev", WorkspaceRole.Admin)).IsValid.Should().BeTrue();
  }

  private Task<Result<TaskFlow.Application.Contracts.WorkspaceMemberDto>> Invite(string email, WorkspaceRole role) =>
    new InviteMemberCommandHandler(_fixture.Db, _fixture.Authorization, _fixture.Time)
      .Handle(new InviteMemberCommand(_world.Workspace.Id, email, role), default);

  // ----- change role -----

  [Fact]
  public async Task ChangeRole_OwnerCanPromoteAndDemote()
  {
    _fixture.ActAs(_world.Owner, WorkspaceRole.Owner);

    var promoted = await ChangeRole(_world.Member.Id, WorkspaceRole.Admin);
    var demoted = await ChangeRole(_world.Member.Id, WorkspaceRole.Member);

    promoted.Value.Role.Should().Be(WorkspaceRole.Admin);
    demoted.Value.Role.Should().Be(WorkspaceRole.Member);
  }

  [Fact]
  public async Task ChangeRole_AdminCannotTouchTheAdminRole()
  {
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var grant = await ChangeRole(_world.Member.Id, WorkspaceRole.Admin);

    grant.Error!.Code.Should().Be("member.owner_required");
    (await MembersAsync()).Single(a => a.UserId == _world.Member.Id).Role.Should().Be(WorkspaceRole.Member);

    var revoke = await ChangeRole(_world.Admin.Id, WorkspaceRole.Member);

    revoke.Error!.Code.Should().Be("member.owner_required");
  }

  [Fact]
  public async Task ChangeRole_TheOwnerRoleIsImmutable()
  {
    _fixture.ActAs(_world.Owner, WorkspaceRole.Owner);

    var result = await ChangeRole(_world.Owner.Id, WorkspaceRole.Member);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Forbidden, Code = "member.owner_immutable" });
  }

  [Fact]
  public async Task ChangeRole_FailsForNonMembers()
  {
    _fixture.ActAs(_world.Owner, WorkspaceRole.Owner);

    var result = await ChangeRole(_world.Outsider.Id, WorkspaceRole.Member);

    result.Error!.Code.Should().Be("member.not_found");
  }

  [Fact]
  public void ChangeRoleValidator_RejectsOwnership() =>
    new ChangeMemberRoleCommandValidator()
      .Validate(new ChangeMemberRoleCommand(Guid.NewGuid(), Guid.NewGuid(), WorkspaceRole.Owner)).IsValid.Should().BeFalse();

  private Task<Result<TaskFlow.Application.Contracts.WorkspaceMemberDto>> ChangeRole(Guid userId, WorkspaceRole role) =>
    new ChangeMemberRoleCommandHandler(_fixture.Db, _fixture.Authorization)
      .Handle(new ChangeMemberRoleCommand(_world.Workspace.Id, userId, role), default);

  // ----- remove -----

  [Fact]
  public async Task Remove_AdminCanRemoveAMemberAndTheirAssignmentsGoToo()
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).AssignedTo(_world.Member).AssignedTo(_world.Owner).Build());
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var result = await Remove(_world.Member.Id);

    result.IsSuccess.Should().BeTrue();
    (await MembersAsync()).Should().NotContain(a => a.UserId == _world.Member.Id);
    var assignees = await _fixture.Db.CardAssignees.Where(a => a.CardId == card.Id).ToListAsync();
    assignees.Should().ContainSingle().Which.UserId.Should().Be(_world.Owner.Id);
  }

  [Fact]
  public async Task Remove_AMemberCanLeaveButNotRemoveOthers()
  {
    _fixture.ActAs(_world.Member, WorkspaceRole.Member);

    var others = await Remove(_world.Admin.Id);
    var self = await Remove(_world.Member.Id);

    others.Error!.Type.Should().Be(ErrorType.Forbidden);
    self.IsSuccess.Should().BeTrue();
    (await MembersAsync()).Should().NotContain(a => a.UserId == _world.Member.Id);
  }

  [Fact]
  public async Task Remove_TheOwnerCannotBeRemovedOrLeave()
  {
    _fixture.ActAs(_world.Owner, WorkspaceRole.Owner);

    var result = await Remove(_world.Owner.Id);

    result.Error!.Code.Should().Be("member.owner_immutable");
  }

  [Fact]
  public async Task Remove_AdminCannotRemoveAnotherAdminButTheOwnerCan()
  {
    var secondAdmin = _fixture.Add(new UserBuilder("Admin2").Build());
    _fixture.Add(new WorkspaceMember { WorkspaceId = _world.Workspace.Id, UserId = secondAdmin.Id, Role = WorkspaceRole.Admin, JoinedAt = DateTime.UtcNow });
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    var asAdmin = await Remove(secondAdmin.Id);

    asAdmin.Error!.Code.Should().Be("member.owner_required");

    _fixture.ActAs(_world.Owner, WorkspaceRole.Owner);
    (await Remove(secondAdmin.Id)).IsSuccess.Should().BeTrue();
  }

  [Fact]
  public async Task Remove_FailsForNonMembers()
  {
    _fixture.ActAs(_world.Admin, WorkspaceRole.Admin);

    (await Remove(_world.Outsider.Id)).Error!.Code.Should().Be("member.not_found");
  }

  private Task<Result> Remove(Guid userId) =>
    new RemoveMemberCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Authorization)
      .Handle(new RemoveMemberCommand(_world.Workspace.Id, userId), default);

  [Fact]
  public async Task ListMembers_OrdersByRoleThenName()
  {
    var result = await new ListMembersQueryHandler(_fixture.Db).Handle(new ListMembersQuery(_world.Workspace.Id), default);

    result.Value.Select(a => a.Role).Should().Equal(WorkspaceRole.Owner, WorkspaceRole.Admin, WorkspaceRole.Member);
  }
}
