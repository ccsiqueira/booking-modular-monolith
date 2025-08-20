namespace Operator.Operators.Exceptions;
using BuildingBlocks.Exception;


public class InvalidOrganizationException : BadRequestException
{
    public InvalidOrganizationException(string message) : base(message)
    {
    }
}
