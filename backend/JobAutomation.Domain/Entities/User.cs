namespace JobAutomation.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    /// <summary>
    /// Lowercase normalized email for case-insensitive uniqueness.
    /// </summary>
    public required string NormalizedEmail { get; set; }

    public required string PasswordHash { get; set; }

    public string? DisplayName { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
