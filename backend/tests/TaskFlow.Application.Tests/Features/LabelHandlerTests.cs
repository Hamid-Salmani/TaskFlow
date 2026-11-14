using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Labels;
using TaskFlow.Application.Tests.Support;

namespace TaskFlow.Application.Tests.Features;

public sealed class LabelHandlerTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;

  public LabelHandlerTests() => _world = _fixture.CreateWorld();

  public void Dispose() => _fixture.Dispose();

  [Fact]
  public async Task Create_NormalizesTheColor()
  {
    var result = await new CreateLabelCommandHandler(_fixture.Db).Handle(new CreateLabelCommand(_world.Board.Id, " Urgent ", "#FF00AA"), default);

    result.Value.Should().BeEquivalentTo(new { Name = "Urgent", Color = "#ff00aa", BoardId = _world.Board.Id });
  }

  [Fact]
  public async Task Create_RejectsDuplicateNamesOnTheSameBoardOnly()
  {
    var handler = new CreateLabelCommandHandler(_fixture.Db);
    var other = _fixture.Add(new BoardBuilder(_world.Workspace).Build());

    var duplicate = await handler.Handle(new CreateLabelCommand(_world.Board.Id, "Bug", "#000000"), default);
    var elsewhere = await handler.Handle(new CreateLabelCommand(other.Id, "Bug", "#000000"), default);

    duplicate.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Conflict, Code = "label.name_taken" });
    elsewhere.IsSuccess.Should().BeTrue();
  }

  [Theory]
  [InlineData("red")]
  [InlineData("#12345")]
  [InlineData("#1234567")]
  [InlineData("#gggggg")]
  [InlineData("")]
  public void Validator_RejectsMalformedColors(string color)
  {
    new CreateLabelCommandValidator().Validate(new CreateLabelCommand(Guid.NewGuid(), "x", color)).IsValid.Should().BeFalse();
    new UpdateLabelCommandValidator().Validate(new UpdateLabelCommand(Guid.NewGuid(), "x", color)).IsValid.Should().BeFalse();
  }

  [Fact]
  public async Task Update_ChangesNameAndColorButCanKeepItsOwnName()
  {
    var handler = new UpdateLabelCommandHandler(_fixture.Db);

    var recolor = await handler.Handle(new UpdateLabelCommand(_world.Bug.Id, "Bug", "#111111"), default);
    var clash = await handler.Handle(new UpdateLabelCommand(_world.Bug.Id, "Feature", "#111111"), default);

    recolor.Value.Color.Should().Be("#111111");
    clash.Error!.Code.Should().Be("label.name_taken");
  }

  [Fact]
  public async Task Delete_DetachesTheLabelFromCards()
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Labeled(_world.Bug).Labeled(_world.Feature).Build());

    var result = await new DeleteLabelCommandHandler(_fixture.Db, _fixture.Time).Handle(new DeleteLabelCommand(_world.Bug.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    (await _fixture.Db.Labels.Select(a => a.Id).ToListAsync()).Should().Equal(_world.Feature.Id);
    (await _fixture.Db.CardLabels.Where(a => a.CardId == card.Id).Select(a => a.LabelId).ToListAsync()).Should().Equal(_world.Feature.Id);
  }

  [Fact]
  public async Task AllMutationsAreRejectedOnArchivedBoards()
  {
    _world.Board.IsArchived = true;
    _fixture.Db.SaveChanges();

    (await new CreateLabelCommandHandler(_fixture.Db).Handle(new CreateLabelCommand(_world.Board.Id, "New", "#000000"), default)).Error!.Code.Should().Be("board.archived");
    (await new UpdateLabelCommandHandler(_fixture.Db).Handle(new UpdateLabelCommand(_world.Bug.Id, "x", "#000000"), default)).Error!.Code.Should().Be("board.archived");
    (await new DeleteLabelCommandHandler(_fixture.Db, _fixture.Time).Handle(new DeleteLabelCommand(_world.Bug.Id), default)).Error!.Code.Should().Be("board.archived");
  }
}
