export type TextSegment = { kind: 'text'; text: string } | { kind: 'mention'; text: string };

// Same rule as the backend's MentionParser: "@handle" at the start or after a non-word character.
const MENTION = /(?<![\w@])@[A-Za-z0-9][A-Za-z0-9._-]{0,29}/g;

/** Splits comment text into plain and `@mention` segments, so mentions can be highlighted without HTML injection. */
export function splitMentions(text: string): TextSegment[] {
  const segments: TextSegment[] = [];
  let cursor = 0;
  for (const match of text.matchAll(MENTION)) {
    if (match.index > cursor) {
      segments.push({ kind: 'text', text: text.slice(cursor, match.index) });
    }
    segments.push({ kind: 'mention', text: match[0] });
    cursor = match.index + match[0].length;
  }
  if (cursor < text.length) {
    segments.push({ kind: 'text', text: text.slice(cursor) });
  }
  return segments;
}
