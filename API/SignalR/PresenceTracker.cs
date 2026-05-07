using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.HttpResults;

namespace API.SignalR;

// This presence tracker is not scalable, means if API project is deployed on multiple servers then 
// it will only be available for the current server means each server will have it's own presence tracker
public class PresenceTracker
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> OnlineUsers = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();

    public Task UserConnected(string userId, string connectionId)
    {
        var connections = OnlineUsers.GetOrAdd(userId, _ => 
            new ConcurrentDictionary<string, byte>());
        connections.TryAdd(connectionId, 0);

        return Task.CompletedTask;
    }

    public Task UserDisconnected(string userId, string connectionId)
    {
        if (OnlineUsers.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if(connections.IsEmpty)
            {
                OnlineUsers.TryRemove(userId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<string[]> GetOnlineUsers()
    {
        return Task.FromResult(OnlineUsers.Keys.OrderBy(k => k).ToArray());
    }

    public static Task<List<string>> GetConenctionsForUser(string userId)
    {
        if(OnlineUsers.TryGetValue(userId, out var connections))
        {
            return Task.FromResult(connections.Keys.ToList());
        }

        return Task.FromResult(new List<string>());
    }
}
