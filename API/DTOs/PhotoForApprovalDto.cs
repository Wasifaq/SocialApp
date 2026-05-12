using System;

namespace API.DTOs;

public class PhotoForApprovalDto
{
    public int Id { get; set; }

    public required string Url { get; set; }

    public string? PublicId { get; set; }

    public bool IsApproved { get; set; }

    public required string MemberId { get; set; }

    public required string MemberDisplayName { get; set; }
}
