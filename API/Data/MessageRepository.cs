using System;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class MessageRepository(AppDbContext context) : IMessageRepository
{
    public void AddMessage(Message message)
    {
        context.Messages.Add(message);
    }

    public void DeleteMessage(Message message)
    {
        context.Messages.Remove(message);
    }

    public async Task<Message?> GetMessage(string messageId)
    {
        return await context.Messages.FindAsync(messageId);
    }

    public async Task<PaginatedResult<MessageDto>> GetMessagesForMember(MessageParams messageParams)
    {
        var query = context.Messages
                        .OrderByDescending(o => o.MessageSent)
                        .AsQueryable();
        
        query = messageParams.Container switch
        {
            "Outbox" => query.Where(m => m.SenderId == messageParams.MemberId && !m.SenderDeleted),
            _ => query.Where(m => m.RecipientId == messageParams.MemberId && !m.RecipientDeleted)
        };

        var messageQuery = query.Select(MessageExtensions.ToDtoProjection());

        return await PaginationHelper.CreateAsync(messageQuery, messageParams.PageNumber, messageParams.PageSize);
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessageThread(string currentMemberId, string recipientId)
    {
        await context.Messages
                .Where(m => m.RecipientId == currentMemberId && m.SenderId == recipientId && m.DateRead == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.DateRead, DateTime.UtcNow));
        
        return await context.Messages
                        .Where(m => (m.RecipientId == currentMemberId && !m.RecipientDeleted && m.SenderId == recipientId) 
                            || (m.SenderId == currentMemberId && !m.SenderDeleted && m.RecipientId == recipientId))
                        .OrderBy(o => o.MessageSent)
                        .Select(MessageExtensions.ToDtoProjection())
                        .ToListAsync();
    }

    public async Task<bool> SaveAllAsync()
    {
        return await context.SaveChangesAsync() > 0;
    }
}
