using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Cards;
using TaskFlow.Application.Features.Columns;

namespace TaskFlow.Api.Controllers;

[Route(ApiRoutes.Base)]
public sealed class ColumnsController(ISender sender) : ApiControllerBase
{
  [HttpPost("boards/{boardId:guid}/columns")]
  [ProducesResponseType(typeof(ColumnDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<ColumnDto>> Create(Guid boardId, CreateColumnRequest request) =>
    RespondCreated(await sender.Send(new CreateColumnCommand(boardId, request.Title)));

  [HttpPut("columns/{columnId:guid}")]
  public async Task<ActionResult<ColumnDto>> Rename(Guid columnId, UpdateColumnRequest request) =>
    Respond(await sender.Send(new RenameColumnCommand(columnId, request.Title)));

  [HttpPut("columns/{columnId:guid}/move")]
  public async Task<ActionResult<MoveColumnResultDto>> Move(Guid columnId, MoveColumnRequest request) =>
    Respond(await sender.Send(new MoveColumnCommand(columnId, request.AfterId, request.BeforeId)));

  [HttpDelete("columns/{columnId:guid}")]
  public async Task<ActionResult> Delete(Guid columnId) =>
    Respond(await sender.Send(new DeleteColumnCommand(columnId)));

  [HttpPost("columns/{columnId:guid}/cards")]
  [ProducesResponseType(typeof(CardDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<CardDto>> CreateCard(Guid columnId, CreateCardRequest request) =>
    RespondCreated(await sender.Send(new CreateCardCommand(columnId, request.Title, request.Description, request.DueDate)));
}
