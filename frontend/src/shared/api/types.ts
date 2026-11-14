import type { components } from './schema';

/** Friendly aliases for the generated OpenAPI schemas. Regenerate with `npm run api:sync`. */
type Schemas = components['schemas'];

export type AuthResponse = Schemas['AuthResponse'];
export type UserDto = Schemas['UserDto'];
export type RegisterRequest = Schemas['RegisterRequest'];
export type LoginRequest = Schemas['LoginRequest'];

export type WorkspaceRole = Schemas['WorkspaceRole'];
export type WorkspaceDto = Schemas['WorkspaceDto'];
export type WorkspaceMemberDto = Schemas['WorkspaceMemberDto'];
export type CreateWorkspaceRequest = Schemas['CreateWorkspaceRequest'];
export type UpdateWorkspaceRequest = Schemas['UpdateWorkspaceRequest'];
export type InviteMemberRequest = Schemas['InviteMemberRequest'];
export type ChangeMemberRoleRequest = Schemas['ChangeMemberRoleRequest'];

export type BoardSummaryDto = Schemas['BoardSummaryDto'];
export type BoardDetailDto = Schemas['BoardDetailDto'];
export type ColumnDto = Schemas['ColumnDto'];
export type LabelDto = Schemas['LabelDto'];
export type CreateBoardRequest = Schemas['CreateBoardRequest'];
export type UpdateBoardRequest = Schemas['UpdateBoardRequest'];

export type CardDto = Schemas['CardDto'];
export type CardPage = Schemas['CardDtoPagedResult'];
export type MoveCardResultDto = Schemas['MoveCardResultDto'];
export type MoveColumnResultDto = Schemas['MoveColumnResultDto'];
export type CreateCardRequest = Schemas['CreateCardRequest'];
export type UpdateCardRequest = Schemas['UpdateCardRequest'];
export type MoveCardRequest = Schemas['MoveCardRequest'];
export type SetCardLabelsRequest = Schemas['SetCardLabelsRequest'];
export type SetCardAssigneesRequest = Schemas['SetCardAssigneesRequest'];
export type CreateColumnRequest = Schemas['CreateColumnRequest'];
export type UpdateColumnRequest = Schemas['UpdateColumnRequest'];
export type MoveColumnRequest = Schemas['MoveColumnRequest'];
export type LabelRequest = Schemas['LabelRequest'];

export type CommentDto = Schemas['CommentDto'];
export type CommentPage = Schemas['CommentDtoPagedResult'];
export type ActivityDto = Schemas['ActivityDto'];
export type ActivityPage = Schemas['ActivityDtoPagedResult'];
export type ActivityType = Schemas['ActivityType'];
export type NotificationDto = Schemas['NotificationDto'];
export type NotificationPage = Schemas['NotificationDtoPagedResult'];
export type UnreadCountDto = Schemas['UnreadCountDto'];
