namespace Parchment;

public abstract class ParchmentException(string message, Exception? inner = null) :
    Exception(message, inner);
