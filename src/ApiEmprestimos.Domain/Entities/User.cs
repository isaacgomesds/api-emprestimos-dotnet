namespace ApiEmprestimos.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public static User Create(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new User
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Name = name.Trim();
        Email = email.Trim();
    }
}
