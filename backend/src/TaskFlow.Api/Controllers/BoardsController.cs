using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Boards;

namespace TaskFlow.Api.Controllers;

[Route($"{ApiRoutes.Base}/boards")]
public sealed class BoardsController(ISender sender) : ApiControllerBase
{
  [HttpGet("{boardId:guid}")]
  public async Task<ActionResult<BoardDetailDto>> Get(Guid boardId) =>
    Respond(await sender.Send(new GetBoardQuery(boardId)));

  [HttpPut("{boardId:guid}")]
  public async Task<ActionResult<BoardSummaryDto>> Update(Guid boardId, UpdateBoardRequest request) =>
    Respond(await sender.Send(new UpdateBoardCommand(boardId, request.Name, request.Description)));

  [HttpPut("{boardId:guid}/archive")]
  public async Task<ActionResult<BoardSummaryDto>> Archive(Guid boardId, ArchiveBoardRequest request) =>
    Respond(await sender.Send(new ArchiveBoardCommand(boardId, request.Archived)));

  [HttpDelete("{boardId:guid}")]
  public async Task<ActionResult> Delete(Guid boardId) =>
    Respond(await sender.Send(new DeleteBoardCommand(boardId)));
}
