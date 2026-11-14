using FluentAssertions;
using TaskFlow.Application.Common;

namespace TaskFlow.Application.Tests.Features;

public sealed class MentionParserTests
{
  [Theory]
  [InlineData("hi @bob", "bob")]
  [InlineData("@bob first", "bob")]
  [InlineData("cc (@bob)", "bob")]
  [InlineData("Hello @BOB!", "bob")]
  [InlineData("ping @ada.lovelace now", "ada.lovelace")]
  [InlineData("line one\n@bob", "bob")]
  public void FindsMentions(string text, string expected) =>
    MentionParser.Parse(text).Should().Contain(expected);

  [Theory]
  [InlineData("write to ada@test.dev")]
  [InlineData("no mention here")]
  [InlineData("a lone @ sign")]
  [InlineData("@@double")]
  [InlineData("")]
  public void IgnoresTextThatIsNotAMention(string text) =>
    MentionParser.Parse(text).Should().BeEmpty();

  [Fact]
  public void ReturnsEachHandleOnce_AndOffersTheVersionWithoutTrailingPunctuation()
  {
    var handles = MentionParser.Parse("@bob and @bob again, thanks @carol.");

    handles.Should().BeEquivalentTo(new[] { "bob", "carol.", "carol" });
  }

  [Fact]
  public void CapsTheLengthOfAHandle() =>
    MentionParser.Parse("@" + new string('a', 80)).Single().Should().HaveLength(30);
}
