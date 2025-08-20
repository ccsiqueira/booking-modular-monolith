namespace Operator.Operators.ValueObjects;

using Operator.Operators.Exceptions;

public record WarName
{
    public string Value { get; }

    private WarName(string value)
    {
        Value = value;
    }

    public static WarName Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidWarNameException("War name cannot be empty");
        }

        if (value.Length < 2 || value.Length > 40)
        {
            throw new InvalidWarNameException("War name must be between 2 and 40 characters");
        }

        // Allow alphanumeric and basic symbols for call signs
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z0-9_-]+$"))
        {
            throw new InvalidWarNameException("War name can only contain letters, numbers, underscores, and hyphens");
        }

        return new WarName(value);
    }

    public static implicit operator string(WarName warName)
    {
        return warName.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
