namespace Operator.Operators.Exceptions;
using BuildingBlocks.Exception;


public class OperatorAlreadyExist : ConflictException
{
    public OperatorAlreadyExist() : base("Operator already exists.")
    {
    }
}