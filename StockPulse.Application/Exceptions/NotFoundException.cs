namespace StockPulse.Application.Exceptions;

public class NotFoundException(string name, object key)
    : Exception($"{name} with key '{key}' was not found.");
