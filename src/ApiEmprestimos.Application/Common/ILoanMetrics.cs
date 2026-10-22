namespace ApiEmprestimos.Application.Common;

public interface ILoanMetrics
{
    void RecordLoanCreated();
    void RecordUnavailableLoanAttempt();
    void RecordIdempotentReplay();
}
