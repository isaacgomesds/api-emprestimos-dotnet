using System.Net;
using System.Net.Http.Json;
using ApiEmprestimos.Application.Common.DTOs;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class UserApiIntegrationTests(ApiIntegrationFixture fixture)
    : ApiIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Get_users_returns_users_created_through_the_api()
    {
        var expectedUsers = new List<UserResponseDto>();
        foreach (var (name, email) in new[]
                 {
                     ("Ada Lovelace", $"ada-{Guid.NewGuid():N}@example.test"),
                     ("Grace Hopper", $"grace-{Guid.NewGuid():N}@example.test")
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/users")
            {
                Content = JsonContent.Create(new { Name = name, Email = email })
            };
            request.Headers.Add("X-Actor", "integration-test");
            request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());

            using var response = await Client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            expectedUsers.Add((await response.Content.ReadFromJsonAsync<UserResponseDto>())!);
        }

        using var listResponse = await Client.GetAsync("/users");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await listResponse.Content.ReadFromJsonAsync<List<UserResponseDto>>();

        users.Should().NotBeNull();
        foreach (var expected in expectedUsers)
        {
            var actual = users.Should().ContainSingle(user => user.Id == expected.Id).Which;
            actual.Name.Should().Be(expected.Name);
            actual.Email.Should().Be(expected.Email);
            actual.CreatedAtUtc.Should().BeCloseTo(expected.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        }
    }
}
