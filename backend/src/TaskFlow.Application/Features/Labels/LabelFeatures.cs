using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Labels;

internal static class LabelRules
{
  public const string ColorPattern = "^#[0-9a-fA-F]{6}$";
}

public sealed record CreateLabelCommand(Guid BoardId, string Name, string Color) : IRequest<Result<LabelDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class CreateLabelCommandValidator : AbstractValidator<CreateLabelCommand>
{
  public CreateLabelCommandValidator()
  {
    RuleFor(a => a.Name).NotEmpty().MaximumLength(50);
    RuleFor(a => a.Color).Matches(LabelRules.ColorPattern).WithMessage("Color must be a hex value like #3b82f6.");
  }
}

public sealed class CreateLabelCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
  public async Task<Result<LabelDto>> Handle(CreateLabelCommand request, CancellationToken cancellationToken)
  {
    if (await BoardGuard.CheckEditableAsync(db, request.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var name = request.Name.Trim();
    if (await db.Labels.AnyAsync(a => a.BoardId == request.BoardId && a.Name == name, cancellationToken))
    {
      return Error.Conflict("label.name_taken", "A label with this name already exists on the board.");
    }

    var label = new Label { BoardId = request.BoardId, Name = name, Color = request.Color.ToLowerInvariant() };
    db.Labels.Add(label);
    await db.SaveChangesAsync(cancellationToken);
    return new LabelDto(label.Id, label.BoardId, label.Name, label.Color);
  }
}

public sealed record UpdateLabelCommand(Guid LabelId, string Name, string Color) : IRequest<Result<LabelDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Label(LabelId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class UpdateLabelCommandValidator : AbstractValidator<UpdateLabelCommand>
{
  public UpdateLabelCommandValidator()
  {
    RuleFor(a => a.Name).NotEmpty().MaximumLength(50);
    RuleFor(a => a.Color).Matches(LabelRules.ColorPattern).WithMessage("Color must be a hex value like #3b82f6.");
  }
}

public sealed class UpdateLabelCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateLabelCommand, Result<LabelDto>>
{
  public async Task<Result<LabelDto>> Handle(UpdateLabelCommand request, CancellationToken cancellationToken)
  {
    var label = await db.Labels.FirstAsync(a => a.Id == request.LabelId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, label.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var name = request.Name.Trim();
    if (await db.Labels.AnyAsync(a => a.BoardId == label.BoardId && a.Name == name && a.Id != label.Id, cancellationToken))
    {
      return Error.Conflict("label.name_taken", "A label with this name already exists on the board.");
    }

    label.Name = name;
    label.Color = request.Color.ToLowerInvariant();
    await db.SaveChangesAsync(cancellationToken);
    return new LabelDto(label.Id, label.BoardId, label.Name, label.Color);
  }
}

/// <summary>Removing a label detaches it from every card, so it is an admin action.</summary>
public sealed record DeleteLabelCommand(Guid LabelId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Label(LabelId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class DeleteLabelCommandHandler(IApplicationDbContext db, TimeProvider timeProvider)
  : IRequestHandler<DeleteLabelCommand, Result>
{
  public async Task<Result> Handle(DeleteLabelCommand request, CancellationToken cancellationToken)
  {
    var label = await db.Labels.FirstAsync(a => a.Id == request.LabelId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, label.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    db.CardLabels.RemoveRange(await db.CardLabels.Where(a => a.LabelId == label.Id).ToListAsync(cancellationToken));
    label.SoftDelete(timeProvider.GetUtcNow().UtcDateTime);
    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
