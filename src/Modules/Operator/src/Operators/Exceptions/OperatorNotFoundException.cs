namespace Operator.Operators.Exceptions;
using BuildingBlocks.Exception;


public class OperatorNotFoundException : NotFoundException
{
    public OperatorNotFoundException() : base("Operator not found.")
    {
    }
}