using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
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
            return (index % 2 == 0 ? Client : SecondClient).SendAsync(request);
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
    public async Task Concurrent_book_metadata_update_and_loan_preserve_the_reserved_inventory()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        var loanDate = DateTimeOffset.UtcNow;
        using var loanRequest = CreateLoanRequest(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            $"metadata-loan-race-{Guid.NewGuid():N}");

        var updateTask = Client.PatchAsJsonAsync(
            $"/books/{bookId}",
            new { Title = "Metadata Updated During Loan" });
        var loanTask = SecondClient.SendAsync(loanRequest);
        using var updateResponse = await updateTask;
        using var loanResponse = await loanTask;

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loanResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetBookDetailsAsync(bookId)).Title.Should().Be("Metadata Updated During Loan");
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
    public async Task Concurrent_idempotent_requests_across_api_instances_create_only_one_loan()
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
        var idempotencyKey = $"cross-instance-idempotency-{Guid.NewGuid():N}";
        using var firstRequest = CreateLoanRequest(requestBody, idempotencyKey);
        using var secondRequest = CreateLoanRequest(requestBody, idempotencyKey);
        var responses = await Task.WhenAll(
            Client.SendAsync(firstRequest),
            SecondClient.SendAsync(secondRequest));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await secondResponse.Content.ReadAsStringAsync())
            .Should().Be(await firstResponse.Content.ReadAsStringAsync());
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
    public async Task Concurrent_returns_across_api_instances_increment_inventory_once_per_loan()
    {
        var (bookId, firstUserId) = await SeedBookAndUserAsync(availableCopies: 2);
        var secondUserId = await SeedUserAsync();
        var loanDate = DateTimeOffset.UtcNow;
        using var firstCreateResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = firstUserId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            $"return-first-{Guid.NewGuid():N}");
        using var secondCreateResponse = await SecondClient.SendAsync(CreateLoanRequest(
            new
            {
                BookId = bookId,
                UserId = secondUserId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            $"return-second-{Guid.NewGuid():N}"));
        firstCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstLoan = await firstCreateResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        var secondLoan = await secondCreateResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        firstLoan.Should().NotBeNull();
        secondLoan.Should().NotBeNull();

        var returnTasks = new[]
        {
            Client.PostAsync($"/loans/{firstLoan!.Id}/return", content: null),
            SecondClient.PostAsync($"/loans/{secondLoan!.Id}/return", content: null)
        };
        using var firstReturn = await returnTasks[0];
        using var secondReturn = await returnTasks[1];

        firstReturn.StatusCode.Should().Be(HttpStatusCode.OK);
        secondReturn.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(2);
        state.LoanCount.Should().Be(2);
        (await GetLoanStatesAsync(bookId))
            .Should().OnlyContain(loan => loan.Status == LoanStatus.Returned);
    }

    [Fact]
    public async Task Concurrent_duplicate_returns_across_api_instances_restore_inventory_once()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        using var createResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = DateTimeOffset.UtcNow,
                DueDateUtc = DateTimeOffset.UtcNow.AddDays(14)
            },
            $"concurrent-return-{Guid.NewGuid():N}");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdLoan = await createResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        createdLoan.Should().NotBeNull();

        var returnResponses = await Task.WhenAll(
            Client.PostAsync($"/loans/{createdLoan!.Id}/return", content: null),
            SecondClient.PostAsync($"/loans/{createdLoan.Id}/return", content: null));
        using var firstReturn = returnResponses[0];
        using var secondReturn = returnResponses[1];

        returnResponses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        returnResponses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        (await GetBookStateAsync(bookId)).AvailableCopies.Should().Be(1);
        (await GetLoanStatesAsync(bookId))
            .Should().ContainSingle()
            .Which.Status.Should().Be(LoanStatus.Returned);
    }

    [Fact]
    public async Task Concurrent_duplicate_isbn_registrations_return_one_created_and_one_conflict()
    {
        var isbn = Guid.NewGuid().ToString("N")[..20];
        var request = new
        {
            Title = "Concurrent ISBN",
            Isbn = isbn,
            Author = "Integration Author",
            TotalCopies = 1
        };
        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync("/books", request),
            SecondClient.PostAsJsonAsync("/books", request));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        conflict.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem!.Detail.Should().Be("A book with this ISBN already exists.");
        problem.Detail.Should().NotContain("IX_Books_Isbn");
        (await GetBookCountByIsbnAsync(isbn)).Should().Be(1);
    }

    [Fact]
    public async Task Returned_loan_remains_in_user_history_and_persisted_state()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        using var createResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = DateTimeOffset.UtcNow,
                DueDateUtc = DateTimeOffset.UtcNow.AddDays(14)
            },
            $"history-return-{Guid.NewGuid():N}");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdLoan = await createResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        createdLoan.Should().NotBeNull();

        using var returnResponse = await Client.PostAsync(
            $"/loans/{createdLoan!.Id}/return",
            content: null);
        returnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var historyResponse = await Client.GetAsync($"/users/{userId}/loans");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<List<UserLoanHistoryResponseDto>>(LoanJsonOptions);

        history.Should().ContainSingle(loan =>
            loan.Id == createdLoan.Id && loan.Status == LoanStatus.Returned);
        var persistedLoan = (await GetLoanStatesAsync(bookId)).Should().ContainSingle().Which;
        persistedLoan.Status.Should().Be(LoanStatus.Returned);
        using var repeatedReturnResponse = await Client.PostAsync(
            $"/loans/{createdLoan.Id}/return",
            content: null);
        repeatedReturnResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetBookStateAsync(bookId)).AvailableCopies.Should().Be(1);
    }

    [Fact]
    public async Task Cancelled_loan_remains_in_user_history_and_persisted_state()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        using var createResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = DateTimeOffset.UtcNow,
                DueDateUtc = DateTimeOffset.UtcNow.AddDays(14)
            },
            $"history-cancel-{Guid.NewGuid():N}");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdLoan = await createResponse.Content.ReadFromJsonAsync<LoanResponseDto>(LoanJsonOptions);
        createdLoan.Should().NotBeNull();

        using var cancelResponse = await Client.PostAsync(
            $"/loans/{createdLoan!.Id}/cancel",
            content: null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var historyResponse = await Client.GetAsync($"/users/{userId}/loans");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<List<UserLoanHistoryResponseDto>>(LoanJsonOptions);

        history.Should().ContainSingle(loan =>
            loan.Id == createdLoan.Id && loan.Status == LoanStatus.Cancelled);
        var persistedLoan = (await GetLoanStatesAsync(bookId)).Should().ContainSingle().Which;
        persistedLoan.Status.Should().Be(LoanStatus.Cancelled);
        (await GetBookStateAsync(bookId)).AvailableCopies.Should().Be(1);
    }

    [Fact]
    public async Task Business_metrics_count_created_loans_unavailable_attempts_and_idempotent_replays()
    {
        var (bookId, userId) = await SeedBookAndUserAsync(availableCopies: 1);
        var secondUserId = await SeedUserAsync();
        var loanDate = DateTimeOffset.UtcNow;
        var body = new
        {
            BookId = bookId,
            UserId = userId,
            LoanDateUtc = loanDate,
            DueDateUtc = loanDate.AddDays(14)
        };
        var idempotencyKey = $"metrics-loan-{Guid.NewGuid():N}";
        var measurements = new ConcurrentDictionary<string, long>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == LoanMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            measurements.AddOrUpdate(instrument.Name, measurement, (_, total) => total + measurement));
        meterListener.Start();

        using var createdResponse = await SendLoanRequestAsync(body, idempotencyKey, Client);
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var replayResponse = await SendLoanRequestAsync(body, idempotencyKey, Client);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var mismatchedReplayResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = userId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(21)
            },
            idempotencyKey,
            Client);
        mismatchedReplayResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var duplicateActiveLoanResponse = await SendLoanRequestAsync(
            body,
            $"metrics-active-duplicate-{Guid.NewGuid():N}",
            Client);
        duplicateActiveLoanResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var rejectedResponse = await SendLoanRequestAsync(
            new
            {
                BookId = bookId,
                UserId = secondUserId,
                LoanDateUtc = loanDate,
                DueDateUtc = loanDate.AddDays(14)
            },
            $"metrics-unavailable-{Guid.NewGuid():N}",
            Client);
        rejectedResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var state = await GetBookStateAsync(bookId);
        state.AvailableCopies.Should().Be(0);
        state.LoanCount.Should().Be(1);

        measurements.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["api.loans.created"] = 1,
            ["api.loans.rejected.no_available_copies"] = 1,
            ["api.loans.idempotent_replays"] = 1
        }, "each corresponding business event should increment its counter exactly once");

        using var metricsResponse = await Client.GetAsync("/metrics");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        metricsResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        var metrics = await metricsResponse.Content.ReadAsStringAsync();
        metrics.Should().Contain("api_loans_created_total");
        metrics.Should().Contain("api_loans_rejected_no_available_copies_total");
        metrics.Should().Contain("api_loans_idempotent_replays_total");
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

    private Task<HttpResponseMessage> SendLoanRequestAsync<TRequest>(
        TRequest request,
        string idempotencyKey,
        HttpClient? client = null)
    {
        return (client ?? Client).SendAsync(CreateLoanRequest(request, idempotencyKey));
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
