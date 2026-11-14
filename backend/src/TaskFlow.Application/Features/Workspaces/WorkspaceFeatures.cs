using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Workspaces;

public static class WorkspaceErrors
{
  public static Error UserNotFound() =>
    Error.NotFound("user.not_found", "No registered user has that email address. Ask them to sign up first.");

  public static Error AlreadyMember() =>
    Error.Conflict("member.already_exists", "That user is already a member of this workspace.");

  public static Error MemberNotFound() => Error.NotFound("member.not_found", "That user is not a member of this workspace.");

  public static Error OwnerImmutable() =>
    Error.Forbidden("member.owner_immutable", "The owner's role cannot be changed and the owner cannot be removed.");

  public static Error OwnerOnly() =>
    Error.Forbidden("member.owner_required", "Only the workspace owner can grant, change or remove the admin role.");
}

public static class WorkspaceQueries
{
  public static Expression<Func<Workspace, WorkspaceDto>> ToDto(Guid userId) => a => new WorkspaceDto(
    a.Id,
    a.Name,
    a.Description,
    a.Members.Where(b => b.UserId == userId).Select(b => b.Role).FirstOrDefault(),
    a.Members.Count(),
    a.Boards.Count());

  /// <summary>
  /// Roles are stored as strings, so ordering by the enum column would sort alphabetically. Rank them explicitly.
  /// </summary>
  public static IOrderedQueryable<WorkspaceMember> OrderByRoleThenName(this IQueryable<WorkspaceMember> members) =>
    members
      .OrderByDescending(a => a.Role == WorkspaceRole.Owner ? 3 : a.Role == WorkspaceRole.Admin ? 2 : 1)
      .ThenBy(a => a.User.DisplayName);

  public static readonly Expression<Func<WorkspaceMember, WorkspaceMemberDto>> MemberToDto = a => new WorkspaceMemberDto(
    a.UserId,
    a.User.Email!,
    a.User.DisplayName,
    a.User.Handle,
    a.Role,
    a.JoinedAt);
}

// ---------- Create ----------

public sealed record CreateWorkspaceCommand(string Name, string? Description) : IRequest<Result<WorkspaceDto>>;

public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
  public CreateWorkspaceCommandValidator()
  {
    RuleFor(a => a.Name).NotEmpty().MaximumLength(100);
    RuleFor(a => a.Description).MaximumLength(1000);
  }
}

public sealed class CreateWorkspaceCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
  : IRequestHandler<CreateWorkspaceCommand, Result<WorkspaceDto>>
{
  public async Task<Result<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
  {
    var workspace = new Workspace { Name = request.Name.Trim(), Description = request.Description?.Trim() };
    workspace.Members.Add(new WorkspaceMember
    {
      UserId = currentUser.UserId,
      Role = WorkspaceRole.Owner,
      JoinedAt = timeProvider.GetUtcNow().UtcDateTime
    });

    db.Workspaces.Add(workspace);
    await db.SaveChangesAsync(cancellationToken);

    return await db.Workspaces
      .AsNoTracking()
      .Where(a => a.Id == workspace.Id)
      .Select(WorkspaceQueries.ToDto(currentUser.UserId))
      .FirstAsync(cancellationToken);
  }
}

// ---------- List / get ----------

public sealed record ListWorkspacesQuery : IRequest<Result<List<WorkspaceDto>>>;

public sealed class ListWorkspacesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<ListWorkspacesQuery, Result<List<WorkspaceDto>>>
{
  public async Task<Result<List<WorkspaceDto>>> Handle(ListWorkspacesQuery request, CancellationToken cancellationToken)
  {
    var userId = currentUser.UserId;
    return await db.Workspaces
      .AsNoTracking()
      .Where(a => a.Members.Any(b => b.UserId == userId))
      .OrderBy(a => a.Name)
      .Select(WorkspaceQueries.ToDto(userId))
      .ToListAsync(cancellationToken);
  }
}

