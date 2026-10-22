namespace ApiEmprestimos.Domain.Exceptions;

public sealed class BookIsbnAlreadyExistsException()
    : Exception("A book with this ISBN already exists.");
