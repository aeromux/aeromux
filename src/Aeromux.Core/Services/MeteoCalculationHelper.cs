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

using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Services;

/// <summary>
/// Helper class for calculating meteorological values from aircraft state.
/// Provides shared calculation logic for wind and temperature calculations
/// triggered by velocity and dynamics data updates.
/// </summary>
internal static class MeteoCalculationHelper
{
    /// <summary>
    /// Maximum data age for velocity data (TAS = True Airspeed, GS = Ground Speed) in seconds.
    /// Data older than this is considered stale and not used for wind calculations.
    /// </summary>
    private const double MaxDataAge = 2.5;

    /// <summary>
    /// Maximum data age for heading and track data in seconds.
    /// Stricter than velocity data age to ensure accurate wind vector calculations.
    /// </summary>
    private const double MaxHeadingAge = 1.25;

    /// <summary>
    /// Attempts to calculate wind speed and direction from aircraft velocity vectors.
    /// Uses the triangle of velocities: Wind Vector = Ground Speed Vector - True Airspeed Vector.
    /// </summary>
    /// <param name="aircraft">Aircraft state containing velocity and heading data</param>
    /// <param name="timestamp">Current message timestamp for data freshness checks</param>
    /// <param name="windSpeed">Calculated wind speed in knots (output)</param>
    /// <param name="windDirection">Calculated wind direction in degrees true (output)</param>
    /// <returns>True if calculation succeeded with fresh data, false otherwise</returns>
    public static bool TryCalculateWind(
        Aircraft aircraft,
        DateTime timestamp,
        out int? windSpeed,
        out double? windDirection)
    {
        windSpeed = null;
        windDirection = null;

        if (aircraft.Position.IsOnGround)
        {
            return false;
        }

        if (!TryGetWindInputs(aircraft, timestamp,
            out double trueHeading, out double track,
            out double tas, out double groundSpeed))
        {
            return false;
        }

        if (WindCalculator.Calculate(trueHeading, track, tas, groundSpeed,
            out double ws, out double wd))
        {
            windSpeed = (int)Math.Round(ws);
            windDirection = wd;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to calculate OAT (Outside Air Temperature) and TAT (Total Air Temperature) from TAS (True Airspeed) and Mach number.
    /// OAT is the static air temperature, while TAT accounts for ram air heating due to aircraft speed.
    /// </summary>
    /// <param name="aircraft">Aircraft state containing TAS and Mach number from Comm-B messages</param>
    /// <param name="timestamp">Current message timestamp for data freshness checks</param>
    /// <param name="oat">Calculated outside air temperature (static) in °C (output)</param>
    /// <param name="tat">Calculated total air temperature (with ram rise) in °C (output)</param>
    /// <returns>True if calculation succeeded with fresh data, false otherwise</returns>
    public static bool TryCalculateTemperatures(
        Aircraft aircraft,
        DateTime timestamp,
        out double? oat,
        out double? tat)
    {
        oat = null;
        tat = null;

        if (aircraft.Position.IsOnGround)
        {
            return false;
        }

        if (aircraft.Velocity.CommBTrueAirspeed == null ||
            aircraft.Velocity.LastUpdate == null ||
            (timestamp - aircraft.Velocity.LastUpdate.Value).TotalSeconds > MaxDataAge)
        {
            return false;
        }
        double tas = aircraft.Velocity.CommBTrueAirspeed.Knots;

        if (aircraft.FlightDynamics?.MachNumber == null ||
            aircraft.FlightDynamics.LastUpdate == null ||
            (timestamp - aircraft.FlightDynamics.LastUpdate.Value).TotalSeconds > MaxDataAge)
        {
            return false;
        }
        double mach = aircraft.FlightDynamics.MachNumber.Value;

        oat = TemperatureCalculator.CalculateOAT(tas, mach);
        if (oat == null)
        {
            return false;
        }

        tat = TemperatureCalculator.CalculateTAT(oat.Value, mach);
        if (tat == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gathers and validates input data for wind calculation.
    /// </summary>
    private static bool TryGetWindInputs(
        Aircraft aircraft,
        DateTime timestamp,
        out double trueHeading,
        out double track,
        out double tas,
        out double groundSpeed)
    {
        trueHeading = 0;
        track = 0;
        tas = 0;
        groundSpeed = 0;

        if (aircraft.FlightDynamics?.TrueHeading == null ||
            aircraft.FlightDynamics.LastUpdate == null ||
            (timestamp - aircraft.FlightDynamics.LastUpdate.Value).TotalSeconds > MaxHeadingAge)
        {
            return false;
        }
        trueHeading = aircraft.FlightDynamics.TrueHeading.Value;

        // Prefer TrackAngle from BDS 5,0 (Comm-B Track and Turn Report) over ADS-B Track for better accuracy
        double? trackValue = aircraft.Velocity.TrackAngle ?? aircraft.Velocity.Track;
        if (trackValue == null ||
            aircraft.Velocity.LastUpdate == null ||
            (timestamp - aircraft.Velocity.LastUpdate.Value).TotalSeconds > MaxHeadingAge)
        {
            return false;
        }
        track = trackValue.Value;

        // TAS (True Airspeed) available from BDS 5,0 (Track and Turn) or BDS 5,3 (Air-Referenced State)
        if (aircraft.Velocity.CommBTrueAirspeed == null ||
            aircraft.Velocity.LastUpdate == null ||
            (timestamp - aircraft.Velocity.LastUpdate.Value).TotalSeconds > MaxDataAge)
        {
            return false;
        }
        tas = aircraft.Velocity.CommBTrueAirspeed.Knots;

        // Prefer CommB ground speed from BDS 5,0 over ADS-B TC 19 for temporal consistency
        Velocity? gsVelocity = aircraft.Velocity.CommBGroundSpeed ?? aircraft.Velocity.Speed;
        if (gsVelocity == null ||
            aircraft.Velocity.LastUpdate == null ||
            (timestamp - aircraft.Velocity.LastUpdate.Value).TotalSeconds > MaxDataAge)
        {
            return false;
        }
        groundSpeed = gsVelocity.Knots;

        return true;
    }
}
