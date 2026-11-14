using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.Support;

public static class TestJson
{
  public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonStringEnumConverter() }
  };
}

/// <summary>Shape of every error response (see NOTES.md). Extension members are flattened onto the root object.</summary>
public sealed record ProblemBody(int Status, string Title, string? Detail, string? Code, Dictionary<string, string[]>? Errors);

public static class HttpExtensions
{
  public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
    client.PostAsJsonAsync(url, body, TestJson.Options);

  public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
    client.PutAsJsonAsync(url, body, TestJson.Options);

  public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
  {
    var value = await response.Content.ReadFromJsonAsync<T>(TestJson.Options);
    return value ?? throw new InvalidOperationException("Empty response body.");
  }

  public static Task<ProblemBody> ReadProblemAsync(this HttpResponseMessage response) => response.ReadAsAsync<ProblemBody>();
}

/// <summary>A registered user together with an authenticated client.</summary>
public sealed class TestUser(HttpClient client, AuthResponse auth)
{
  public HttpClient Client { get; } = client;
  public AuthResponse Auth { get; } = auth;
  public Guid Id => Auth.User.Id;
  public string Email => Auth.User.Email;
  public string Handle => Auth.User.Handle;
}

public static class FactoryExtensions
{
  public const string Password = "Password123";

  public static async Task<TestUser> RegisterUserAsync(this TaskFlowFactory factory, string name = "Test User")
  {
    var client = factory.CreateClient();
    var email = $"{Guid.NewGuid():N}@test.dev";
    var response = await client.PostJsonAsync("/api/v1/auth/register", new RegisterRequest(email, name, Password));
    response.EnsureSuccessStatusCode();
    var auth = await response.ReadAsAsync<AuthResponse>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    return new TestUser(client, auth);
  }

  public static async Task<WorkspaceDto> CreateWorkspaceAsync(this TestUser owner, string name = "Workspace")
  {
    var response = await owner.Client.PostJsonAsync("/api/v1/workspaces", new CreateWorkspaceRequest(name, null));
    response.EnsureSuccessStatusCode();
    return await response.ReadAsAsync<WorkspaceDto>();
  }

  public static async Task AddMemberAsync(this TestUser owner, Guid workspaceId, TestUser member, WorkspaceRole role = WorkspaceRole.Member)
  {
    var response = await owner.Client.PostJsonAsync($"/api/v1/workspaces/{workspaceId}/members", new InviteMemberRequest(member.Email, role));
    response.EnsureSuccessStatusCode();
  }

  public static async Task<BoardDetailDto> CreateBoardAsync(this TestUser user, Guid workspaceId, string name = "Board")
  {
    var created = await user.Client.PostJsonAsync($"/api/v1/workspaces/{workspaceId}/boards", new CreateBoardRequest(name, null));
    created.EnsureSuccessStatusCode();
    var summary = await created.ReadAsAsync<BoardSummaryDto>();
    var detail = await user.Client.GetAsync($"/api/v1/boards/{summary.Id}");
    detail.EnsureSuccessStatusCode();
    return await detail.ReadAsAsync<BoardDetailDto>();
  }

  public static async Task<CardDto> CreateCardAsync(this TestUser user, Guid columnId, string title = "Card", DateTime? dueDate = null)
  {
    var response = await user.Client.PostJsonAsync($"/api/v1/columns/{columnId}/cards", new CreateCardRequest(title, null, dueDate));
    response.EnsureSuccessStatusCode();
    return await response.ReadAsAsync<CardDto>();
  }
}
