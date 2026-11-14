using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Labels;

namespace TaskFlow.Api.Controllers;

[Route(ApiRoutes.Base)]
public sealed class LabelsController(ISender sender) : ApiControllerBase
{
  [HttpPost("boards/{boardId:guid}/labels")]
  [ProducesResponseType(typeof(LabelDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<LabelDto>> Create(Guid boardId, LabelRequest request) =>
    RespondCreated(await sender.Send(new CreateLabelCommand(boardId, request.Name, request.Color)));

  [HttpPut("labels/{labelId:guid}")]
  public async Task<ActionResult<LabelDto>> Update(Guid labelId, LabelRequest request) =>
    Respond(await sender.Send(new UpdateLabelCommand(labelId, request.Name, request.Color)));

  [HttpDelete("labels/{labelId:guid}")]
  public async Task<ActionResult> Delete(Guid labelId) =>
    Respond(await sender.Send(new DeleteLabelCommand(labelId)));
}
