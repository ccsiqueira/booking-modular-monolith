using Flight.Aircrafts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flight.Data.Configurations;

using System;
using Aircrafts.ValueObjects;

public class AircraftConfiguration : IEntityTypeConfiguration<Aircraft>
{
    public void Configure(EntityTypeBuilder<Aircraft> builder)
    {

        builder.ToTable(nameof(Aircraft));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Version).IsConcurrencyToken();

        // Ignore the AircraftId helper property (it's computed from Id)
        builder.Ignore(r => r.AircraftId);

        builder.OwnsOne(
            x => x.Name,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName(nameof(Aircraft.Name))
                    .HasMaxLength(50)
                    .IsRequired();
            }
        );

        builder.OwnsOne(
            x => x.Model,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName(nameof(Aircraft.Model))
                    .HasMaxLength(50)
                    .IsRequired();
            }
        );

        builder.OwnsOne(
            x => x.ManufacturingYear,
            a =>
            {
                a.Property(p => p.Value)
                    .HasColumnName(nameof(Aircraft.ManufacturingYear))
                    .HasMaxLength(5)
                    .IsRequired();
            }
        );

        // Configure telemetry value objects as owned types (nullable since they're only set after telemetry updates)
        builder.OwnsOne(
            x => x.CurrentPosition,
            pos =>
            {
                pos.Property(p => p.Latitude).HasColumnName("Latitude").HasPrecision(18, 6);
                pos.Property(p => p.Longitude).HasColumnName("Longitude").HasPrecision(18, 6);
                pos.Property(p => p.Altitude).HasColumnName("Altitude").HasPrecision(18, 2);
            }
        );

        builder.OwnsOne(
            x => x.CurrentAttitude,
            att =>
            {
                att.Property(p => p.Roll).HasColumnName("Roll").HasPrecision(18, 6);
                att.Property(p => p.Pitch).HasColumnName("Pitch").HasPrecision(18, 6);
                att.Property(p => p.Yaw).HasColumnName("Yaw").HasPrecision(18, 6);
            }
        );

        builder.OwnsOne(
            x => x.CurrentTelemetry,
            tel =>
            {
                tel.Property(p => p.Speed).HasColumnName("Speed").HasPrecision(18, 2);
                tel.Property(p => p.Heading).HasColumnName("TelemetryHeading").HasPrecision(18, 6);
                tel.Property(p => p.FuelLevel).HasColumnName("FuelLevel").HasPrecision(18, 2);
                tel.Property(p => p.FlightPhase).HasColumnName("FlightPhase").HasMaxLength(50);
            }
        );

        // Configure telemetry timestamp
        builder.Property(x => x.LastTelemetryUpdate).HasColumnName("LastTelemetryUpdate");
    }
}