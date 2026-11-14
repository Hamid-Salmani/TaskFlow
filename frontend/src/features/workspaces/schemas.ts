import { z } from 'zod';

export const nameDescriptionSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Name is required')
    .max(100, 'Name must be at most 100 characters'),
  description: z.string().trim().max(1000, 'Description must be at most 1000 characters'),
});
export type NameDescriptionValues = z.infer<typeof nameDescriptionSchema>;

export const inviteMemberSchema = z.object({
  email: z.string().trim().min(1, 'Email is required').email('Enter a valid email address'),
  role: z.enum(['Member', 'Admin']),
});
export type InviteMemberValues = z.infer<typeof inviteMemberSchema>;
