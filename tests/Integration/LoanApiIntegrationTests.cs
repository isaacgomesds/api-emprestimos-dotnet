using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiEmprestimos.Application.Common.DTOs;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LoanApiIntegrationTests(ApiIntegrationFixture fixture)
    : ApiIntegrationTestBase(fixture)
{
    private static readonly JsonSerializerOptions LoanJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Concurrent_requests_for_the_last_copy_create_one_loan_and_return_conflicts_for_the_rest()
    {
        const int requestCount = 12;
        var (bookId, firstUserId) = await SeedBookAndUserAsync(availableCopies: 1);
        var userIds = new List<Guid> { firstUserId };
        for (var index = 1; index < requestCount; index++)
        {
            userIds.Add(await SeedUserAsync());
        }

        var loanDate = DateTimeOffset.UtcNow;
        var dueDate = loanDate.AddDays(14);
        var responses = await Task.WhenAll(userIds.Select((userId, index) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
            {
                Content = JsonContent.Create(new
                {
                    BookId = bookId,
                    UserId = userId,
                    LoanDateUtc = loanDate,
                    DueDateUtc = dueDate
                })
            };
            request.Headers.Add("Idempotency-Key", $"concurrent-last-copy-{index}-{Guid.NewGuid():N}");
            return Client.SendAsync(request);
        }));

        responses.Count(response => response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
            .Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict)
            .Should().Be(requestCount - 1);

        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(0);
        state.LoanCount.Should().Be(1);
    }

    [Fact]
    public async Task Repeated_idempotency_key_replays_success_response_without_creating_a_second_loan()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 2);
        var requestBody = new
        {
            BookId = bookId,
            UserId = userId,
            LoanDateUtc = DateTimeOffset.UtcNow,
            DueDateUtc = DateTimeOffset.UtcNow.AddDays(14)
        };
        var idempotencyKey = $"loan-replay-{Guid.NewGuid():N}";

        using var firstResponse = await SendLoanRequestAsync(requestBody, idempotencyKey);
        using var replayResponse = await SendLoanRequestAsync(requestBody, idempotencyKey);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        replayResponse.StatusCode.Should().Be(firstResponse.StatusCode);
        (await replayResponse.Content.ReadAsStringAsync())
            .Should().Be(await firstResponse.Content.ReadAsStringAsync());
        replayResponse.Headers.Location.Should().Be(firstResponse.Headers.Location);

        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(1);
        state.LoanCount.Should().Be(1);

        var replayedLoan = await replayResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        replayedLoan.Should().NotBeNull();
    }

    [Fact]
    public async Task Idempotency_result_survives_redis_cache_loss()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 2);
        var requestBody = new
        {
            BookId = bookId,
            UserId = userId,
            LoanDateUtc = DateTimeOffset.UtcNow,
            DueDateUtc = DateTimeOffset.UtcNow.AddDays(14)
        };
        var idempotencyKey = $"loan-durable-replay-{Guid.NewGuid():N}";

        using var firstResponse = await SendLoanRequestAsync(requestBody, idempotencyKey);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        await FlushRedisAsync();

        using var replayResponse = await SendLoanRequestAsync(requestBody, idempotencyKey);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replayResponse.Content.ReadAsStringAsync()).Should().Be(firstBody);

        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(1);
        state.LoanCount.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_idempotency_key_create_only_one_loan()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 2);
        var loanDate = DateTimeOffset.UtcNow;
        var requestBody = new
        {
            BookId = bookId,
            UserId = userId,
            LoanDateUtc = loanDate,
            DueDateUtc = loanDate.AddDays(14)
        };
        var idempotencyKey = $"loan-concurrent-replay-{Guid.NewGuid():N}";

        using var firstRequest = CreateLoanRequest(requestBody, idempotencyKey);
        using var secondRequest = CreateLoanRequest(requestBody, idempotencyKey);
        var responses = await Task.WhenAll(
            Client.SendAsync(firstRequest),
            Client.SendAsync(secondRequest));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        secondBody.Should().Be(firstBody);

        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(1);
        state.LoanCount.Should().Be(1);
    }

    [Fact]
    public async Task Reusing_idempotency_key_with_a_different_body_returns_conflict()
    {
        var (bookId, firstUserId) = await SeedBookAndUserAsync(availableCopies: 2);
        var secondUserId = await SeedUserAsync();
        var loanDate = DateTimeOffset.UtcNow;
        var idempotencyKey = $"loan-different-body-{Guid.NewGuid():N}";

        using var firstResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = firstUserId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            idempotencyKey);
        using var secondResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = secondUserId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(21)
            },
            idempotencyKey);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        secondResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Contain("Idempotency-Key");

        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(1);
        state.LoanCount.Should().Be(1);
    }

    [Fact]
    public async Task Get_user_loan_history_includes_book_details()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        var bookDetails = await GetBookDetailsAsync(bookId);
        var loanDate = DateTimeOffset.UtcNow;
        using var createResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            $"loan-history-{Guid.NewGuid():N}");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var historyResponse = await Client.GetAsync($"/users/{userId}/loans");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<List<UserLoanHistoryResponseDto>>(LoanJsonOptions);

        history.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                BookId = bookId,
                BookTitle = bookDetails.Title,
                BookAuthor = bookDetails.Author,
                Isbn = bookDetails.Isbn,
                UserId = userId
            },
            options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Get_book_loan_history_includes_borrower_details()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        var userDetails = await GetUserDetailsAsync(userId);
        var loanDate = DateTimeOffset.UtcNow;
        using var createResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            $"book-history-{Guid.NewGuid():N}");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var historyResponse = await Client.GetAsync($"/books/{bookId}/history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<List<BookLoanHistoryResponseDto>>(LoanJsonOptions);

        history.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                BookId = bookId,
                UserId = userId,
                UserName = userDetails.Name,
                UserEmail = userDetails.Email
            },
            options => options.ExcludingMissingMembers());
    }

    private Task<HttpResponseMessage> SendLoanRequestAsync<TRequest>(TRequest request, string idempotencyKey)
    {
        return Client.SendAsync(CreateLoanRequest(request, idempotencyKey));
    }

    private static HttpRequestMessage CreateLoanRequest<TRequest>(TRequest request, string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return message;
    }

    private sealed record ProblemDetailsResponse(string? Detail);
}
