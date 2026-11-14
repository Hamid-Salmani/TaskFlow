import { splitMentions } from './mentions';

describe('splitMentions', () => {
  it('marks @handles and keeps the surrounding text', () => {
    expect(splitMentions('hi @bob, see this')).toEqual([
      { kind: 'text', text: 'hi ' },
      { kind: 'mention', text: '@bob' },
      { kind: 'text', text: ', see this' },
    ]);
  });

  it('handles a mention at the start and several mentions', () => {
    expect(splitMentions('@a1 and @b.c')).toEqual([
      { kind: 'mention', text: '@a1' },
      { kind: 'text', text: ' and ' },
      { kind: 'mention', text: '@b.c' },
    ]);
  });

  it('does not treat e-mail addresses as mentions', () => {
    expect(splitMentions('mail ada@test.dev')).toEqual([
      { kind: 'text', text: 'mail ada@test.dev' },
    ]);
  });

  it('returns nothing for an empty string', () => {
    expect(splitMentions('')).toEqual([]);
  });
});
