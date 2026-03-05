// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.

using System.Text.RegularExpressions;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Spectre.Console;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Builds the detailed view for a single aircraft as a table with viewport scrolling.
/// Displays all aircraft properties organized by section (identification, status, position, etc.).
/// </summary>
internal static class LiveAircraftDetailBuilder
{
    /// <summary>
    /// Builds detailed view for a single aircraft as a table (matching main table width).
    /// </summary>
    /// <param name="aircraft">Aircraft to display detailed information for.</param>
    /// <param name="distanceUnit">Unit to display distances (miles or kilometers).</param>
    /// <param name="altitudeUnit">Unit to display altitudes (feet or meters).</param>
    /// <param name="speedUnit">Unit to display speeds (knots, km/h, or mph).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <param name="selectedRow">Currently selected row index for highlighting (0-based, validated to skip headers).</param>
    /// <returns>Spectre.Console Table with detailed aircraft information and fixed 120-character width.</returns>
    public static (Table Table, List<DetailRow> DetailRows) Build(
        Aircraft aircraft,
        DistanceUnit distanceUnit,
        AltitudeUnit altitudeUnit,
        SpeedUnit speedUnit,
        ReceiverConfig? receiverConfig,
        int selectedRow = 0)
    {
        // Calculate available viewport rows based on terminal height
        // Layout: title (1) + table header (1) + data rows + footer (2) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 1;        // Title row with ICAO
        const int footerLines = 2;        // Two-line footer with navigation hints
        const int tableHeaderLines = 1;   // Column header row (Field | Value | Scrollbar)
        const int padding = 3;            // Border and spacing overhead

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);

        // Collect all detail rows first (for viewport windowing)
        var allRows = new List<DetailRow>
        {
            // === IDENTIFICATION ===
            new("[bold]=== IDENTIFICATION =====================[/]", "", IsSectionHeader: true),
            new("ICAO Address", aircraft.Identification.ICAO),
            new("Callsign", aircraft.Identification.Callsign ?? "N/A"),
            new("Category", aircraft.Identification.Category?.ToString() ?? "N/A"),
            new("Squawk", aircraft.Identification.Squawk ?? "N/A"),
            new("Emergency", aircraft.Identification.EmergencyState.ToString()),
            new("Flight Status", aircraft.Identification.FlightStatus?.ToString() ?? "N/A"),
            new("ADS-B Version", aircraft.Identification.Version?.ToString() ?? "N/A"),
        };

        // === AIRCRAFT DETAILS ===
        allRows.Add(new DetailRow("", "", IsSectionHeader: true));
        allRows.Add(new DetailRow("[bold]=== AIRCRAFT DETAILS ===================[/]", "", IsSectionHeader: true));

        if (!aircraft.DatabaseEnabled)
        {
            allRows.Add(new DetailRow("No valid database configured", "", IsSectionHeader: true));
        }
        else
        {
            AircraftDatabaseRecord db = aircraft.DatabaseRecord;
            allRows.AddRange([
                new DetailRow("Registration", db.Registration ?? "N/A"),
                new DetailRow("Registration Country", db.Country ?? "N/A"),
                new DetailRow("Operator Name", db.OperatorName ?? "N/A"),
                new DetailRow("Manufacturer ICAO", db.ManufacturerIcao ?? "N/A"),
                new DetailRow("Manufacturer Name", db.ManufacturerName ?? "N/A"),
                new DetailRow("Type Class ICAO", db.TypeIcaoClass ?? "N/A"),
                new DetailRow("Type Designator", db.TypeCode ?? "N/A"),
                new DetailRow("Type Description", db.TypeDescription ?? "N/A"),
                new DetailRow("Aircraft Model", db.Model ?? "N/A"),
                new DetailRow("FAA PIA (Privacy)", db.Pia.HasValue ? (db.Pia.Value ? "Yes" : "No") : "N/A"),
                new DetailRow("FAA LADD (Limiting)", db.Ladd.HasValue ? (db.Ladd.Value ? "Yes" : "No") : "N/A"),
                new DetailRow("Military", db.Military.HasValue ? (db.Military.Value ? "Yes" : "No") : "N/A")
            ]);
        }

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === STATUS ===
        allRows.AddRange([
            new DetailRow("[bold]=== STATUS =============================[/]", "", IsSectionHeader: true),
            new DetailRow("First Seen", aircraft.Status.FirstSeen.ToString("HH:mm:ss")),
            new DetailRow("Last Seen", $"{(DateTime.UtcNow - aircraft.Status.LastSeen).TotalSeconds:F1}s ago"),
            new DetailRow("Total Messages", aircraft.Status.TotalMessages.ToString()),
            new DetailRow("Position Messages", aircraft.Status.PositionMessages.ToString()),
            new DetailRow("Velocity Messages", aircraft.Status.VelocityMessages.ToString()),
            new DetailRow("ID Messages", aircraft.Status.IdentificationMessages.ToString())
        ]);

