# TaskFlow – conventions and decisions

This file is the contract between backend and frontend. When something here changes, change it first.

## Naming

| Area | Convention |
| --- | --- |
| C# | 2-space indent, file-scoped namespaces, braces on every block, early returns, `== false` instead of `!`, primary constructors, expression-bodied members where a single expression fits |
| Nullable | enabled in Domain/Application/Infrastructure/tests, disabled in `TaskFlow.Api` (request contracts opt in with `#nullable enable`) |
| Commands / queries | `VerbNounCommand`, `GetNounQuery`, handler in the same file as its request and validator (one folder per feature) |
| Migrations | descriptive PascalCase phrase, e.g. `CreateInitialSchema` |
| Routes | `api/v1/...`, lower-case plural nouns, nested only for creation/listing (`boards/{boardId}/columns`), flat for item access (`columns/{id}`) |
| JSON | camelCase, enums as strings (`"Owner"`), GUID ids, UTC ISO-8601 timestamps |
| Frontend | feature folders (`features/auth|boards|cards|notifications`), `shared/` for ui, lib, hooks. One hook per endpoint (`useMoveCard`, `useBoard`, ...). Components PascalCase, hooks `useXxx`, tests next to the file as `*.test.ts(x)` |

## Position format (ordering)

* `Position` is a `double`. Lower = earlier. Cards are ordered inside a column, columns inside a board.
* Gap between neighbours when appending: `1024`. Inserting between two items takes the midpoint. Inserting at the very top uses `first - 1024` (positions may become negative), at the very bottom `last + 1024`.
* When the gap between two neighbours drops below `1e-6` the server **rebalances** the container: all items are renumbered `1024, 2048, ...` in their current order, then the position is recomputed. The client never needs to know.
* **The API accepts a target slot as neighbour ids, not as a number or an index.** A move request carries
  `afterId` (the item that will be directly above/left of the moved item) and `beforeId` (directly below/right). Either may be `null`.
  * both `null`: append to the end (also the only valid form for an empty column)
  * only `beforeId`: place directly **before** that item (at the very start when it is the first item)
  * only `afterId`: place directly **after** that item (at the very end when it is the last item). The other neighbour is derived from the current order.
  * both: place between them. The server returns `400` when a neighbour is not in the target container (`position.neighbor_not_found`) or the two are in the wrong order (`position.neighbors_out_of_order`) - a stale client.
  * the moved item itself is never a valid neighbour.
* **Ties.** Nothing serializes two requests that append to the same column at the same moment, so two items can end up with the same position. This is tolerated on purpose: every ordering is `(position, id)`, so the order is deterministic everywhere, and the next move that touches the tied neighbours rebalances the container instead of failing. (Serializing would need a lock per board or a DB-level sequence; both cost more than they are worth here and are documented as a limitation.)
* The move response carries `rebalanced: true` when siblings were renumbered. Clients then refetch the container, because other items' positions changed.
* Why ids: the client's view of positions may be stale, ids are stable. The server derives the number, so rebalancing is invisible and positions never leak into client logic.
  The response of a move contains the authoritative `position` and new `version`.
* Pure implementation: `TaskFlow.Application.Ordering.PositionService` (backend) and `shared/lib/position.ts` (frontend, same algorithm used for the optimistic update).

## Concurrency

* `Card.Version` (int, starts at 1) is an EF concurrency token and is incremented on **every** mutation of a card (update, move, labels, assignees).
* Mutating requests carry `expectedVersion`. A mismatch returns `409 Conflict` (ProblemDetails, `code = "card.version_conflict"`). A race that slips past the check is caught by the EF token (`DbUpdateConcurrencyException`) and is mapped to the same `409`.

## Error shape

Every non-2xx response is `application/problem+json` (RFC 9457):

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Conflict",
  "status": 409,
  "detail": "The card was modified by someone else.",
  "code": "card.version_conflict",
  "traceId": "00-..."
}
```

Validation failures use `status = 400` and add `"errors": { "title": ["..."] }` (keys are camelCase property names). Business errors are returned through the `Result<T>` pattern and mapped by `ErrorType`:

| ErrorType | Status |
| --- | --- |
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |

A user who is **not a member** of a workspace gets `404` for anything inside it (existence is not leaked). A member who lacks the role gets `403`.

## Auth

* Access token: JWT, 15 minutes. Refresh token: opaque random value, 14 days, stored hashed, **rotated on every use**. Re-using a rotated token revokes the whole token family.
* The SPA keeps both tokens in a persisted Zustand store (known trade-off, see README limitations). SignalR passes the access token via the `access_token` query string.

## Authorization

Requests that touch a resource implement `IAuthorizedRequest` (scope = workspace/board/column/card + minimum role). A MediatR pipeline behaviour resolves the owning workspace, checks membership and role, and short-circuits with `NotFound`/`Forbidden`.

| Action | Minimum role |
| --- | --- |
| Read/modify boards content (columns, cards, comments, labels), create boards | Member |
| Update workspace, invite / change role / remove members, archive & delete boards | Admin |
| Delete workspace, promote to Owner | Owner (ownership transfer is not implemented) |

Comments: only the author edits; author **or** Admin/Owner deletes.

## Realtime (SignalR, hub at `/hubs/board`)

Client → server: `JoinBoard(boardId)` (membership is verified, otherwise `HubException`), `LeaveBoard(boardId)`.
Server → client events (group = `board:{boardId}`). **Every payload contains `boardId` and `actorUserId`**; clients ignore events whose `actorUserId` is their own.

| Event | Payload |
| --- | --- |
| `CardCreated` | `{ boardId, actorUserId, card: CardDto }` |
| `CardUpdated` | `{ boardId, actorUserId, card: CardDto }` |
| `CardMoved` | `{ boardId, actorUserId, cardId, fromColumnId, toColumnId, position, version }` |
| `CardDeleted` | `{ boardId, actorUserId, cardId, columnId }` |
| `CommentAdded` | `{ boardId, actorUserId, cardId, comment: CommentDto }` |
| `ColumnsChanged` | `{ boardId, actorUserId }` (create / rename / reorder / delete of columns: clients refetch the board) |

Events are emitted from handlers through `IBoardNotifier` (Application layer), implemented with `IHubContext` in the Api project.

## Mentions

Users have a unique `handle`. `@handle` in a comment notifies that user if they are a member of the workspace. Assigning a card to someone other than yourself also notifies them.

## Pagination

`GET` list endpoints that are paged accept `page` (1-based) and `pageSize` (max 200) and return `PagedResult<T>`: `{ items, page, pageSize, totalCount, totalPages }`.