public sealed record GetWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<WorkspaceDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class GetWorkspaceQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<GetWorkspaceQuery, Result<WorkspaceDto>>
{
  public async Task<Result<WorkspaceDto>> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken) =>
    await db.Workspaces
      .AsNoTracking()
      .Where(a => a.Id == request.WorkspaceId)
      .Select(WorkspaceQueries.ToDto(currentUser.UserId))
      .FirstAsync(cancellationToken);
}

// ---------- Update / delete ----------

public sealed record UpdateWorkspaceCommand(Guid WorkspaceId, string Name, string? Description)
  : IRequest<Result<WorkspaceDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class UpdateWorkspaceCommandValidator : AbstractValidator<UpdateWorkspaceCommand>
{
  public UpdateWorkspaceCommandValidator()
  {
    RuleFor(a => a.Name).NotEmpty().MaximumLength(100);
    RuleFor(a => a.Description).MaximumLength(1000);
  }
}

public sealed class UpdateWorkspaceCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<UpdateWorkspaceCommand, Result<WorkspaceDto>>
{
  public async Task<Result<WorkspaceDto>> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
  {
    var workspace = await db.Workspaces.FirstAsync(a => a.Id == request.WorkspaceId, cancellationToken);
    workspace.Name = request.Name.Trim();
    workspace.Description = request.Description?.Trim();
    await db.SaveChangesAsync(cancellationToken);

    return await db.Workspaces
      .AsNoTracking()
      .Where(a => a.Id == workspace.Id)
      .Select(WorkspaceQueries.ToDto(currentUser.UserId))
      .FirstAsync(cancellationToken);
  }
}

public sealed record DeleteWorkspaceCommand(Guid WorkspaceId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Owner;
}

public sealed class DeleteWorkspaceCommandHandler(IApplicationDbContext db, TimeProvider timeProvider)
  : IRequestHandler<DeleteWorkspaceCommand, Result>
{
  public async Task<Result> Handle(DeleteWorkspaceCommand request, CancellationToken cancellationToken)
  {
    var workspace = await db.Workspaces.FirstAsync(a => a.Id == request.WorkspaceId, cancellationToken);
    workspace.SoftDelete(timeProvider.GetUtcNow().UtcDateTime);
    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

// ---------- Members ----------

public sealed record ListMembersQuery(Guid WorkspaceId) : IRequest<Result<List<WorkspaceMemberDto>>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class ListMembersQueryHandler(IApplicationDbContext db)
  : IRequestHandler<ListMembersQuery, Result<List<WorkspaceMemberDto>>>
{
  public async Task<Result<List<WorkspaceMemberDto>>> Handle(ListMembersQuery request, CancellationToken cancellationToken) =>
    await db.WorkspaceMembers
      .AsNoTracking()
      .Where(a => a.WorkspaceId == request.WorkspaceId)
      .OrderByRoleThenName()
      .Select(WorkspaceQueries.MemberToDto)
      .ToListAsync(cancellationToken);
}

public sealed record InviteMemberCommand(Guid WorkspaceId, string Email, WorkspaceRole Role)
  : IRequest<Result<WorkspaceMemberDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
  public InviteMemberCommandValidator()
  {
    RuleFor(a => a.Email).NotEmpty().EmailAddress();
    RuleFor(a => a.Role)
      .IsInEnum()
      .NotEqual(WorkspaceRole.Owner).WithMessage("A workspace has exactly one owner; invite people as Admin or Member.");
  }
}

public sealed class InviteMemberCommandHandler(IApplicationDbContext db, IAuthorizationContext authorization, TimeProvider timeProvider)
  : IRequestHandler<InviteMemberCommand, Result<WorkspaceMemberDto>>
{
  public async Task<Result<WorkspaceMemberDto>> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
  {
    if (request.Role == WorkspaceRole.Admin && authorization.Role != WorkspaceRole.Owner)
    {
      return WorkspaceErrors.OwnerOnly();
    }

    var normalizedEmail = request.Email.Trim().ToUpperInvariant();
    var user = await db.Users.FirstOrDefaultAsync(a => a.NormalizedEmail == normalizedEmail, cancellationToken);
    if (user is null)
    {
      return WorkspaceErrors.UserNotFound();
    }

    var exists = await db.WorkspaceMembers
      .AnyAsync(a => a.WorkspaceId == request.WorkspaceId && a.UserId == user.Id, cancellationToken);
    if (exists)
    {
      return WorkspaceErrors.AlreadyMember();
    }

    db.WorkspaceMembers.Add(new WorkspaceMember
    {
      WorkspaceId = request.WorkspaceId,
      UserId = user.Id,
      Role = request.Role,
      JoinedAt = timeProvider.GetUtcNow().UtcDateTime
    });
    await db.SaveChangesAsync(cancellationToken);

    return await db.WorkspaceMembers
      .AsNoTracking()
      .Where(a => a.WorkspaceId == request.WorkspaceId && a.UserId == user.Id)
      .Select(WorkspaceQueries.MemberToDto)
      .FirstAsync(cancellationToken);
  }
}

public sealed record ChangeMemberRoleCommand(Guid WorkspaceId, Guid UserId, WorkspaceRole Role)
  : IRequest<Result<WorkspaceMemberDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
  public ChangeMemberRoleCommandValidator()
  {
    RuleFor(a => a.Role)
      .IsInEnum()
      .NotEqual(WorkspaceRole.Owner).WithMessage("Ownership cannot be assigned. Choose Admin or Member.");
  }
}

