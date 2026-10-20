using ApiEmprestimos.Domain.Entities;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class UserTests
{
    [Fact]
    public void Create_trims_name_and_email()
    {
        var user = User.Create("  Ada Lovelace  ", "  ada@example.test  ");

        user.Name.Should().Be("Ada Lovelace");
        user.Email.Should().Be("ada@example.test");
        user.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Update_profile_rejects_empty_fields_without_changing_existing_values()
    {
        var user = User.Create("Ada Lovelace", "ada@example.test");

        var act = () => user.UpdateProfile("", "updated@example.test");

        act.Should().Throw<ArgumentException>();
        user.Name.Should().Be("Ada Lovelace");
        user.Email.Should().Be("ada@example.test");
    }
}
