import { z } from 'zod';

const id = z.string().min(1);

const cardSchema = z.object({
  id,
  boardId: id,
  columnId: id,
  title: z.string(),
  description: z.string().nullish(),
  position: z.number(),
  dueDate: z.string().nullish(),
  version: z.number(),
  labelIds: z.array(id),
  assigneeIds: z.array(id),
  commentCount: z.number(),
  createdAt: z.string(),
  updatedAt: z.string().nullish(),
});

const base = { boardId: id, actorUserId: id };

/** Wire format of the server's board events (see NOTES.md). Unknown or malformed payloads are dropped, never trusted. */
export const boardEventSchemas = {
  CardCreated: z.object({ ...base, card: cardSchema }),
  CardUpdated: z.object({ ...base, card: cardSchema }),
  CardMoved: z.object({
    ...base,
    cardId: id,
    fromColumnId: id,
    toColumnId: id,
    position: z.number(),
    version: z.number(),
    rebalanced: z.boolean(),
  }),
  CardDeleted: z.object({ ...base, cardId: id, columnId: id }),
  CommentAdded: z.object({ ...base, cardId: id, comment: z.object({ id }).passthrough() }),
  CommentDeleted: z.object({ ...base, cardId: id, commentId: id }),
  ColumnsChanged: z.object(base),
} as const;

export type BoardEventName = keyof typeof boardEventSchemas;

export const BOARD_EVENT_NAMES = Object.keys(boardEventSchemas).filter(
  (name): name is BoardEventName => name in boardEventSchemas,
);

export type BoardEvent = {
  [Name in BoardEventName]: { name: Name; payload: z.infer<(typeof boardEventSchemas)[Name]> };
}[BoardEventName];

export function parseBoardEvent(name: BoardEventName, payload: unknown): BoardEvent | undefined {
  switch (name) {
    case 'CardCreated':
    case 'CardUpdated': {
      const parsed = boardEventSchemas[name].safeParse(payload);
      return parsed.success ? { name, payload: parsed.data } : undefined;
    }
    case 'CardMoved': {
      const parsed = boardEventSchemas.CardMoved.safeParse(payload);
      return parsed.success ? { name, payload: parsed.data } : undefined;
    }
    case 'CardDeleted': {
      const parsed = boardEventSchemas.CardDeleted.safeParse(payload);
      return parsed.success ? { name, payload: parsed.data } : undefined;
    }
    case 'CommentAdded': {
      const parsed = boardEventSchemas.CommentAdded.safeParse(payload);
      return parsed.success ? { name, payload: parsed.data } : undefined;
    }
    case 'CommentDeleted': {
      const parsed = boardEventSchemas.CommentDeleted.safeParse(payload);
      return parsed.success ? { name, payload: parsed.data } : undefined;
    }
    case 'ColumnsChanged': {
      const parsed = boardEventSchemas.ColumnsChanged.safeParse(payload);
      return parsed.success ? { name, payload: parsed.data } : undefined;
    }
  }
}
