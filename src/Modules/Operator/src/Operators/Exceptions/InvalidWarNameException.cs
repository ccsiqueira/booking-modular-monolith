namespace Operator.Operators.Exceptions;

using BuildingBlocks.Exception;

public class InvalidWarNameException : BadRequestException
{
    public InvalidWarNameException(string message) : base(message)
    {
    }
}

