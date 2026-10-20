using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Infrastructure.Data;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class CatalogCacheIntegrationTests(ApiIntegrationFixture fixture)
    : ApiIntegrationTestBase(fixture)
{
    private static readonly JsonSerializerOptions LoanJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Book_and_loan_mutations_invalidate_cached_catalog_and_availability()
    {
        var (bookId, firstUserId) = await SeedBookAndUserAsync(availableCopies: 2);

        var firstCatalog = await GetCatalogAsync();
        var cachedCatalog = await GetCatalogAsync();
        cachedCatalog.Should().BeEquivalentTo(firstCatalog);
        firstCatalog.Single(book => book.Id == bookId).AvailableCopies.Should().Be(2);
        (await GetBookAsync(bookId)).AvailableCopies.Should().Be(2);
        (await GetBookAsync(bookId)).AvailableCopies.Should().Be(2);

        var requestDate = DateTimeOffset.UtcNow;
        using var loanResponse = await SendLoanRequestAsync(
            bookId,
            firstUserId,
            requestDate,
            "cache-test-loan-create");
        loanResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdLoan = await loanResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        createdLoan.Should().NotBeNull();

        (await GetCatalogAsync()).Single(book => book.Id == bookId).AvailableCopies.Should().Be(1);
        (await GetBookAsync(bookId)).AvailableCopies.Should().Be(1);

        using var returnResponse = await Client.PostAsync($"/loans/{createdLoan!.Id}/return", content: null);
        returnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetCatalogAsync()).Single(book => book.Id == bookId).AvailableCopies.Should().Be(2);
        (await GetBookAsync(bookId)).AvailableCopies.Should().Be(2);

        var secondUserId = await SeedUserAsync();
        using var secondLoanResponse = await SendLoanRequestAsync(
            bookId,
            secondUserId,
            requestDate.AddMinutes(1),
            "cache-test-loan-cancel");
        secondLoanResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondLoan = await secondLoanResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        secondLoan.Should().NotBeNull();
        (await GetCatalogAsync()).Single(book => book.Id == bookId).AvailableCopies.Should().Be(1);

        using var cancelResponse = await Client.PostAsync($"/loans/{secondLoan!.Id}/cancel", content: null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetCatalogAsync()).Single(book => book.Id == bookId).AvailableCopies.Should().Be(2);

        using var updateResponse = await Client.PatchAsJsonAsync(
            $"/books/{bookId}",
            new { Title = "Updated Catalog Title", TotalCopies = 3 });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedCatalog = await GetCatalogAsync();
        updatedCatalog.Single(book => book.Id == bookId).Title.Should().Be("Updated Catalog Title");
        updatedCatalog.Single(book => book.Id == bookId).AvailableCopies.Should().Be(3);
        (await GetBookAsync(bookId)).Title.Should().Be("Updated Catalog Title");
        (await GetBookAsync(bookId)).AvailableCopies.Should().Be(3);

        using var createResponse = await Client.PostAsJsonAsync(
            "/books",
            new
            {
                Title = "New Catalog Book",
                Isbn = Guid.NewGuid().ToString("N")[..20],
                Author = "Catalog Author",
                TotalCopies = 1
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetCatalogAsync()).Should().Contain(book => book.Title == "New Catalog Book");

        using var deactivateResponse = await Client.DeleteAsync($"/books/{bookId}");
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetCatalogAsync()).Should().NotContain(book => book.Id == bookId);
        var inactiveBook = await GetBookAsync(bookId);
        inactiveBook.IsActive.Should().BeFalse();
    }

    private async Task<IReadOnlyList<BookResponseDto>> GetCatalogAsync() =>
        (await Client.GetFromJsonAsync<IReadOnlyList<BookResponseDto>>("/books"))!;

    private async Task<BookResponseDto> GetBookAsync(Guid bookId) =>
        (await Client.GetFromJsonAsync<BookResponseDto>($"/books/{bookId}"))!;

    private Task<HttpResponseMessage> SendLoanRequestAsync(
        Guid bookId,
        Guid userId,
        DateTimeOffset loanDate,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return Client.SendAsync(request);
    }
}
