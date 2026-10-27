using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Brokerage.Host.Services;

public sealed record ChatPendingAction(
    Guid Id,
    string Method,
    string Description,
    JsonElement Parameters,
    DateTimeOffset ExpiresAt);

public sealed record ChatActionExecutionResponse(string Method, string Result);

public sealed class ChatActionDraftStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<Guid, Draft> _drafts = new();

    public ChatPendingAction Create(
        ClaimsPrincipal user,
        string method,
        string description,
        JsonObject parameters)
    {
        if (!TryGetUserId(user, out var userId))
            throw new UnauthorizedAccessException("The authenticated user has no stable identifier.");
        var now = timeProvider.GetUtcNow();
        RemoveExpired(now);

        var draft = new Draft(
            userId,
            method,
            description,
            JsonSerializer.SerializeToElement(parameters),
            now.Add(Lifetime));
        var id = Guid.NewGuid();
        _drafts[id] = draft;

        return ToPublicAction(id, draft);
    }

    public bool TryTake(Guid id, ClaimsPrincipal user, out ChatPendingAction action)
    {
        if (!TryGetUserId(user, out var userId))
        {
            action = null!;
            return false;
        }

        if (_drafts.TryGetValue(id, out var draft) &&
            draft.UserId == userId &&
            draft.ExpiresAt > timeProvider.GetUtcNow() &&
            _drafts.TryRemove(new KeyValuePair<Guid, Draft>(id, draft)))
        {
            action = ToPublicAction(id, draft);
            return true;
        }

        action = null!;
        return false;
    }

    public bool TryCancel(Guid id, ClaimsPrincipal user)
    {
        return TryGetUserId(user, out var userId) &&
               _drafts.TryGetValue(id, out var draft) &&
               draft.UserId == userId &&
               _drafts.TryRemove(new KeyValuePair<Guid, Draft>(id, draft));
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in _drafts)
        {
            if (entry.Value.ExpiresAt <= now)
                _drafts.TryRemove(entry);
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out string userId)
    {
        userId = user.FindFirst("sub")?.Value!;
        return userId is not null;
    }

    private static ChatPendingAction ToPublicAction(Guid id, Draft draft) =>
        new(id, draft.Method, draft.Description, draft.Parameters, draft.ExpiresAt);

    private sealed record Draft(
        string UserId,
        string Method,
        string Description,
        JsonElement Parameters,
        DateTimeOffset ExpiresAt);
}