        string signalStrength = aircraft.Status is { SignalStrength: not null, SignalStrengthDecibel: not null }
            ? $"{aircraft.Status.SignalStrengthDecibel.Value:F1} dBFS (RSSI: {aircraft.Status.SignalStrength.Value:F1})"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Signal Strength", signalStrength));
        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === POSITION ===
        allRows.Add(new DetailRow("[bold]=== POSITION ===========================[/]", "", IsSectionHeader: true));

        string latitude = aircraft.Position.Coordinate != null
            ? $"{aircraft.Position.Coordinate.Latitude:F6}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Latitude", latitude));

        string longitude = aircraft.Position.Coordinate != null
            ? $"{aircraft.Position.Coordinate.Longitude:F6}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Longitude", longitude));

        // Distance
        string distance;
        if (aircraft.Position.Coordinate != null && receiverConfig?.Latitude.HasValue == true && receiverConfig?.Longitude.HasValue == true)
        {
            var receiverLocation = new GeographicCoordinate(
                receiverConfig.Latitude.Value,
                receiverConfig.Longitude.Value);

            double dist = distanceUnit == DistanceUnit.Miles
                ? receiverLocation.DistanceToMiles(aircraft.Position.Coordinate)
                : receiverLocation.DistanceToKilometers(aircraft.Position.Coordinate);

            string unitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
            distance = $"{dist:F1} {unitLabel}";
        }
        else if (aircraft.Position.Coordinate != null)
        {
            distance = "N/A (no receiver location)";
        }
        else
        {
            distance = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Distance", distance));

        // Barometric altitude
        string baroAlt;
        if (aircraft.Position.BarometricAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                baroAlt = $"{aircraft.Position.BarometricAltitude.Feet:F0} ft";
            }
            else
            {
                double meters = aircraft.Position.BarometricAltitude.Feet * 0.3048;
                baroAlt = $"{meters:F0} m";
            }
        }
        else
        {
            baroAlt = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Barometric Altitude", baroAlt));

        // Geometric altitude
        string geoAlt;
        if (aircraft.Position.GeometricAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                geoAlt = $"{aircraft.Position.GeometricAltitude.Feet:F0} ft";
            }
            else
            {
                double meters = aircraft.Position.GeometricAltitude.Feet * 0.3048;
                geoAlt = $"{meters:F0} m";
            }
        }
        else
        {
            geoAlt = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Geometric Altitude (WGS84)", geoAlt));

        allRows.Add(new DetailRow("On Ground", aircraft.Position.IsOnGround.ToString()));

        // Movement category (ground only)
        string movementCategory = aircraft.Position.MovementCategory?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Movement Category", movementCategory));

        // Antenna configuration
        string antenna = aircraft.Position.Antenna.HasValue
            ? (aircraft.Position.Antenna.Value == AntennaFlag.SingleAntenna ? "Single Antenna" : "Diversity Antenna")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Antenna", antenna));

        allRows.Add(new DetailRow("NACp", aircraft.Position.NACp?.ToString() ?? "N/A (no data yet)"));

        // NICbaro - barometric altitude integrity
        string nicBaro = aircraft.Position.NICbaro.HasValue
            ? (aircraft.Position.NICbaro.Value ? "Cross-checked" : "Not cross-checked")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NICbaro", nicBaro));

        allRows.Add(new DetailRow("SIL", aircraft.Position.SIL?.ToString() ?? "N/A (no data yet)"));

        // Last position update timestamp
        string posLastUpdate = aircraft.Position.LastUpdate.HasValue
            ? aircraft.Position.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", posLastUpdate));

        // Position source (shows where the current position came from)
        string positionSource = aircraft.Position.PositionSource.HasValue
            ? aircraft.Position.PositionSource.Value.ToString()
            : "N/A (no position yet)";
        allRows.Add(new DetailRow("Position Source", positionSource));

        // MLAT history (shows if aircraft ever had MLAT position)
        string hadMlatPosition = aircraft.Position.HadMlatPosition ? "Yes" : "No";
        allRows.Add(new DetailRow("Had MLAT Position", hadMlatPosition));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === VELOCITY ===
        allRows.Add(new DetailRow("[bold]=== VELOCITY ===========================[/]", "", IsSectionHeader: true));

        Velocity? speedValue = aircraft.Velocity.Speed ?? aircraft.Velocity.GroundSpeed;
        string speed;
        if (speedValue != null)
        {
            double displaySpeed = speedUnit switch
            {
                SpeedUnit.Knots => speedValue.Knots,
                SpeedUnit.KilometersPerHour => speedValue.Knots * 1.852,
                SpeedUnit.MilesPerHour => speedValue.Knots * 1.15078,
                _ => speedValue.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            string velocityTypeStr = aircraft.Velocity.VelocitySubtype switch
            {
                VelocitySubtype.GroundSpeedSubsonic or VelocitySubtype.GroundSpeedSupersonic => "Ground Speed",
                VelocitySubtype.AirspeedSubsonic or VelocitySubtype.AirspeedSupersonic => "Airspeed",
                _ => "Unknown"
            };

            speed = $"{displaySpeed:F0} {unitLabel} ({velocityTypeStr})";
        }
        else
        {
            speed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Speed", speed));

        string heading = aircraft.Velocity.Heading != null
            ? $"{aircraft.Velocity.Heading:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Heading", heading));

        string track = aircraft.Velocity.Track != null
            ? $"{aircraft.Velocity.Track:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track", track));

        string groundTrack = aircraft.Velocity.GroundTrack != null
            ? $"{aircraft.Velocity.GroundTrack:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Ground Track", groundTrack));

        string verticalRate = aircraft.Velocity.VerticalRate != null
            ? $"{aircraft.Velocity.VerticalRate:F0} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Vertical Rate", verticalRate));

        // Indicated Airspeed (IAS from Comm-B)
        string indicatedAirspeed;
        if (aircraft.Velocity.CommBIndicatedAirspeed != null)
        {
            double displayIAS = speedUnit switch
            {
                SpeedUnit.Knots => aircraft.Velocity.CommBIndicatedAirspeed.Knots,
                SpeedUnit.KilometersPerHour => aircraft.Velocity.CommBIndicatedAirspeed.Knots * 1.852,
                SpeedUnit.MilesPerHour => aircraft.Velocity.CommBIndicatedAirspeed.Knots * 1.15078,
                _ => aircraft.Velocity.CommBIndicatedAirspeed.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            indicatedAirspeed = $"{displayIAS:F0} {unitLabel} (IAS)";
        }
        else
        {
            indicatedAirspeed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Comm-B Indicated Airspeed", indicatedAirspeed));

        // True Airspeed (TAS from Comm-B)
        string trueAirspeed;
        if (aircraft.Velocity.CommBTrueAirspeed != null)
        {
            double displayTAS = speedUnit switch
            {
                SpeedUnit.Knots => aircraft.Velocity.CommBTrueAirspeed.Knots,
                SpeedUnit.KilometersPerHour => aircraft.Velocity.CommBTrueAirspeed.Knots * 1.852,
                SpeedUnit.MilesPerHour => aircraft.Velocity.CommBTrueAirspeed.Knots * 1.15078,
                _ => aircraft.Velocity.CommBTrueAirspeed.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            trueAirspeed = $"{displayTAS:F0} {unitLabel} (TAS)";
        }
        else
        {
            trueAirspeed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Comm-B True Airspeed", trueAirspeed));

        // True Airspeed (GS from Comm-B)
        string groundSpeed;
        if (aircraft.Velocity.CommBGroundSpeed != null)
        {
            double displayGS = speedUnit switch
            {
                SpeedUnit.Knots => aircraft.Velocity.CommBGroundSpeed.Knots,
                SpeedUnit.KilometersPerHour => aircraft.Velocity.CommBGroundSpeed.Knots * 1.852,
                SpeedUnit.MilesPerHour => aircraft.Velocity.CommBGroundSpeed.Knots * 1.15078,
                _ => aircraft.Velocity.CommBGroundSpeed.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            groundSpeed = $"{displayGS:F0} {unitLabel} (GS)";
        }
        else
        {
            groundSpeed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Comm-B Ground Speed", groundSpeed));

        // Navigation Accuracy Category for Velocity
        string nacv = aircraft.Velocity.NACv?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACv", nacv));

        // Last velocity update timestamp
        string velLastUpdate = aircraft.Velocity.LastUpdate.HasValue
            ? aircraft.Velocity.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", velLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === AUTOPILOT ===
        allRows.Add(new DetailRow("[bold]=== AUTOPILOT ==========================[/]", "", IsSectionHeader: true));

        // Selected Altitude
        string selectedAltitude;
        if (aircraft.Autopilot?.SelectedAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                selectedAltitude = $"{aircraft.Autopilot.SelectedAltitude.Feet:F0} ft";
            }
            else
            {
                double meters = aircraft.Autopilot.SelectedAltitude.Feet * 0.3048;
                selectedAltitude = $"{meters:F0} m";
            }
        }
        else
        {
            selectedAltitude = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Selected Altitude", selectedAltitude));

        // Altitude Source
        string altitudeSource = aircraft.Autopilot?.AltitudeSource?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Altitude Source", altitudeSource));

        // Selected Heading
        string selectedHeading = aircraft.Autopilot?.SelectedHeading.HasValue == true
            ? $"{aircraft.Autopilot.SelectedHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Selected Heading", selectedHeading));

        // Barometric Pressure Setting
        string barometricPressure = aircraft.Autopilot?.BarometricPressureSetting.HasValue == true
            ? $"{aircraft.Autopilot.BarometricPressureSetting.Value:F1} hPa"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Barometric Pressure", barometricPressure));

        // Vertical Mode
        string verticalMode = aircraft.Autopilot?.VerticalMode?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Vertical Mode", verticalMode));

        // Horizontal Mode
        string horizontalMode = aircraft.Autopilot?.HorizontalMode?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Horizontal Mode", horizontalMode));

        // Autopilot Engaged
        string autopilotEngaged = aircraft.Autopilot?.AutopilotEngaged.HasValue == true
            ? (aircraft.Autopilot.AutopilotEngaged.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Autopilot Engaged", autopilotEngaged));

        // VNAV Mode
        string vnavMode = aircraft.Autopilot?.VNAVMode.HasValue == true
            ? (aircraft.Autopilot.VNAVMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("VNAV Mode", vnavMode));

        // LNAV Mode
        string lnavMode = aircraft.Autopilot?.LNAVMode.HasValue == true
            ? (aircraft.Autopilot.LNAVMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("LNAV Mode", lnavMode));

        // Altitude Hold Mode
        string altitudeHoldMode = aircraft.Autopilot?.AltitudeHoldMode.HasValue == true
            ? (aircraft.Autopilot.AltitudeHoldMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Altitude Hold", altitudeHoldMode));

        // Approach Mode
        string approachMode = aircraft.Autopilot?.ApproachMode.HasValue == true
            ? (aircraft.Autopilot.ApproachMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Approach Mode", approachMode));

        // Last autopilot update timestamp
        string autopilotLastUpdate = aircraft.Autopilot?.LastUpdate.HasValue == true
            ? aircraft.Autopilot.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", autopilotLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === ACAS/TCAS ===
        allRows.Add(new DetailRow("[bold]=== ACAS/TCAS ==========================[/]", "", IsSectionHeader: true));

        // TCAS Operational
        string tcasOperational = aircraft.Acas?.TCASOperational.HasValue == true
            ? (aircraft.Acas.TCASOperational.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS Operational", tcasOperational));

        // Sensitivity Level
        string sensitivityLevel = aircraft.Acas?.SensitivityLevel.HasValue == true
            ? (aircraft.Acas.SensitivityLevel.Value == 0 ? "0 (Inoperative)" : aircraft.Acas.SensitivityLevel.Value.ToString())
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Sensitivity Level", sensitivityLevel));

        // Cross-Link Capability
        string crossLinkCapability = aircraft.Acas?.CrossLinkCapability.HasValue == true
            ? (aircraft.Acas.CrossLinkCapability.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Cross-Link Capability", crossLinkCapability));

        // Reply Information
        string replyInformation = aircraft.Acas?.ReplyInformation?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Reply Information", replyInformation));

        // TCAS RA Active
        string tcasRaActive = aircraft.Acas?.TCASRAActive.HasValue == true
            ? (aircraft.Acas.TCASRAActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS RA Active", tcasRaActive));

        // RA Terminated
        string raTerminated = aircraft.Acas?.ResolutionAdvisoryTerminated.HasValue == true
            ? (aircraft.Acas.ResolutionAdvisoryTerminated.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RA Terminated", raTerminated));

        // Multiple Threats
        string multipleThreats = aircraft.Acas?.MultipleThreatEncounter.HasValue == true
            ? (aircraft.Acas.MultipleThreatEncounter.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Multiple Threats", multipleThreats));

        // RAC: Not Below
        string racNotBelow = aircraft.Acas?.RACNotBelow.HasValue == true
            ? (aircraft.Acas.RACNotBelow.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Below", racNotBelow));

        // RAC: Not Above
        string racNotAbove = aircraft.Acas?.RACNotAbove.HasValue == true
            ? (aircraft.Acas.RACNotAbove.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Above", racNotAbove));

        // RAC: Not Left
        string racNotLeft = aircraft.Acas?.RACNotLeft.HasValue == true
            ? (aircraft.Acas.RACNotLeft.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Left", racNotLeft));

        // RAC: Not Right
        string racNotRight = aircraft.Acas?.RACNotRight.HasValue == true
            ? (aircraft.Acas.RACNotRight.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Right", racNotRight));

        // Last ACAS update timestamp
        string acasLastUpdate = aircraft.Acas?.LastUpdate.HasValue == true
            ? aircraft.Acas.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", acasLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === FLIGHT DYNAMICS ===
        allRows.Add(new DetailRow("[bold]=== FLIGHT DYNAMICS ====================[/]", "", IsSectionHeader: true));

        // Roll Angle
        string rollAngle = aircraft.FlightDynamics?.RollAngle.HasValue == true
            ? $"{aircraft.FlightDynamics.RollAngle.Value:F2}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Roll Angle", rollAngle));

        // Magnetic Heading
        string magneticHeading = aircraft.FlightDynamics?.MagneticHeading.HasValue == true
            ? $"{aircraft.FlightDynamics.MagneticHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Magnetic Heading", magneticHeading));

        // Magnetic Declination
        string magneticDeclination = aircraft.FlightDynamics?.MagneticDeclination != null
            ? $"{aircraft.FlightDynamics.MagneticDeclination.Declination:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Magnetic Declination", magneticDeclination));

        // True Heading
        string trueHeading = aircraft.FlightDynamics?.TrueHeading.HasValue == true
            ? $"{aircraft.FlightDynamics.TrueHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("True Heading", trueHeading));

        // Barometric Vertical Rate
        string baroVerticalRate = aircraft.FlightDynamics?.BarometricVerticalRate.HasValue == true
            ? $"{aircraft.FlightDynamics.BarometricVerticalRate.Value:+0;-#} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Barometric Vert Rate", baroVerticalRate));

        // Inertial Vertical Rate
        string inertialVerticalRate = aircraft.FlightDynamics?.InertialVerticalRate.HasValue == true
            ? $"{aircraft.FlightDynamics.InertialVerticalRate.Value:+0;-#} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Inertial Vert Rate", inertialVerticalRate));

        // Mach Number
        string machNumber = aircraft.FlightDynamics?.MachNumber.HasValue == true
            ? $"M {aircraft.FlightDynamics.MachNumber.Value:F3}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Mach Number", machNumber));

        // Track Rate
        string trackRate = aircraft.FlightDynamics?.TrackRate.HasValue == true
            ? $"{aircraft.FlightDynamics.TrackRate.Value:+0.00;-0.00} °/s"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track Rate", trackRate));

        // Last flight dynamics update timestamp
        string flightDynamicsLastUpdate = aircraft.FlightDynamics?.LastUpdate.HasValue == true
            ? aircraft.FlightDynamics.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", flightDynamicsLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === METEOROLOGY ===
        allRows.Add(new DetailRow("[bold]=== METEOROLOGY ========================[/]", "", IsSectionHeader: true));

        // Wind Speed
        string windSpeed = aircraft.Meteo?.WindSpeed.HasValue == true
            ? $"{aircraft.Meteo.WindSpeed.Value} kts"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Wind Speed", windSpeed));

        // Wind Direction
        string windDirection = aircraft.Meteo?.WindDirection.HasValue == true
            ? $"{aircraft.Meteo.WindDirection.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Wind Direction", windDirection));

        // Total Air Temperature
        string totalAirTemp = aircraft.Meteo?.TotalAirTemperature.HasValue == true
            ? $"{aircraft.Meteo.TotalAirTemperature.Value:F1} °C"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Total Air Temperature", totalAirTemp));

        // Static Air Temperature
        string staticAirTemp = aircraft.Meteo?.StaticAirTemperature.HasValue == true
            ? $"{aircraft.Meteo.StaticAirTemperature.Value:F1} °C"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Static Air Temperature", staticAirTemp));

        // Pressure
        string pressure = aircraft.Meteo?.Pressure.HasValue == true
            ? $"{aircraft.Meteo.Pressure.Value:F1} hPa"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Pressure", pressure));

        // Turbulence
        string turbulence = aircraft.Meteo?.Turbulence?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Turbulence", turbulence));

        // Wind Shear
        string windShear = aircraft.Meteo?.WindShear?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Wind Shear", windShear));

        // Microburst
        string microburst = aircraft.Meteo?.Microburst?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Microburst", microburst));

        // Icing
        string icing = aircraft.Meteo?.Icing?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Icing", icing));

        // Wake Vortex
        string wakeVortex = aircraft.Meteo?.WakeVortex?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Wake Vortex", wakeVortex));

        // Radio Height
        string radioHeight;
        if (aircraft.Meteo?.RadioHeight.HasValue == true)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                radioHeight = $"{aircraft.Meteo.RadioHeight.Value} ft";
            }
            else
            {
                double meters = aircraft.Meteo.RadioHeight.Value * 0.3048;
                radioHeight = $"{(int)meters} m";
            }
        }
        else
        {
            radioHeight = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Radio Height", radioHeight));

        // Figure of Merit
        string figureOfMerit = aircraft.Meteo?.FigureOfMerit.HasValue == true
            ? aircraft.Meteo.FigureOfMerit.Value.ToString()
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Figure of Merit", figureOfMerit));

        // Humidity
        string humidity = aircraft.Meteo?.Humidity.HasValue == true
            ? $"{aircraft.Meteo.Humidity.Value:F1}%"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Humidity", humidity));

        // Last meteorological update timestamp
        string meteoLastUpdate = aircraft.Meteo?.LastUpdate.HasValue == true
            ? aircraft.Meteo.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", meteoLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === CAPABILITIES ===
        allRows.Add(new DetailRow("[bold]=== CAPABILITIES =======================[/]", "", IsSectionHeader: true));

        // Transponder Level
        string transponderLevel = aircraft.Capabilities?.TransponderLevel?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Transponder Level", transponderLevel));

        // TCAS Capability
        string tcasCapability = aircraft.Capabilities?.TCASCapability.HasValue == true
            ? (aircraft.Capabilities.TCASCapability.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS Capability", tcasCapability));

        // CDTI Available
        string cdtiAvailable = aircraft.Capabilities?.CockpitDisplayTraffic.HasValue == true
            ? (aircraft.Capabilities.CockpitDisplayTraffic.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("CDTI Available", cdtiAvailable));

        // ADS-B 1090ES
        string adsb1090es = aircraft.Capabilities?.ADSB1090ES.HasValue == true
            ? (aircraft.Capabilities.ADSB1090ES.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("ADS-B 1090ES", adsb1090es));

        // Air Referenced Velocity
        string airReferencedVelocity = aircraft.Capabilities?.AirReferencedVelocity.HasValue == true
            ? (aircraft.Capabilities.AirReferencedVelocity.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Air Referenced Velocity", airReferencedVelocity));

        // Target State Reporting
        string targetStateReporting = aircraft.Capabilities?.TargetStateReporting.HasValue == true
            ? (aircraft.Capabilities.TargetStateReporting.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Target State Reporting", targetStateReporting));

        // Trajectory Change Level
        string trajectoryChangeLevel = aircraft.Capabilities?.TrajectoryChangeLevel?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Trajectory Change Level", trajectoryChangeLevel));

        // UAT 978 Support
        string uat978Support = aircraft.Capabilities?.UAT978Support.HasValue == true
            ? (aircraft.Capabilities.UAT978Support.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("UAT 978 Support", uat978Support));

        // Position Offset Applied
        string positionOffsetApplied = aircraft.Capabilities?.PositionOffsetApplied.HasValue == true
            ? (aircraft.Capabilities.PositionOffsetApplied.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Position Offset Applied", positionOffsetApplied));

        // Low Power 1090ES
        string lowPower1090ES = aircraft.Capabilities?.LowPower1090ES.HasValue == true
            ? (aircraft.Capabilities.LowPower1090ES.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Low Power 1090ES", lowPower1090ES));

        // NACv (from Capabilities)
        string capabilitiesNacv = aircraft.Capabilities?.NACv?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACv", capabilitiesNacv));

        // NIC Supplement C
        string nicSupplementC = aircraft.Capabilities?.NICSupplementC.HasValue == true
            ? (aircraft.Capabilities.NICSupplementC.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NIC Supplement C", nicSupplementC));

        // Data Link Capability
        string dataLinkCapability = aircraft.Capabilities?.DataLinkCapabilityBits.HasValue == true
            ? $"0x{aircraft.Capabilities.DataLinkCapabilityBits.Value:X4}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Data Link Capability", dataLinkCapability));

        // Supported BDS Registers
        string supportedBdsRegisters = aircraft.Capabilities?.SupportedBDSRegisters.HasValue == true
            ? $"0x{aircraft.Capabilities.SupportedBDSRegisters.Value:X14}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Supported BDS Registers", supportedBdsRegisters));

        // Aircraft Dimensions
        string dimensions = aircraft.Capabilities?.Dimensions?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Aircraft Dimensions", dimensions));

        // Last capabilities update timestamp
        string capabilitiesLastUpdate = aircraft.Capabilities?.LastUpdate.HasValue == true
            ? aircraft.Capabilities.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", capabilitiesLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === DATA QUALITY ===
        allRows.Add(new DetailRow("[bold]=== DATA QUALITY =======================[/]", "", IsSectionHeader: true));

        // Geometric Vertical Accuracy
        string geometricVerticalAccuracy = aircraft.DataQuality?.GeometricVerticalAccuracy?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Geometric Vert Accuracy", geometricVerticalAccuracy));

        // NICbaro (TC 29)
        string nicBaroTc29 = aircraft.DataQuality?.NICbaro_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NICbaro (TC 29)", nicBaroTc29));

        // NIC Supplement A
        string nicSupplementA = aircraft.DataQuality?.NICSupplementA.HasValue == true
            ? (aircraft.DataQuality.NICSupplementA.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NIC Supplement A", nicSupplementA));

        // SIL Supplement
        string silSupplement = aircraft.DataQuality?.SILSupplement?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("SIL Supplement", silSupplement));

        // SIL (TC 29)
        string silTc29 = aircraft.DataQuality?.SIL_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("SIL (TC 29)", silTc29));

        // NACp (TC 29)
        string nacpTc29 = aircraft.DataQuality?.NACp_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACp (TC 29)", nacpTc29));

        // Horizontal Reference
        string horizontalReference = aircraft.DataQuality?.HorizontalReference?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Horizontal Reference", horizontalReference));

        // Heading Type
        string headingType = aircraft.DataQuality?.HeadingType?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Heading Type", headingType));

        // Last data quality update timestamp
        string dataQualityLastUpdate = aircraft.DataQuality?.LastUpdate.HasValue == true
            ? aircraft.DataQuality.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", dataQualityLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === OPERATIONAL MODE ===
        allRows.Add(new DetailRow("[bold]=== OPERATIONAL MODE ===================[/]", "", IsSectionHeader: true));

        // TCAS RA Active
        string opModeTcasRaActive = aircraft.OperationalMode?.TCASRAActive.HasValue == true
            ? (aircraft.OperationalMode.TCASRAActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS RA Active", opModeTcasRaActive));

        // IDENT Switch Active
        string identSwitchActive = aircraft.OperationalMode?.IdentSwitchActive.HasValue == true
            ? (aircraft.OperationalMode.IdentSwitchActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("IDENT Switch Active", identSwitchActive));

        // Receiving ATC Services
        string receivingAtcServices = aircraft.OperationalMode?.ReceivingATCServices.HasValue == true
            ? (aircraft.OperationalMode.ReceivingATCServices.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Receiving ATC Services", receivingAtcServices));

        // Antenna Configuration
        string antennaConfiguration = aircraft.OperationalMode?.SingleAntenna.HasValue == true
            ? (aircraft.OperationalMode.SingleAntenna.Value == AntennaFlag.SingleAntenna ? "Single" : "Diversity")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Antenna Configuration", antennaConfiguration));

        // System Design Assurance
        string systemDesignAssurance = aircraft.OperationalMode?.SystemDesignAssurance?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("System Design Assurance", systemDesignAssurance));

        // GPS Lateral Offset
        string gpsLateralOffset = aircraft.OperationalMode?.GPSLateralOffset?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("GPS Lateral Offset", gpsLateralOffset));

        // GPS Longitudinal Offset
        string gpsLongitudinalOffset = aircraft.OperationalMode?.GPSLongitudinalOffset?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("GPS Longitudinal Offset", gpsLongitudinalOffset));

        // Downlink Request
        string downlinkRequest = aircraft.OperationalMode?.DownlinkRequest.HasValue == true
            ? aircraft.OperationalMode.DownlinkRequest.Value.ToString()
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Downlink Request", downlinkRequest));

        // Utility Message
        string utilityMessage = aircraft.OperationalMode?.UtilityMessage.HasValue == true
            ? $"0x{aircraft.OperationalMode.UtilityMessage.Value:X2}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Utility Message", utilityMessage));

        // Last operational mode update timestamp
        string operationalModeLastUpdate = aircraft.OperationalMode?.LastUpdate.HasValue == true
            ? aircraft.OperationalMode.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", operationalModeLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // Calculate viewport (same logic as AircraftTableBuilder)
        int totalRows = allRows.Count;

        // Ensure selectedRow is not on a section header
        if (selectedRow < totalRows && allRows[selectedRow].IsSectionHeader)
        {
            // Find next non-header row
            int nextRow = selectedRow + 1;
            while (nextRow < totalRows && allRows[nextRow].IsSectionHeader)
            {
                nextRow++;
            }
            selectedRow = nextRow < totalRows ? nextRow : selectedRow;
        }

        int viewportStart;
        int viewportEnd;

        // If all rows fit on screen, don't apply viewport scrolling
        if (totalRows <= availableRows)
        {
            viewportStart = 0;
            viewportEnd = totalRows;
        }
        else
        {
            // Apply viewport scrolling logic when rows exceed available height
            int halfViewport = availableRows / 2;
            viewportStart = Math.Max(0, selectedRow - halfViewport);
            viewportEnd = Math.Min(totalRows, viewportStart + availableRows);

            // Adjust if at end of list
            if (viewportEnd - viewportStart < availableRows)
            {
                viewportStart = Math.Max(0, viewportEnd - availableRows);
            }
        }

        // Calculate scrollbar parameters
        bool showScrollbar = totalRows > availableRows;
        int thumbSize = 1;
        int thumbStart = 0;

        if (showScrollbar)
        {
            thumbSize = Math.Max(1, (int)Math.Floor((double)availableRows * availableRows / totalRows));
            int scrollableRange = totalRows - availableRows;
            if (scrollableRange > 0)
            {
                double scrollProgress = (double)viewportStart / scrollableRange;
                thumbStart = (int)Math.Floor(scrollProgress * (availableRows - thumbSize));
            }
        }

        // Create table with scrollbar column
        Table table = new Table()
            .Border(TableBorder.Square)
            .Title($"AIRCRAFT DETAIL ({aircraft.Identification.ICAO}) - Aeromux", new Style(decoration:Decoration.Bold))
            .AddColumn(new TableColumn("[bold]Field[/]").Width(40).NoWrap().PadLeft(1).PadRight(1))
            .AddColumn(new TableColumn("[bold]Value[/]").Width(53).NoWrap().PadLeft(1).PadRight(1))
            .AddColumn(new TableColumn("[bold] [/]").Width(1).Centered().NoWrap());

        // Render rows in viewport
        for (int i = viewportStart; i < viewportEnd; i++)
        {
            DetailRow row = allRows[i];
            bool isSelected = i == selectedRow;

            // Calculate scrollbar character for this row
            string scrollbarChar;
            if (showScrollbar)
            {
                int rowInViewport = i - viewportStart;
                scrollbarChar = (rowInViewport >= thumbStart && rowInViewport < thumbStart + thumbSize)
                    ? "█" : "░";
            }
            else
            {
                scrollbarChar = "░";  // Always show track
            }

            // Apply highlighting to selected row (but not scrollbar or section headers)
            if (isSelected && !row.IsSectionHeader)
            {
                table.AddRow(
                    $"[black on white]{row.Field,-40}[/]",
                    $"[black on white]{row.Value,-53}[/]",
                    scrollbarChar);  // Scrollbar keeps normal styling
            }
            else
            {
                table.AddRow(row.Field, row.Value, scrollbarChar);
            }
        }

        // Fill remaining rows
        int rowsRendered = viewportEnd - viewportStart;
        int emptyRowsNeeded = availableRows - rowsRendered;
        for (int i = 0; i < emptyRowsNeeded; i++)
        {
            table.AddRow("", "", "░");
        }

        // Build footer (2 rows with left/right alignment)
        string distUnitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
        string altUnitLabel = altitudeUnit == AltitudeUnit.Feet ? "ft" : "m";
        string speedUnitLabel = speedUnit switch
        {
            SpeedUnit.Knots => "kts",
            SpeedUnit.KilometersPerHour => "km/h",
            SpeedUnit.MilesPerHour => "mph",
            _ => "kts"
        };

        // Footer row 1: left and right sections
        string footerRow1Left = $"[bold]Row:[/] {selectedRow + 1}/{totalRows}";
        string footerRow1Right = $"[bold]Dist:[/] {distUnitLabel} | [bold]Alt:[/] {altUnitLabel} | [bold]Spd:[/] {speedUnitLabel}";

        // Footer row 2: left and right sections
        string footerRow2Left = "[bold]↑/↓[/]: Row, [bold]←/→[/]: Page, [bold]Home/End[/]";
        string footerRow2Right = "[bold]ESC[/]: Back, [bold]Q[/]: Quit";

        // Pad footer rows to exactly 100 visible characters (table is 104 wide, 2 indent per side)
        string footerRow1 = PadFooterRow(footerRow1Left, footerRow1Right);
        string footerRow2 = PadFooterRow(footerRow2Left, footerRow2Right);

        string footer = footerRow1 + "\n" + footerRow2;

        table.Caption(footer, new Style(foreground: Color.Grey));

        // Return table and allRows for navigation
        return (table, allRows);
    }

    /// <summary>
    /// Pads a footer row so left and right sections fill exactly 100 visible characters.
    /// Accounts for Spectre.Console markup tags that don't consume visible width.
    /// </summary>
    private static string PadFooterRow(string left, string right)
    {
        const int usableWidth = 100;
        int leftVisible = VisibleLength(left);
        int rightVisible = VisibleLength(right);
        int padding = Math.Max(1, usableWidth - leftVisible - rightVisible);
        return left + new string(' ', padding) + right;
    }

    /// <summary>
    /// Calculates the visible length of a string by stripping Spectre.Console markup tags.
    /// </summary>
    private static int VisibleLength(string markup)
    {
        string stripped = Regex.Replace(markup, @"\[/?[^\]]*\]", "");
        return stripped.Length;
    }
}
