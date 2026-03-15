using System.Collections.Concurrent;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harmonie.API.RealTime;

public sealed class ConnectionTracker : IConnectionTracker, IDisposable
{
    private readonly ConcurrentDictionary<string, UserConnectionState> _states = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectionTracker> _logger;
    private readonly TimeSpan _gracePeriod;

    public ConnectionTracker(
        IServiceScopeFactory scopeFactory,
        ILogger<ConnectionTracker> logger)
        : this(scopeFactory, logger, TimeSpan.FromSeconds(30))
    {
    }

    internal ConnectionTracker(
        IServiceScopeFactory scopeFactory,
        ILogger<ConnectionTracker> logger,
        TimeSpan gracePeriod)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _gracePeriod = gracePeriod;
    }

    public async Task HandleConnectedAsync(
        UserId userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var userKey = userId.ToString();
        var state = _states.GetOrAdd(userKey, _ => new UserConnectionState());
        bool isFirstConnection;

        lock (state.Lock)
        {
            isFirstConnection = state.ConnectionIds.Count == 0;
            state.ConnectionIds.Add(connectionId);

            if (state.GracePeriodCts is not null)
            {
                state.GracePeriodCts.Cancel();
                state.GracePeriodCts.Dispose();
                state.GracePeriodCts = null;
            }
        }

        if (isFirstConnection)
        {
            _logger.LogInformation(
                "User {UserId} connected (first connection), broadcasting presence",
                userId);
            await BroadcastUserStatusAsync(userId, cancellationToken);
        }
        else
        {
            _logger.LogDebug(
                "User {UserId} added connection {ConnectionId}",
                userId, connectionId);
        }
    }

    public Task HandleDisconnectedAsync(
        UserId userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var userKey = userId.ToString();

        if (!_states.TryGetValue(userKey, out var state))
            return Task.CompletedTask;

        bool isLastConnection;

        lock (state.Lock)
        {
            state.ConnectionIds.Remove(connectionId);
            isLastConnection = state.ConnectionIds.Count == 0;
        }

        if (isLastConnection)
        {
            _logger.LogInformation(
                "User {UserId} lost last connection, starting {GracePeriod}s grace period",
                userId, _gracePeriod.TotalSeconds);
            StartGracePeriod(userId, state);
        }

        return Task.CompletedTask;
    }

    public bool IsOnline(UserId userId)
    {
        var userKey = userId.ToString();
        return _states.TryGetValue(userKey, out var state)
            && state.ConnectionIds.Count > 0;
    }

    private void StartGracePeriod(UserId userId, UserConnectionState state)
    {
        var cts = new CancellationTokenSource();

        lock (state.Lock)
        {
            if (state.GracePeriodCts is not null)
            {
                state.GracePeriodCts.Cancel();
                state.GracePeriodCts.Dispose();
            }

            state.GracePeriodCts = cts;
        }

        _ = RunGracePeriodAsync(userId, state, cts);
    }

    private async Task RunGracePeriodAsync(
        UserId userId,
        UserConnectionState state,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_gracePeriod, cts.Token);

            bool stillOffline;
            lock (state.Lock)
            {
                stillOffline = state.ConnectionIds.Count == 0;
            }

            if (!stillOffline)
                return;

            _logger.LogInformation(
                "Grace period expired for user {UserId}, broadcasting offline",
                userId);

            await BroadcastOfflineAsync(userId, CancellationToken.None);
        }
        catch (TaskCanceledException)
        {
            // Grace period cancelled — user reconnected
        }
        finally
        {
            lock (state.Lock)
            {
                if (state.GracePeriodCts == cts)
                    state.GracePeriodCts = null;
            }

            cts.Dispose();
        }
    }

    private async Task BroadcastUserStatusAsync(UserId userId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var guildMemberRepository = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();
            var presenceNotifier = scope.ServiceProvider.GetRequiredService<IUserPresenceNotifier>();

            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return;

            var broadcastStatus = string.Equals(user.Status, "invisible", StringComparison.OrdinalIgnoreCase)
                ? "offline"
                : user.Status;

            var memberships = await guildMemberRepository.GetUserGuildMembershipsAsync(userId, cancellationToken);
            var guildIds = memberships.Select(m => m.Guild.Id).ToList();

            if (guildIds.Count > 0)
            {
                await presenceNotifier.NotifyStatusChangedAsync(
                    new UserPresenceChangedNotification(userId, broadcastStatus, guildIds),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast presence for user {UserId}", userId);
        }
    }

    private async Task BroadcastOfflineAsync(UserId userId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var guildMemberRepository = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();
            var presenceNotifier = scope.ServiceProvider.GetRequiredService<IUserPresenceNotifier>();

            var memberships = await guildMemberRepository.GetUserGuildMembershipsAsync(userId, cancellationToken);
            var guildIds = memberships.Select(m => m.Guild.Id).ToList();

            if (guildIds.Count > 0)
            {
                await presenceNotifier.NotifyStatusChangedAsync(
                    new UserPresenceChangedNotification(userId, "offline", guildIds),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast offline for user {UserId}", userId);
        }
    }

    public void Dispose()
    {
        foreach (var state in _states.Values)
        {
            lock (state.Lock)
            {
                if (state.GracePeriodCts is not null)
                {
                    state.GracePeriodCts.Cancel();
                    state.GracePeriodCts.Dispose();
                    state.GracePeriodCts = null;
                }
            }
        }

        _states.Clear();
    }

    private sealed class UserConnectionState
    {
        public readonly HashSet<string> ConnectionIds = new();
        public CancellationTokenSource? GracePeriodCts;
        public readonly object Lock = new();
    }
}
