using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TaskFlow.Api.Hubs;

namespace TaskFlow.Api.IntegrationTests.Support;

/// <summary>A real SignalR client talking to the in-memory test server over long polling.</summary>
public sealed class HubClient : IAsyncDisposable
{
  private readonly ConcurrentQueue<(string Name, JsonElement Payload)> _received = new();
  private readonly SemaphoreSlim _signal = new(0);

  public HubConnection Connection { get; }

  public HubClient(TaskFlowFactory factory, string? accessToken)
  {
    Connection = new HubConnectionBuilder()
      .WithUrl(new Uri(factory.Server.BaseAddress, BoardHub.Path), options =>
      {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        options.AccessTokenProvider = () => Task.FromResult(accessToken);
      })
      .Build();

    foreach (var name in new[] { "CardCreated", "CardUpdated", "CardMoved", "CardDeleted", "CommentAdded", "CommentDeleted", "ColumnsChanged" })
    {
      Connection.On<JsonElement>(name, payload =>
      {
        _received.Enqueue((name, payload));
        _signal.Release();
      });
    }
  }

  public static async Task<HubClient> ConnectAsync(TaskFlowFactory factory, TestUser user)
  {
    var client = new HubClient(factory, user.Auth.AccessToken);
    await client.Connection.StartAsync();
    return client;
  }

  /// <summary>Waits for the next event; fails the test instead of hanging when none arrives.</summary>
  public async Task<(string Name, JsonElement Payload)> NextAsync(TimeSpan? timeout = null)
  {
    if (!await _signal.WaitAsync(timeout ?? TimeSpan.FromSeconds(5)))
    {
      throw new TimeoutException("No event was received.");
    }

    _received.TryDequeue(out var item);
    return item;
  }

  /// <summary>True when nothing arrives within the (short) window; used to prove an event was NOT delivered.</summary>
  public async Task<bool> StaysQuietAsync(TimeSpan? window = null) =>
    !await _signal.WaitAsync(window ?? TimeSpan.FromMilliseconds(600));

  public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
