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
    /// <param name="isExpired">True if the aircraft has expired (timed out) — shows [EXPIRED] in title.</param>
    /// <param name="isDetailSearchActive">Whether detail search mode is currently active.</param>
    /// <param name="detailSearchInput">Current search input text for field name highlighting.</param>
    /// <returns>Spectre.Console Table with detailed aircraft information and fixed 120-character width.</returns>
    public static (Table Table, List<DetailRow> DetailRows) Build(
        Aircraft aircraft,
        DistanceUnit distanceUnit,
        AltitudeUnit altitudeUnit,
        SpeedUnit speedUnit,
        ReceiverConfig? receiverConfig,
        int selectedRow = 0,
        bool isExpired = false,
        bool isDetailSearchActive = false,
        string detailSearchInput = "")
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
        var allRows = new List<DetailRow>();

        // =====================================================================
        // Section 1: IDENTIFICATION
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== IDENTIFICATION =====================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Identity sub-section ---
        allRows.Add(new DetailRow("[dim]--- Identity ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
        allRows.Add(new DetailRow("ICAO Address", aircraft.Identification.ICAO));
        allRows.Add(new DetailRow("Callsign", aircraft.Identification.Callsign ?? "N/A"));
        allRows.Add(new DetailRow("Category", aircraft.Identification.Category?.ToString() ?? "N/A"));

        // --- Transponder sub-section ---
        allRows.Add(new DetailRow("[dim]--- Transponder ------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
        allRows.Add(new DetailRow("Squawk", aircraft.Identification.Squawk ?? "N/A"));
        allRows.Add(new DetailRow("Emergency", aircraft.Identification.EmergencyState.ToString()));
        allRows.Add(new DetailRow("Flight Status", aircraft.Identification.FlightStatus?.ToString() ?? "N/A"));

        // =====================================================================
        // Section 2: AIRCRAFT DATABASE
        // =====================================================================
        allRows.Add(new DetailRow("", "", IsSectionHeader: true));
        allRows.Add(new DetailRow("[bold]=== AIRCRAFT DATABASE ==================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        if (!aircraft.DatabaseEnabled)
        {
            allRows.Add(new DetailRow("No database configured. See README.md.", "", IsSectionHeader: true));
        }
        else
        {
            AircraftDatabaseRecord db = aircraft.DatabaseRecord;

            // --- Registration sub-section ---
            allRows.Add(new DetailRow("[dim]--- Registration -----------------------[/]",
                "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
            allRows.AddRange([
                new DetailRow("Registration", db.Registration ?? "N/A"),
                new DetailRow("Registration Country", db.Country ?? "N/A"),
                new DetailRow("Operator Name", db.OperatorName ?? "N/A"),
            ]);

            // --- Aircraft Type sub-section ---
            allRows.Add(new DetailRow("[dim]--- Aircraft Type ----------------------[/]",
                "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
            allRows.AddRange([
                new DetailRow("Manufacturer ICAO", db.ManufacturerIcao ?? "N/A"),
                new DetailRow("Manufacturer Name", db.ManufacturerName ?? "N/A"),
                new DetailRow("Type Class ICAO", db.TypeIcaoClass ?? "N/A"),
                new DetailRow("Type Designator", db.TypeCode ?? "N/A"),
                new DetailRow("Type Description", db.TypeDescription ?? "N/A"),
                new DetailRow("Aircraft Model", db.Model ?? "N/A"),
            ]);

            // --- Flags sub-section ---
            allRows.Add(new DetailRow("[dim]--- Flags ------------------------------[/]",
                "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
            allRows.AddRange([
                new DetailRow("FAA PIA (Privacy)", db.Pia.HasValue ? (db.Pia.Value ? "Yes" : "No") : "N/A"),
                new DetailRow("FAA LADD (Limiting)", db.Ladd.HasValue ? (db.Ladd.Value ? "Yes" : "No") : "N/A"),
                new DetailRow("Military", db.Military.HasValue ? (db.Military.Value ? "Yes" : "No") : "N/A")
            ]);
        }

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 3: STATUS
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== STATUS =============================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Timing sub-section ---
        allRows.Add(new DetailRow("[dim]--- Timing -----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
        allRows.Add(new DetailRow("First Seen", aircraft.Status.FirstSeen.ToString("HH:mm:ss")));
        allRows.Add(new DetailRow("Last Seen", $"{(DateTime.UtcNow - aircraft.Status.LastSeen).TotalSeconds:F1}s ago"));

        // --- Messages sub-section ---
        allRows.Add(new DetailRow("[dim]--- Messages ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));
        allRows.Add(new DetailRow("Total Messages", aircraft.Status.TotalMessages.ToString()));
        allRows.Add(new DetailRow("Position Messages", aircraft.Status.PositionMessages.ToString()));
        allRows.Add(new DetailRow("Velocity Messages", aircraft.Status.VelocityMessages.ToString()));
        allRows.Add(new DetailRow("ID Messages", aircraft.Status.IdentificationMessages.ToString()));

        string signalStrength = aircraft.Status is { SignalStrength: not null, SignalStrengthDecibel: not null }
            ? $"{aircraft.Status.SignalStrengthDecibel.Value:F1} dBFS (RSSI: {aircraft.Status.SignalStrength.Value:F1})"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Signal Strength", signalStrength));
        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 4: POSITION
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== POSITION ===========================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Coordinates sub-section ---
        allRows.Add(new DetailRow("[dim]--- Coordinates ------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Altitude sub-section ---
        allRows.Add(new DetailRow("[dim]--- Altitude ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // GNSS-Baro Offset (TC 19)
        string gnssBaro;
        if (aircraft.Position.GeometricBarometricDelta.HasValue)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                gnssBaro = $"{aircraft.Position.GeometricBarometricDelta.Value:+0;-#} ft";
            }
            else
            {
                double meters = aircraft.Position.GeometricBarometricDelta.Value * 0.3048;
                gnssBaro = $"{(int)meters:+0;-#} m";
            }
        }
        else
        {
            gnssBaro = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Barometric Offset (GNSS)", gnssBaro));

        // --- State sub-section ---
        allRows.Add(new DetailRow("[dim]--- State ------------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        allRows.Add(new DetailRow("On Ground", aircraft.Position.IsOnGround.ToString()));

        // Movement category (ground only)
        string movementCategory = aircraft.Position.MovementCategory?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Movement Category", movementCategory));

        // Position source
        string positionSource = aircraft.Position.PositionSource.HasValue
            ? aircraft.Position.PositionSource.Value.ToString()
            : "N/A (no position yet)";
        allRows.Add(new DetailRow("Position Source", positionSource));

        // MLAT history
        string hadMlatPosition = aircraft.Position.HadMlatPosition ? "Yes" : "No";
        allRows.Add(new DetailRow("Had MLAT Position", hadMlatPosition));

        // Last position update timestamp
        string posLastUpdate = aircraft.Position.LastUpdate.HasValue
            ? aircraft.Position.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", posLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 5: VELOCITY & DYNAMICS
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== VELOCITY & DYNAMICS ================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Speed sub-section ---
        allRows.Add(new DetailRow("[dim]--- Speed ------------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Speed (TC 19)
        string speedTc19;
        if (aircraft.Velocity.Speed != null)
        {
            double displaySpeed = speedUnit switch
            {
                SpeedUnit.Knots => aircraft.Velocity.Speed.Knots,
                SpeedUnit.KilometersPerHour => aircraft.Velocity.Speed.Knots * 1.852,
                SpeedUnit.MilesPerHour => aircraft.Velocity.Speed.Knots * 1.15078,
                _ => aircraft.Velocity.Speed.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            string velocityTypeStr = aircraft.Velocity.Speed.Type switch
            {
                VelocityType.GroundSpeed => "GS",
                VelocityType.IndicatedAirspeed => "IAS",
                VelocityType.TrueAirspeed => "TAS",
                _ => "Unknown"
            };

            speedTc19 = $"{displaySpeed:F0} {unitLabel} ({velocityTypeStr})";
        }
        else
        {
            speedTc19 = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Speed", speedTc19));

        // Indicated Airspeed (BDS 6,0)
        string indicatedAirspeed = FormatSpeed(aircraft.Velocity.CommBIndicatedAirspeed, speedUnit, " (IAS)");
        allRows.Add(new DetailRow("Indicated Airspeed", indicatedAirspeed));

        // True Airspeed (BDS 5,0)
        string trueAirspeed = FormatSpeed(aircraft.Velocity.CommBTrueAirspeed, speedUnit, " (TAS)");
        allRows.Add(new DetailRow("True Airspeed", trueAirspeed));

        // Ground Speed (BDS 5,0)
        string groundSpeedBds50 = FormatSpeed(aircraft.Velocity.CommBGroundSpeed, speedUnit, " (GS)");
        allRows.Add(new DetailRow("Ground Speed", groundSpeedBds50));

        // Mach Number (BDS 6,0)
        string machNumber = aircraft.FlightDynamics?.MachNumber.HasValue == true
            ? $"M {aircraft.FlightDynamics.MachNumber.Value:F3}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Mach Number", machNumber));

        // --- Direction sub-section ---
        allRows.Add(new DetailRow("[dim]--- Direction --------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Track (TC 19)
        string track = aircraft.Velocity.Track != null
            ? $"{aircraft.Velocity.Track:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track [grey](TC 19)[/]", track));

        // Track Angle (BDS 5,0)
        string trackAngle = aircraft.Velocity.TrackAngle.HasValue
            ? $"{aircraft.Velocity.TrackAngle.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track [grey](BDS 5,0)[/]", trackAngle));

        // Magnetic Heading (BDS 6,0)
        string magneticHeading = aircraft.FlightDynamics?.MagneticHeading.HasValue == true
            ? $"{aircraft.FlightDynamics.MagneticHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Magnetic Heading", magneticHeading));

        // True Heading (BDS 5,0)
        string trueHeading = aircraft.FlightDynamics?.TrueHeading.HasValue == true
            ? $"{aircraft.FlightDynamics.TrueHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("True Heading", trueHeading));

        // Magnetic Declination
        string magneticDeclination = aircraft.FlightDynamics?.MagneticDeclination != null
            ? $"{aircraft.FlightDynamics.MagneticDeclination.Declination:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Magnetic Declination", magneticDeclination));

        // Heading (TC 19)
        string heading = aircraft.Velocity.Heading != null
            ? $"{aircraft.Velocity.Heading:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Heading", heading));

        // Heading Type (TC 31)
        string headingType = aircraft.DataQuality?.HeadingType?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Heading Type", headingType));

        // Horizontal Reference (TC 31)
        string horizontalReference = aircraft.DataQuality?.HorizontalReference?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Horizontal Reference", horizontalReference));

        // --- Vertical sub-section ---
        allRows.Add(new DetailRow("[dim]--- Vertical ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Vertical Rate (TC 19)
        string verticalRate = aircraft.Velocity.VerticalRate != null
            ? $"{aircraft.Velocity.VerticalRate:F0} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Vertical Rate", verticalRate));

        // Barometric Vertical Rate (BDS 6,0)
        string baroVerticalRate = aircraft.FlightDynamics?.BarometricVerticalRate.HasValue == true
            ? $"{aircraft.FlightDynamics.BarometricVerticalRate.Value:+0;-#} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Barometric Vertical Rate", baroVerticalRate));

        // Inertial Vertical Rate (BDS 6,0)
        string inertialVerticalRate = aircraft.FlightDynamics?.InertialVerticalRate.HasValue == true
            ? $"{aircraft.FlightDynamics.InertialVerticalRate.Value:+0;-#} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Inertial Vertical Rate", inertialVerticalRate));

        // --- Dynamics sub-section ---
        allRows.Add(new DetailRow("[dim]--- Dynamics ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Roll Angle (BDS 5,0)
        string rollAngle = aircraft.FlightDynamics?.RollAngle.HasValue == true
            ? $"{aircraft.FlightDynamics.RollAngle.Value:F2}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Roll Angle", rollAngle));

        // Track Rate (BDS 5,0)
        string trackRate = aircraft.FlightDynamics?.TrackRate.HasValue == true
            ? $"{aircraft.FlightDynamics.TrackRate.Value:+0.00;-0.00} °/s"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track Rate", trackRate));

        // --- On Ground sub-section ---
        allRows.Add(new DetailRow("[dim]--- On Ground --------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Ground Speed (TC 5-8)
        string groundSpeedTc58 = FormatSpeed(aircraft.Velocity.GroundSpeed, speedUnit);
        allRows.Add(new DetailRow("Speed On Ground", groundSpeedTc58));

        // Ground Track (TC 5-8)
        string groundTrack = aircraft.Velocity.GroundTrack != null
            ? $"{aircraft.Velocity.GroundTrack:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track On Ground", groundTrack));

        // Last Update = Max(TrackedVelocity, TrackedFlightDynamics)
        string velDynLastUpdate = FormatMaxLastUpdate(
            aircraft.Velocity.LastUpdate,
            aircraft.FlightDynamics?.LastUpdate);
        allRows.Add(new DetailRow("Last Update", velDynLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 6: AUTOPILOT
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== AUTOPILOT ==========================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Status sub-section ---
        allRows.Add(new DetailRow("[dim]--- Status -----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Autopilot Engaged
        string autopilotEngaged = aircraft.Autopilot?.AutopilotEngaged.HasValue == true
            ? (aircraft.Autopilot.AutopilotEngaged.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Autopilot Engaged", autopilotEngaged));

        // --- Targets sub-section ---
        allRows.Add(new DetailRow("[dim]--- Targets ----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Modes sub-section ---
        allRows.Add(new DetailRow("[dim]--- Modes ------------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Vertical Mode
        string verticalMode = aircraft.Autopilot?.VerticalMode?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Vertical Mode", verticalMode));

        // Horizontal Mode
        string horizontalMode = aircraft.Autopilot?.HorizontalMode?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Horizontal Mode", horizontalMode));

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

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 7: METEOROLOGY
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== METEOROLOGY ========================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Wind sub-section ---
        allRows.Add(new DetailRow("[dim]--- Wind -------------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Temperature sub-section ---
        allRows.Add(new DetailRow("[dim]--- Temperature ------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Pressure & Altitude sub-section ---
        allRows.Add(new DetailRow("[dim]--- Pressure & Altitude ----------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Pressure
        string pressure = aircraft.Meteo?.Pressure.HasValue == true
            ? $"{aircraft.Meteo.Pressure.Value:F1} hPa"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Pressure", pressure));

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

        // --- Hazards sub-section ---
        allRows.Add(new DetailRow("[dim]--- Hazards ----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Quality sub-section ---
        allRows.Add(new DetailRow("[dim]--- Quality ----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Humidity
        string humidity = aircraft.Meteo?.Humidity.HasValue == true
            ? $"{aircraft.Meteo.Humidity.Value:F1}%"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Humidity", humidity));

        // Figure of Merit
        string figureOfMerit = aircraft.Meteo?.FigureOfMerit.HasValue == true
            ? aircraft.Meteo.FigureOfMerit.Value.ToString()
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Figure of Merit", figureOfMerit));

        // Last meteorological update timestamp
        string meteoLastUpdate = aircraft.Meteo?.LastUpdate.HasValue == true
            ? aircraft.Meteo.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", meteoLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 8: ACAS/TCAS
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== ACAS/TCAS ==========================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- System sub-section ---
        allRows.Add(new DetailRow("[dim]--- System -----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Resolution Advisory subsection ---
        allRows.Add(new DetailRow("[dim]--- Resolution Advisory ----------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // TCAS RA Active (BDS 3,0)
        string tcasRaActiveBds30 = aircraft.Acas?.TCASRAActive.HasValue == true
            ? (aircraft.Acas.TCASRAActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS RA Active [grey](BDS 3,0)[/]", tcasRaActiveBds30));

        // TCAS RA Active (TC 31)
        string tcasRaActiveTc31 = aircraft.OperationalMode?.TCASRAActive.HasValue == true
            ? (aircraft.OperationalMode.TCASRAActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS RA Active [grey](TC 31)[/]", tcasRaActiveTc31));

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

        // --- Complementary sub-section ---
        allRows.Add(new DetailRow("[dim]--- Complementary ----------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // Last Update = Max(TrackedAcas, TrackedOperationalMode)
        string acasLastUpdate = FormatMaxLastUpdate(
            aircraft.Acas?.LastUpdate,
            aircraft.OperationalMode?.LastUpdate);
        allRows.Add(new DetailRow("Last Update", acasLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 9: CAPABILITIES
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== CAPABILITIES =======================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Equipment sub-section ---
        allRows.Add(new DetailRow("[dim]--- Equipment --------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // ADS-B Version
        allRows.Add(new DetailRow("ADS-B Version", aircraft.Identification.Version?.ToString() ?? "N/A"));

        // Transponder Level
        string transponderLevel = aircraft.Capabilities?.TransponderLevel?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Transponder Level", transponderLevel));

        // ADS-B 1090ES
        string adsb1090es = aircraft.Capabilities?.ADSB1090ES.HasValue == true
            ? (aircraft.Capabilities.ADSB1090ES.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("ADS-B 1090ES", adsb1090es));

        // UAT 978 Support
        string uat978Support = aircraft.Capabilities?.UAT978Support.HasValue == true
            ? (aircraft.Capabilities.UAT978Support.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("UAT 978 Support", uat978Support));

        // Low Power 1090ES
        string lowPower1090ES = aircraft.Capabilities?.LowPower1090ES.HasValue == true
            ? (aircraft.Capabilities.LowPower1090ES.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Low Power 1090ES", lowPower1090ES));

        // --- Features sub-section ---
        allRows.Add(new DetailRow("[dim]--- Features ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // Position Offset Applied
        string positionOffsetApplied = aircraft.Capabilities?.PositionOffsetApplied.HasValue == true
            ? (aircraft.Capabilities.PositionOffsetApplied.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Position Offset Applied", positionOffsetApplied));

        // --- Operational State sub-section ---
        allRows.Add(new DetailRow("[dim]--- Operational State ------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // --- Physical sub-section ---
        allRows.Add(new DetailRow("[dim]--- Physical ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Aircraft Dimensions
        string dimensions = aircraft.Capabilities?.Dimensions?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Aircraft Dimensions", dimensions));

        // GPS Lateral Offset
        string gpsLateralOffset = aircraft.OperationalMode?.GPSLateralOffset?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("GPS Lateral Offset", gpsLateralOffset));

        // GPS Longitudinal Offset
        string gpsLongitudinalOffset = aircraft.OperationalMode?.GPSLongitudinalOffset?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("GPS Longitudinal Offset", gpsLongitudinalOffset));

        // --- Data Link sub-section ---
        allRows.Add(new DetailRow("[dim]--- Data Link --------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

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

        // Last Update = Max(TrackedCapabilities, TrackedOperationalMode)
        string capabilitiesLastUpdate = FormatMaxLastUpdate(
            aircraft.Capabilities?.LastUpdate,
            aircraft.OperationalMode?.LastUpdate);
        allRows.Add(new DetailRow("Last Update", capabilitiesLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));

        // =====================================================================
        // Section 10: DATA QUALITY
        // =====================================================================
        allRows.Add(new DetailRow("[bold]=== DATA QUALITY =======================[/]",
            "[bold]=====================================================[/]", IsSectionHeader: true));

        // --- Antenna sub-section ---
        allRows.Add(new DetailRow("[dim]--- Antenna ----------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Antenna (TC 9-18)
        string antennaTc918 = aircraft.Position.Antenna.HasValue
            ? (aircraft.Position.Antenna.Value == AntennaFlag.SingleAntenna ? "Single" : "Diversity")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Antenna [grey](TC 9-18)[/]", antennaTc918));

        // Antenna (TC 31)
        string antennaTc31 = aircraft.OperationalMode?.SingleAntenna.HasValue == true
            ? (aircraft.OperationalMode.SingleAntenna.Value == AntennaFlag.SingleAntenna ? "Single" : "Diversity")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Antenna [grey](TC 31)[/]", antennaTc31));

        // --- Accuracy sub-section ---
        allRows.Add(new DetailRow("[dim]--- Accuracy ---------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // NACp (TC 9-18)
        allRows.Add(new DetailRow("NACp [grey](TC 9-18)[/]", aircraft.Position.NACp?.ToString() ?? "N/A (no data yet)"));

        // NACp (TC 29)
        string nacpTc29 = aircraft.DataQuality?.NACp_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACp [grey](TC 29)[/]", nacpTc29));

        // NACv (TC 19)
        string nacvTc19 = aircraft.Velocity.NACv?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACv [grey](TC 19)[/]", nacvTc19));

        // NACv (TC 31)
        string nacvTc31 = aircraft.Capabilities?.NACv?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACv [grey](TC 31)[/]", nacvTc31));

        // --- Integrity sub-section ---
        allRows.Add(new DetailRow("[dim]--- Integrity --------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // NICbaro (TC 9-18)
        string nicBaroTc918 = aircraft.Position.NICbaro.HasValue
            ? (aircraft.Position.NICbaro.Value ? "Cross-checked" : "Not cross-checked")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NICbaro [grey](TC 9-18)[/]", nicBaroTc918));

        // NICbaro (TC 29)
        string nicBaroTc29 = aircraft.DataQuality?.NICbaro_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NICbaro [grey](TC 29)[/]", nicBaroTc29));

        // NIC Supplement A
        string nicSupplementA = aircraft.DataQuality?.NICSupplementA.HasValue == true
            ? (aircraft.DataQuality.NICSupplementA.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NIC Supplement A", nicSupplementA));

        // NIC Supplement C
        string nicSupplementC = aircraft.Capabilities?.NICSupplementC.HasValue == true
            ? (aircraft.Capabilities.NICSupplementC.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NIC Supplement C", nicSupplementC));

        // SIL (TC 9-18)
        allRows.Add(new DetailRow("SIL [grey](TC 9-18)[/]", aircraft.Position.SIL?.ToString() ?? "N/A (no data yet)"));

        // SIL (TC 29)
        string silTc29 = aircraft.DataQuality?.SIL_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("SIL [grey](TC 29)[/]", silTc29));

        // SIL Supplement
        string silSupplement = aircraft.DataQuality?.SILSupplement?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("SIL Supplement", silSupplement));

        // --- Other sub-section ---
        allRows.Add(new DetailRow("[dim]--- Other ------------------------------[/]",
            "[dim]-----------------------------------------------------[/]", IsSectionHeader: true));

        // Geometric Vertical Accuracy
        string geometricVerticalAccuracy = aircraft.DataQuality?.GeometricVerticalAccuracy?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Geometric Vertical Accuracy", geometricVerticalAccuracy));

        // System Design Assurance
        string systemDesignAssurance = aircraft.OperationalMode?.SystemDesignAssurance?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("System Design Assurance", systemDesignAssurance));

        // Last Update = Max(TrackedPosition, TrackedDataQuality, TrackedCapabilities, TrackedOperationalMode)
        string dataQualityLastUpdate = FormatMaxLastUpdate(
            aircraft.Position.LastUpdate,
            aircraft.DataQuality?.LastUpdate,
            aircraft.Capabilities?.LastUpdate,
            aircraft.OperationalMode?.LastUpdate);
        allRows.Add(new DetailRow("Last Update", dataQualityLastUpdate));

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
            .Title(isExpired
                ? $"AIRCRAFT DETAIL ({aircraft.Identification.ICAO}) [red][[EXPIRED]][/] - Aeromux"
                : $"AIRCRAFT DETAIL ({aircraft.Identification.ICAO}) - Aeromux", new Style(decoration:Decoration.Bold))
            .AddColumn(new TableColumn("[bold]Field[/]").Width(40).NoWrap().PadLeft(1).PadRight(1))
            .AddColumn(new TableColumn("[bold]Value[/]").Width(53).NoWrap().PadLeft(1).PadRight(1))
            .AddColumn(new TableColumn("[bold] [/]").Width(1).Centered().NoWrap());

        // Pre-compute matching row indices for search highlighting
        var matchingIndices = new HashSet<int>();
        if (isDetailSearchActive && !string.IsNullOrEmpty(detailSearchInput))
        {
            for (int i = 0; i < allRows.Count; i++)
            {
                if (allRows[i].IsSectionHeader)
                {
                    continue;
                }

                string plainField = Regex.Replace(allRows[i].Field, @"\[/?[^\]]*\]", "");
                if (plainField.Contains(detailSearchInput, StringComparison.OrdinalIgnoreCase))
                {
                    matchingIndices.Add(i);
                }
            }
        }

        // Render rows in viewport
        for (int i = viewportStart; i < viewportEnd; i++)
        {
            DetailRow row = allRows[i];
            bool isSelected = i == selectedRow;
            bool isMatch = matchingIndices.Contains(i);

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

            // Apply highlighting to selected and/or matching rows (not section headers)
            if (isSelected && !row.IsSectionHeader)
            {
                string strippedField = Regex.Replace(row.Field, @"\[/?[^\]]*\]", "");
                string fieldDisplay = isMatch
                    ? HighlightFieldName(strippedField, detailSearchInput, isSelected: true)
                    : $"[black on white]{Markup.Escape(strippedField),-40}[/]";
                table.AddRow(
                    fieldDisplay,
                    $"[black on white]{Markup.Escape(row.Value),-53}[/]",
                    scrollbarChar);
            }
            else if (isMatch && !row.IsSectionHeader)
            {
                string strippedField = Regex.Replace(row.Field, @"\[/?[^\]]*\]", "");
                string fieldDisplay = HighlightFieldName(strippedField, detailSearchInput, isSelected: false);
                table.AddRow(fieldDisplay, row.Value, scrollbarChar);
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
        string footerRow1Left;
        if (isDetailSearchActive)
        {
            if (string.IsNullOrEmpty(detailSearchInput))
            {
                int dataFieldCount = allRows.Count(r => !r.IsSectionHeader);
                string fieldWord = dataFieldCount == 1 ? "field" : "fields";
                footerRow1Left = $"[white][bold]Search:[/] _ ({dataFieldCount} {fieldWord})[/]";
            }
            else
            {
                int matchCount = matchingIndices.Count;
                string matchWord = matchCount == 1 ? "match" : "matches";
                footerRow1Left = $"[white][bold]Search:[/] {Markup.Escape(detailSearchInput)}_ ({matchCount} {matchWord})[/]";
            }
        }
        else
        {
            footerRow1Left = $"[bold]Row:[/] {selectedRow + 1}/{totalRows}";
        }

        string footerRow1Right = $"[bold]Dist:[/] {distUnitLabel} | [bold]Alt:[/] {altUnitLabel} | [bold]Spd:[/] {speedUnitLabel}";

        // Footer row 2: left and right sections
        string footerRow2Left;
        string footerRow2Right;
        if (isDetailSearchActive)
        {
            footerRow2Left = "[bold]↑/↓/Tab[/]: Match, [bold]←/→[/]: Page, [bold]Home/End[/]";
            footerRow2Right = "[bold]ESC[/]: Cancel, [bold]Enter[/]: Done";
        }
        else
        {
            footerRow2Left = "[bold]↑/↓[/]: Row, [bold]←/→[/]: Page, [bold]Home/End[/]";
            footerRow2Right = "[bold]D/A/S[/]: Units, [bold]/[/]: Search, [bold]ESC[/]: Back, [bold]Q[/]: Quit";
        }

        // Pad footer rows to exactly 100 visible characters (table is 104 wide, 2 indent per side)
        string footerRow1 = PadFooterRow(footerRow1Left, footerRow1Right);
        string footerRow2 = PadFooterRow(footerRow2Left, footerRow2Right);

        string footer = footerRow1 + "\n" + footerRow2;

        table.Caption(footer, new Style(foreground: Color.Grey));

        // Return table and allRows for navigation
        return (table, allRows);
    }

    /// <summary>
    /// Formats a speed value with the configured unit, returning "N/A (no data yet)" if null.
    /// </summary>
    private static string FormatSpeed(Velocity? velocity, SpeedUnit speedUnit, string suffix = "")
    {
        if (velocity == null)
        {
            return "N/A (no data yet)";
        }

        double displaySpeed = speedUnit switch
        {
            SpeedUnit.Knots => velocity.Knots,
            SpeedUnit.KilometersPerHour => velocity.Knots * 1.852,
            SpeedUnit.MilesPerHour => velocity.Knots * 1.15078,
            _ => velocity.Knots
        };

        string unitLabel = speedUnit switch
        {
            SpeedUnit.Knots => "kts",
            SpeedUnit.KilometersPerHour => "km/h",
            SpeedUnit.MilesPerHour => "mph",
            _ => "kts"
        };

        return $"{displaySpeed:F0} {unitLabel}{suffix}";
    }

    /// <summary>
    /// Returns the maximum non-null DateTime formatted as "HH:mm:ss", or "N/A (no data yet)" if all are null.
    /// Used for merged sections that combine data from multiple tracked classes.
    /// </summary>
    private static string FormatMaxLastUpdate(params DateTime?[] timestamps)
    {
        DateTime? max = null;
        foreach (DateTime? ts in timestamps)
        {
            if (ts.HasValue && (!max.HasValue || ts.Value > max.Value))
            {
                max = ts;
            }
        }

        return max.HasValue ? max.Value.ToString("HH:mm:ss") : "N/A (no data yet)";
    }

    /// <summary>
    /// Renders a field name with the search match substring highlighted in red.
    /// For selected (inverted) rows, uses [red on white] for the match and [black on white] for the rest.
    /// For normal rows, uses [red] for the match and leaves the rest unstyled.
    /// </summary>
    private static string HighlightFieldName(string plainField, string searchInput, bool isSelected)
    {
        int matchStart = plainField.IndexOf(searchInput, StringComparison.OrdinalIgnoreCase);
        if (matchStart < 0)
        {
            return isSelected
                ? $"[black on white]{Markup.Escape(plainField),-40}[/]"
                : Markup.Escape(plainField);
        }

        int matchEnd = matchStart + searchInput.Length;
        string before = Markup.Escape(plainField[..matchStart]);
        string match = Markup.Escape(plainField[matchStart..matchEnd]);
        string after = Markup.Escape(plainField[matchEnd..]);

        if (isSelected)
        {
            int paddingNeeded = Math.Max(0, 40 - plainField.Length);
            string paddedAfter = after + new string(' ', paddingNeeded);
            return $"[black on white]{before}[/][red on white]{match}[/][black on white]{paddedAfter}[/]";
        }

        return $"{before}[red]{match}[/]{after}";
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
