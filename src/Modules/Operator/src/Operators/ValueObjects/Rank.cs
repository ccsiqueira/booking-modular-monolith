namespace Operator.Operators.ValueObjects;

using Operator.Operators.Exceptions;
using Operator.Operators.Enums;

public record Rank
{
    public string Value { get; }
    public int Level { get; }
    public RankType Type { get; }

    private Rank(string value, int level, RankType type)
    {
        Value = value;
        Level = level;
        Type = type;
    }

    public static Rank Of(string value, int level, RankType type)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRankException("Rank name cannot be empty");
        }

        if (level < 1 || level > 20)
        {
            throw new InvalidRankException("Rank level must be between 1 and 20");
        }

        return new Rank(value, level, type);
    }

    // Predefined ranks for Brazilian Navy (Marinha do Brasil) hierarchy
    public static class MarinhaDoBrasil
    {
        // Praças (Enlisted - Levels 1-6)
        public static readonly Rank MarinheiroRecrutaNovo = Of("Marinheiro-Recruta Novo", 1, RankType.Enlisted);
        public static readonly Rank MarinheiroRecrutaSegundaClasse = Of("Marinheiro-Recruta de 2ª Classe", 2, RankType.Enlisted);
        public static readonly Rank MarinheiroRecrutaPrimeiraClasse = Of("Marinheiro-Recruta de 1ª Classe", 3, RankType.Enlisted);
        public static readonly Rank Cabo = Of("Cabo", 4, RankType.Enlisted);
        public static readonly Rank TerceiroSargento = Of("3º Sargento", 5, RankType.Enlisted);
        public static readonly Rank SegundoSargento = Of("2º Sargento", 6, RankType.Enlisted);
        
        // Suboficiais (NCO - Levels 7-10)
        public static readonly Rank PrimeiroSargento = Of("1º Sargento", 7, RankType.NonCommissioned);
        public static readonly Rank Suboficial = Of("Suboficial", 8, RankType.NonCommissioned);
        
        // Oficiais Subalternos (Junior Officers - Levels 11-13)
        public static readonly Rank GuardaMarinha = Of("Guarda-Marinha", 11, RankType.Officer);
        public static readonly Rank SegundoTenente = Of("2º Tenente", 12, RankType.Officer);
        public static readonly Rank PrimeiroTenente = Of("1º Tenente", 13, RankType.Officer);
        
        // Oficiais Intermediários (Intermediate Officers - Levels 14-15)
        public static readonly Rank CapitaoTenente = Of("Capitão-Tenente", 14, RankType.Officer);
        public static readonly Rank CapitaoCorveta = Of("Capitão de Corveta", 15, RankType.Officer);
        
        // Oficiais Superiores (Senior Officers - Levels 16-17)
        public static readonly Rank CapitaoFragata = Of("Capitão de Fragata", 16, RankType.Officer);
        public static readonly Rank CapitaoMar = Of("Capitão de Mar e Guerra", 17, RankType.Officer);
        
        // Oficiais Generais (Flag Officers - Levels 18-20)
        public static readonly Rank ContraAlmirante = Of("Contra-Almirante", 18, RankType.Officer);
        public static readonly Rank ViceAlmirante = Of("Vice-Almirante", 19, RankType.Officer);
        public static readonly Rank Almirante = Of("Almirante", 20, RankType.Officer);
    }

    public bool IsHigherThan(Rank other)
    {
        return Level > other.Level;
    }

    public bool IsLowerThan(Rank other)
    {
        return Level < other.Level;
    }

    public static implicit operator string(Rank rank)
    {
        return rank.Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
