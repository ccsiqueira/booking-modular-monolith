namespace Operator.Operators.ValueObjects;

using System;
using Exceptions;

public record OperatorId
{
    public Guid Value { get; }

    private OperatorId(Guid value)
    {
        Value = value;
    }

    public static OperatorId Of(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidOperatorIdException(value);
        }

        return new OperatorId(value);
    }

    public static implicit operator Guid(OperatorId id) => id.Value;
    public static implicit operator OperatorId(Guid id) => new(id);

    public static OperatorId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}