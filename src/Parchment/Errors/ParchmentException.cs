namespace Parchment.Errors;

public abstract class ParchmentException(string message, Exception? inner = null) :
    Exception(message, inner);
