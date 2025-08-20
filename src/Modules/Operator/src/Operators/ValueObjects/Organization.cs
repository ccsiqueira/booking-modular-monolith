namespace Operator.Operators.ValueObjects;

using Operator.Operators.Exceptions;

public record Organization
{
    public string Value { get; }
    public string Code { get; }

    private Organization(string value, string code)
    {
        Value = value;
        Code = code;
    }

    public static Organization Of(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOrganizationException("Organization name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOrganizationException("Organization code cannot be empty");
        }

        if (code.Length > 10)
        {
            throw new InvalidOrganizationException("Organization code must be 10 characters or less");
        }

        return new Organization(value.Trim(), code.Trim().ToUpperInvariant());
    }

    // Predefined organizations for Brazilian Navy units
    public static class Common
    {
        public static readonly Organization Casnav = Of("Centro de Análise de Sistemas Navais", "CASNAV");
        public static readonly Organization Casop = Of("Centro de Apoio a Sistemas Operativos", "CASOP");
        public static readonly Organization Ipqm = Of("Instituto de Pesquisas da Marinha", "IPQM");
        public static readonly Organization Comench = Of("Comando em Chefe da Esquadra", "COMENCH");
        public static readonly Organization ComOpNav = Of("Comando de Operações Navais", "COMOPNAV");
        public static readonly Organization Cgcfn = Of("Comandante-Geral do Corpo de Fuzileiros Navais", "CGCFN");
        public static readonly Organization En = Of("Escola Naval", "EN");
    }

    public static implicit operator string(Organization organization)
    {
        return organization.Value;
    }

    public override string ToString()
    {
        return $"{Value} ({Code})";
    }
}
