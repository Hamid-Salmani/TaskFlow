using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Ordering;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Persistence;

/// <summary>Creates a small but realistic data set. Only called in the Development environment.</summary>
public sealed class DemoDataSeeder(ApplicationDbContext db, UserManager<User> userManager, TimeProvider timeProvider)
{
  public const string DemoPassword = "Password123";

  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    if (await db.Users.AnyAsync(cancellationToken))
    {
      return;
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var demo = await CreateUserAsync("demo@taskflow.dev", "Demo User");
    var alice = await CreateUserAsync("alice@taskflow.dev", "Alice Johnson");
    var bob = await CreateUserAsync("bob@taskflow.dev", "Bob Martinez");

    var workspace = new Workspace { Name = "Acme Product Team", Description = "Everything the product team ships." };
    workspace.Members.Add(new WorkspaceMember { UserId = demo.Id, Role = WorkspaceRole.Owner, JoinedAt = now });
    workspace.Members.Add(new WorkspaceMember { UserId = alice.Id, Role = WorkspaceRole.Admin, JoinedAt = now });
    workspace.Members.Add(new WorkspaceMember { UserId = bob.Id, Role = WorkspaceRole.Member, JoinedAt = now });

    var board = new Board { Workspace = workspace, Name = "Website Redesign", Description = "Q4 marketing site refresh." };
    var columns = new[] { "Backlog", "In progress", "Review", "Done" }
      .Select((title, index) => new Column { Board = board, Title = title, Position = (index + 1) * PositionService.Step })
      .ToList();

    var labels = new[]
    {
      new Label { Board = board, Name = "Bug", Color = "#ef4444" },
      new Label { Board = board, Name = "Feature", Color = "#3b82f6" },
      new Label { Board = board, Name = "Design", Color = "#a855f7" },
      new Label { Board = board, Name = "Urgent", Color = "#f97316" }
    };

    var specs = new (int Column, string Title, string? Description, int Label, User? Assignee, int? DueInDays)[]
    {
      (0, "Audit existing landing pages", "List every page, its traffic and its conversion rate.", 2, null, 14),
      (0, "Define new design tokens", "Colors, spacing and typography scale for the new brand.", 2, alice, 10),
      (0, "Write case study copy", null, 1, null, null),
      (1, "Build responsive hero section", "Needs to work from 320px up to ultra-wide.", 1, demo, 3),
      (1, "Fix navigation overflow on mobile", "The menu overlaps the logo under 360px.", 0, bob, -2),
      (1, "Set up analytics events", null, 1, alice, 5),
      (2, "Review pricing page layout", "Compare against the approved mockups.", 2, demo, 1),
      (2, "Accessibility pass on forms", "Labels, focus order, error announcements.", 3, bob, 2),
      (3, "Choose CMS provider", null, 1, demo, null),
      (3, "Migrate blog posts", "42 posts migrated, redirects verified.", 1, alice, null)
    };

    var cards = new List<Card>();
    foreach (var group in specs.GroupBy(a => a.Column))
    {
      var position = 0d;
      foreach (var spec in group)
      {
        position += PositionService.Step;
        var card = new Card
        {
          Board = board,
          Column = columns[spec.Column],
          Title = spec.Title,
          Description = spec.Description,
          Position = position,
          DueDate = spec.DueInDays is null ? null : now.Date.AddDays(spec.DueInDays.Value),
          CreatedById = demo.Id
        };
        card.Labels.Add(new CardLabel { Label = labels[spec.Label] });
        if (spec.Assignee is not null)
        {
          card.Assignees.Add(new CardAssignee { UserId = spec.Assignee.Id, AssignedAt = now });
        }

        cards.Add(card);
      }
    }

    cards[3].Comments.Add(new CardComment { Author = alice, Body = "Looks great so far! @demo can you share a preview link?", CreatedAt = now.AddHours(-5) });
    cards[3].Comments.Add(new CardComment { Author = demo, Body = "Preview is on staging, I'll tidy the hero image tomorrow.", CreatedAt = now.AddHours(-3) });
    cards[4].Comments.Add(new CardComment { Author = demo, Body = "@bob this one is blocking the release.", CreatedAt = now.AddHours(-1) });

    db.Workspaces.Add(workspace);
    db.Cards.AddRange(cards);
    db.ActivityLogs.Add(new ActivityLog
    {
      BoardId = board.Id,
      UserId = demo.Id,
      Type = ActivityType.BoardCreated,
      Message = "created the board 'Website Redesign'",
      CreatedAt = now
    });
    db.Notifications.Add(new Notification
    {
      UserId = demo.Id,
      ActorId = alice.Id,
      Type = NotificationType.Mentioned,
      Message = "Alice Johnson mentioned you on 'Build responsive hero section'",
      BoardId = board.Id,
      CardId = cards[3].Id,
      CreatedAt = now.AddHours(-5)
    });

    await db.SaveChangesAsync(cancellationToken);
  }

  private async Task<User> CreateUserAsync(string email, string displayName)
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      UserName = email,
      Email = email,
      EmailConfirmed = true,
      DisplayName = displayName,
      Handle = email.Split('@')[0],
      CreatedAt = timeProvider.GetUtcNow().UtcDateTime
    };

    var result = await userManager.CreateAsync(user, DemoPassword);
    if (result.Succeeded == false)
    {
      throw new InvalidOperationException($"Could not seed user {email}: {string.Join(", ", result.Errors.Select(a => a.Description))}");
    }

    return user;
  }
}
