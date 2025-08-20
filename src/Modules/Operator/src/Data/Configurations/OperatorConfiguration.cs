using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Operator.Data.Configurations;

using Operator.Operators.ValueObjects;
using Operator.Operators.Enums;

public class OperatorConfiguration : IEntityTypeConfiguration<Operators.Models.Operator>
{
    public void Configure(EntityTypeBuilder<Operators.Models.Operator> builder)
    {
        builder.ToTable(nameof(Operators.Models.Operator));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever()
            .HasConversion(operatorId => operatorId.Value, dbId => OperatorId.New());

        builder.Property(r => r.Version).IsConcurrencyToken();

        builder.OwnsOne(
            x => x.Name,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName(nameof(Operators.Models.Operator.Name))
                    .HasMaxLength(100)
                    .IsRequired();
            }
        );

        builder.OwnsOne(
            x => x.WarName,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName(nameof(Operators.Models.Operator.WarName))
                    .HasMaxLength(40)
                    .IsRequired();
            }
        );

        builder.OwnsOne(
            x => x.Rank,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName("RankName")
                    .HasMaxLength(50)
                    .IsRequired();
                    
                a.Property(p => p.Level)
                    .HasColumnName("RankLevel")
                    .IsRequired();
                    
                a.Property(p => p.Type)
                    .HasColumnName("RankType")
                    .IsRequired()
                    .HasConversion(
                        x => x.ToString(),
                        x => (RankType)Enum.Parse(typeof(RankType), x));
            }
        );

        builder.OwnsOne(
            x => x.Organization,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName("OrganizationName")
                    .HasMaxLength(200)
                    .IsRequired();
                    
                a.Property(p => p.Code)
                    .HasColumnName("OrganizationCode")
                    .HasMaxLength(10)
                    .IsRequired();
            }
        );
    }
}
