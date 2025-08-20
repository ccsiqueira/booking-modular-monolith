namespace Flight.Data.Seed;

using System;
using System.Collections.Generic;
using System.Linq;
using Aircrafts.Models;
using Airports.Models;
using Airports.ValueObjects;
using Flight.Aircrafts.ValueObjects;
using Flights.Models;
using Flights.ValueObjects;
using MassTransit;
using Seats.Models;
using Seats.ValueObjects;
using AirportName = Airports.ValueObjects.Name;
using Name = Aircrafts.ValueObjects.Name;

public static class InitialData
{
    public static List<Airport> Airports { get; }
    public static List<Aircraft> Aircrafts { get; }
    public static List<Seat> Seats { get; }
    public static List<Flight> Flights { get; }


    static InitialData()
    {
        // Define IDs for reuse
        var aircraftId1 = AircraftId.Of(new Guid("3c5c0000-97c6-fc34-fcd3-08db322230c8"));
        var aircraftId2 = AircraftId.Of(new Guid("3c5c0000-97c6-fc34-2e04-08db322230c9"));
        var aircraftId3 = AircraftId.Of(new Guid("3c5c0000-97c6-fc34-2e11-08db322230c9"));
        
        var airportId1 = AirportId.Of(new Guid("3c5c0000-97c6-fc34-a0cb-08db322230c8"));
        var airportId2 = AirportId.Of(new Guid("3c5c0000-97c6-fc34-fc3c-08db322230c8"));
        
        var flightId1 = FlightId.Of(new Guid("3c5c0000-97c6-fc34-2eb9-08db322230c9"));

        Airports = new List<Airport>
        {
            Airport.Create(airportId1, AirportName.Of("Lisbon International Airport"), Address.Of("LIS"), Code.Of("12988")),
            Airport.Create(airportId2, AirportName.Of("Sao Paulo International Airport"), Address.Of("BRZ"), Code.Of("11200"))
        };

        Aircrafts = new List<Aircraft>
        {
            Aircraft.Create(aircraftId1, Name.Of("Boeing 737"), Model.Of("B737"), ManufacturingYear.Of(2005)),
            Aircraft.Create(aircraftId2, Name.Of("Airbus 300"), Model.Of("A300"), ManufacturingYear.Of(2000)),
            Aircraft.Create(aircraftId3, Name.Of("Airbus 320"), Model.Of("A320"), ManufacturingYear.Of(2003))
        };

        Flights = new List<Flight>
        {
            Flight.Create(flightId1, FlightNumber.Of("BD467"), aircraftId1, airportId1, DepartureDate.Of(new DateTime(2022, 1, 31, 12, 0, 0)),
               ArriveDate.Of(new DateTime(2022, 1, 31, 14, 0, 0)),
               airportId2, DurationMinutes.Of(120m),
                FlightDate.Of(new DateTime(2022, 1, 31, 13, 0, 0)), global::Flight.Flights.Enums.FlightStatus.Completed,
                Price.Of(8000))
        };

        Seats = new List<Seat>
        {
            Seat.Create(SeatId.Of(NewId.NextGuid()), SeatNumber.Of( "12A"), global::Flight.Seats.Enums.SeatType.Window, global::Flight.Seats.Enums.SeatClass.Economy, flightId1),
            Seat.Create(SeatId.Of(NewId.NextGuid()), SeatNumber.Of("12B"), global::Flight.Seats.Enums.SeatType.Window, global::Flight.Seats.Enums.SeatClass.Economy, flightId1),
            Seat.Create(SeatId.Of(NewId.NextGuid()), SeatNumber.Of("12C"), global::Flight.Seats.Enums.SeatType.Middle, global::Flight.Seats.Enums.SeatClass.Economy, flightId1),
            Seat.Create(SeatId.Of(NewId.NextGuid()), SeatNumber.Of("12D"), global::Flight.Seats.Enums.SeatType.Middle, global::Flight.Seats.Enums.SeatClass.Economy, flightId1),
            Seat.Create(SeatId.Of(NewId.NextGuid()), SeatNumber.Of("12E"), global::Flight.Seats.Enums.SeatType.Aisle, global::Flight.Seats.Enums.SeatClass.Economy, flightId1),
            Seat.Create(SeatId.Of(NewId.NextGuid()), SeatNumber.Of("12F"), global::Flight.Seats.Enums.SeatType.Aisle, global::Flight.Seats.Enums.SeatClass.Economy, flightId1)
        };
    }
}