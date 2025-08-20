namespace Operator.Operators.Exceptions;
using BuildingBlocks.Exception;


public class InvalidRankException : BadRequestException
{
    public InvalidRankException(string message) : base(message)
    {
    }
}