public sealed class ChangeMemberRoleCommandHandler(IApplicationDbContext db, IAuthorizationContext authorization)
  : IRequestHandler<ChangeMemberRoleCommand, Result<WorkspaceMemberDto>>
{
  public async Task<Result<WorkspaceMemberDto>> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
  {
    var member = await db.WorkspaceMembers
      .FirstOrDefaultAsync(a => a.WorkspaceId == request.WorkspaceId && a.UserId == request.UserId, cancellationToken);
    if (member is null)
    {
      return WorkspaceErrors.MemberNotFound();
    }

    if (member.Role == WorkspaceRole.Owner)
    {
      return WorkspaceErrors.OwnerImmutable();
    }

    // Admins manage plain members; touching the admin role (granting or revoking) is reserved for the owner.
    var touchesAdminRole = member.Role == WorkspaceRole.Admin || request.Role == WorkspaceRole.Admin;
    if (touchesAdminRole && authorization.Role != WorkspaceRole.Owner)
    {
      return WorkspaceErrors.OwnerOnly();
    }

    member.Role = request.Role;
    await db.SaveChangesAsync(cancellationToken);

    return await db.WorkspaceMembers
      .AsNoTracking()
      .Where(a => a.WorkspaceId == request.WorkspaceId && a.UserId == request.UserId)
      .Select(WorkspaceQueries.MemberToDto)
      .FirstAsync(cancellationToken);
  }
}

/// <summary>Removes a member. Any member may remove themselves ("leave"); removing others needs Admin.</summary>
public sealed record RemoveMemberCommand(Guid WorkspaceId, Guid UserId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class RemoveMemberCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuthorizationContext authorization)
  : IRequestHandler<RemoveMemberCommand, Result>
{
  public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
  {
    var isSelf = request.UserId == currentUser.UserId;
    if (isSelf == false && authorization.Role < WorkspaceRole.Admin)
    {
      return Error.Forbidden("resource.forbidden", "You do not have permission to perform this action.");
    }

    var member = await db.WorkspaceMembers
      .FirstOrDefaultAsync(a => a.WorkspaceId == request.WorkspaceId && a.UserId == request.UserId, cancellationToken);
    if (member is null)
    {
      return WorkspaceErrors.MemberNotFound();
    }

    if (member.Role == WorkspaceRole.Owner)
    {
      return WorkspaceErrors.OwnerImmutable();
    }

    if (isSelf == false && member.Role == WorkspaceRole.Admin && authorization.Role != WorkspaceRole.Owner)
    {
      return WorkspaceErrors.OwnerOnly();
    }

    // A removed member must not stay assigned to cards of the workspace.
    var assignments = await db.CardAssignees
      .Where(a => a.UserId == request.UserId && a.Card.Board.WorkspaceId == request.WorkspaceId)
      .ToListAsync(cancellationToken);
    db.CardAssignees.RemoveRange(assignments);

    db.WorkspaceMembers.Remove(member);
    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
