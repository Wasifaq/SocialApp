using System;

namespace API.DTOs;

public class MemberUpdateDto
{
    public DateOnly DateOfBirth { get; set; }

    public string? DisplayName { get; set; }

    public string? Gender { get; set; }

    public string? Description { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }
}
