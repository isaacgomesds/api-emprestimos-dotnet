using System.Diagnostics.Metrics;

namespace ApiEmprestimos.Application.Common;

public sealed class LoanMetrics : ILoanMetrics
{
    public const string MeterName = "ApiEmprestimos.Business";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> LoansCreated =
        Meter.CreateCounter<long>("api.loans.created", "{loan}");
    private static readonly Counter<long> UnavailableLoanAttempts =
        Meter.CreateCounter<long>("api.loans.rejected.no_available_copies", "{attempt}");
    private static readonly Counter<long> IdempotentReplays =
        Meter.CreateCounter<long>("api.loans.idempotent_replays", "{request}");

    public void RecordLoanCreated() => LoansCreated.Add(1);

    public void RecordUnavailableLoanAttempt() => UnavailableLoanAttempts.Add(1);

    public void RecordIdempotentReplay() => IdempotentReplays.Add(1);
}
