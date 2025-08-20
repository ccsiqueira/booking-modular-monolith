namespace Operator.Operators.Exceptions;
using BuildingBlocks.Exception;


public class OperatorNotExist : NotFoundException
{
    public OperatorNotExist() : base("Operator does not exist.")
    {
    }
}
