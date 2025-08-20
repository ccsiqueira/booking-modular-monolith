namespace Operator.Operators.Exceptions;
using BuildingBlocks.Exception;


public class InvalidOperatorIdException : BadRequestException
{
    public InvalidOperatorIdException(Guid operatorId) : base($"OperatorId: '{operatorId}' is invalid.")
    {
    }
}
