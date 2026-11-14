using System.Text.RegularExpressions;

namespace TaskFlow.Application.Common;

/// <summary>Finds <c>@handle</c> mentions in comment text. Pure, so it is easy to test exhaustively.</summary>
public static partial class MentionParser
{
  // A mention starts at the beginning or after a character that cannot be part of a word, so "a@b.com" is not a mention.
  [GeneratedRegex(@"(?<![\w@])@([A-Za-z0-9][A-Za-z0-9._-]{0,29})", RegexOptions.CultureInvariant)]
  private static partial Regex MentionPattern();

  /// <summary>
  /// Lower-cased handles mentioned in <paramref name="text"/>, without duplicates. Trailing punctuation that is valid
  /// inside a handle ("thanks @bob.") is offered as an extra candidate, so both "bob." and "bob" are returned and the
  /// caller keeps whichever matches a real member.
  /// </summary>
  public static IReadOnlyCollection<string> Parse(string text)
  {
    var handles = new HashSet<string>(StringComparer.Ordinal);
    foreach (Match match in MentionPattern().Matches(text))
    {
      var token = match.Groups[1].Value.ToLowerInvariant();
      handles.Add(token);

      var trimmed = token.TrimEnd('.', '-', '_');
      if (trimmed.Length > 0)
      {
        handles.Add(trimmed);
      }
    }

    return handles;
  }
}
