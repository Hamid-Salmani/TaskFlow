using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Cards;

namespace TaskFlow.Api.Controllers;

[Route(ApiRoutes.Base)]
public sealed class CardsController(ISender sender) : ApiControllerBase
{
  /// <summary>Filters, sorts and pages the cards of a board.</summary>
  [HttpGet("boards/{boardId:guid}/cards")]
  public async Task<ActionResult<PagedResult<CardDto>>> Search(Guid boardId, [FromQuery] CardSearchParameters query) =>
    Respond(await sender.Send(new SearchCardsQuery(
      boardId,
      query.LabelId,
      query.AssigneeId,
      query.HasDueDate,
      query.DueFrom,
      query.DueTo,
      query.Q,
      query.SortBy,
      query.SortDirection,
      query.Page,
      query.PageSize)));

  [HttpGet("cards/{cardId:guid}")]
  public async Task<ActionResult<CardDto>> Get(Guid cardId) =>
    Respond(await sender.Send(new GetCardQuery(cardId)));

  [HttpPut("cards/{cardId:guid}")]
  public async Task<ActionResult<CardDto>> Update(Guid cardId, UpdateCardRequest request) =>
    Respond(await sender.Send(new UpdateCardCommand(cardId, request.ExpectedVersion, request.Title, request.Description, request.DueDate)));

  /// <summary>Moves or reorders a card. The target slot is given as neighbour ids (see NOTES.md).</summary>
  [HttpPut("cards/{cardId:guid}/move")]
  public async Task<ActionResult<MoveCardResultDto>> Move(Guid cardId, MoveCardRequest request) =>
    Respond(await sender.Send(new MoveCardCommand(cardId, request.ExpectedVersion, request.TargetColumnId, request.AfterId, request.BeforeId)));

  [HttpPut("cards/{cardId:guid}/labels")]
  public async Task<ActionResult<CardDto>> SetLabels(Guid cardId, SetCardLabelsRequest request) =>
    Respond(await sender.Send(new SetCardLabelsCommand(cardId, request.ExpectedVersion, request.LabelIds)));

  [HttpPut("cards/{cardId:guid}/assignees")]
  public async Task<ActionResult<CardDto>> SetAssignees(Guid cardId, SetCardAssigneesRequest request) =>
    Respond(await sender.Send(new SetCardAssigneesCommand(cardId, request.ExpectedVersion, request.UserIds)));

  [HttpDelete("cards/{cardId:guid}")]
  public async Task<ActionResult> Delete(Guid cardId) =>
    Respond(await sender.Send(new DeleteCardCommand(cardId)));
}
