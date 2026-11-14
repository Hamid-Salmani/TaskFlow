using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Workspaces;

namespace TaskFlow.Api.Controllers;

[Route($"{ApiRoutes.Base}/workspaces")]
public sealed class WorkspacesController(ISender sender) : ApiControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<WorkspaceDto>>> List() =>
    Respond(await sender.Send(new ListWorkspacesQuery()));

  [HttpPost]
  [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<WorkspaceDto>> Create(CreateWorkspaceRequest request) =>
    RespondCreated(await sender.Send(new CreateWorkspaceCommand(request.Name, request.Description)));

  [HttpGet("{workspaceId:guid}")]
  public async Task<ActionResult<WorkspaceDto>> Get(Guid workspaceId) =>
    Respond(await sender.Send(new GetWorkspaceQuery(workspaceId)));

  [HttpPut("{workspaceId:guid}")]
  public async Task<ActionResult<WorkspaceDto>> Update(Guid workspaceId, UpdateWorkspaceRequest request) =>
    Respond(await sender.Send(new UpdateWorkspaceCommand(workspaceId, request.Name, request.Description)));

  [HttpDelete("{workspaceId:guid}")]
  public async Task<ActionResult> Delete(Guid workspaceId) =>
    Respond(await sender.Send(new DeleteWorkspaceCommand(workspaceId)));

  [HttpGet("{workspaceId:guid}/members")]
  public async Task<ActionResult<List<WorkspaceMemberDto>>> ListMembers(Guid workspaceId) =>
    Respond(await sender.Send(new ListMembersQuery(workspaceId)));

  [HttpPost("{workspaceId:guid}/members")]
  [ProducesResponseType(typeof(WorkspaceMemberDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<WorkspaceMemberDto>> InviteMember(Guid workspaceId, InviteMemberRequest request) =>
    RespondCreated(await sender.Send(new InviteMemberCommand(workspaceId, request.Email, request.Role)));

  [HttpPut("{workspaceId:guid}/members/{userId:guid}")]
  public async Task<ActionResult<WorkspaceMemberDto>> ChangeMemberRole(Guid workspaceId, Guid userId, ChangeMemberRoleRequest request) =>
    Respond(await sender.Send(new ChangeMemberRoleCommand(workspaceId, userId, request.Role)));

  [HttpDelete("{workspaceId:guid}/members/{userId:guid}")]
  public async Task<ActionResult> RemoveMember(Guid workspaceId, Guid userId) =>
    Respond(await sender.Send(new RemoveMemberCommand(workspaceId, userId)));

  [HttpGet("{workspaceId:guid}/boards")]
  public async Task<ActionResult<List<BoardSummaryDto>>> ListBoards(Guid workspaceId, [FromQuery] bool includeArchived = false) =>
    Respond(await sender.Send(new Application.Features.Boards.ListBoardsQuery(workspaceId, includeArchived)));

  [HttpPost("{workspaceId:guid}/boards")]
  [ProducesResponseType(typeof(BoardSummaryDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<BoardSummaryDto>> CreateBoard(Guid workspaceId, CreateBoardRequest request) =>
    RespondCreated(await sender.Send(new Application.Features.Boards.CreateBoardCommand(workspaceId, request.Name, request.Description)));
}
