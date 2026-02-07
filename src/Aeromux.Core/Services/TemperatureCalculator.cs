// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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

namespace Aeromux.Core.Services;

/// <summary>
/// Calculates outside air temperature (OAT) and total air temperature (TAT) from aircraft velocity data.
/// </summary>
/// <remarks>
/// <para>
/// Implements ISA (International Standard Atmosphere) temperature calculations using the relationship
/// between true airspeed, Mach number, and static air temperature. TAT includes kinetic heating effects
/// from ram air compression (recovery factor for air temperature probes).
/// </para>
/// <para>
/// Original formulas derived from aerodynamic relationships documented in ICAO Doc 7488 and
/// NACA (National Advisory Committee for Aeronautics) Report 1135 (temperature recovery in air at high speeds).
/// </para>
/// <para>
/// Implementation inspired by readsb (https://github.com/wiedehopf/readsb)
/// by Mitre Corporation and wiedehopf. Thank you for the reference implementation.
/// </para>
/// </remarks>
public static class TemperatureCalculator
{
    /// <summary>
    /// Minimum Mach number required for reliable temperature calculation.
    /// Below this threshold, the calculation becomes unreliable.
    /// </summary>
    private const double MinMachForCalculation = 0.395;

    /// <summary>
    /// Speed of sound at ISA (International Standard Atmosphere) sea level in knots.
    /// ISA defines standard atmospheric conditions: 15°C (288.15K), 1013.25 hPa at sea level.
    /// </summary>
    private const double SpeedOfSoundSeaLevel = 661.47;

    /// <summary>
    /// Standard temperature at ISA sea level in Kelvin (15°C).
    /// </summary>
    private const double StandardTemperatureSeaLevel = 288.15;

    /// <summary>
    /// Absolute zero in Celsius.
    /// </summary>
    private const double AbsoluteZeroCelsius = -273.15;

    /// <summary>
    /// Minimum valid temperature in Celsius.
    /// </summary>
    private const double MinTemperature = -80.0;

    /// <summary>
    /// Maximum valid temperature in Celsius.
    /// </summary>
    private const double MaxTemperature = 100.0;

    /// <summary>
    /// Calculates outside air temperature (OAT) from true airspeed and Mach number.
    /// OAT is also known as static air temperature (SAT).
    /// </summary>
    /// <param name="tas">True airspeed in knots</param>
    /// <param name="mach">Mach number</param>
    /// <returns>OAT in degrees Celsius, or null if calculation invalid</returns>
    public static double? CalculateOAT(double tas, double mach)
    {
        if (mach < MinMachForCalculation)
        {
            return null;
        }

        // Calculate OAT using the ISA relationship:
        // TAS = Mach × SpeedOfSound, where SpeedOfSound = sqrt(gamma × R × T)
        // Rearranging to solve for temperature:
        // temperatureRatio = TAS / (SpeedOfSoundSeaLevel × Mach)
        // OAT = (temperatureRatio² × StandardTemperatureSeaLevel) + AbsoluteZeroCelsius
        double temperatureRatio = tas / (SpeedOfSoundSeaLevel * mach);
        double oat = (temperatureRatio * temperatureRatio * StandardTemperatureSeaLevel) + AbsoluteZeroCelsius;

        if (oat is < MinTemperature or > MaxTemperature)
        {
            return null;
        }

        return oat;
    }

    /// <summary>
    /// Calculates total air temperature (TAT) from outside air temperature (OAT) and Mach number.
    /// TAT is the temperature measured by aircraft probes, including kinetic heating (ram effect).
    /// </summary>
    /// <param name="oat">Outside air temperature in degrees Celsius</param>
    /// <param name="mach">Mach number</param>
    /// <returns>TAT in degrees Celsius, or null if calculation invalid</returns>
    public static double? CalculateTAT(double oat, double mach)
    {
        if (mach < MinMachForCalculation)
        {
            return null;
        }

        // Calculate TAT using ram air temperature rise formula:
        // TAT = OAT_kelvin × (1 + recoveryFactor × Mach²)
        // where recoveryFactor = (γ - 1) / 2 = 0.2 for γ = 1.4 (air)
        // γ (gamma) is the heat capacity ratio (specific heat at constant pressure / constant volume)
        // This accounts for kinetic heating from air compression at the temperature probe
        const double recoveryFactor = 0.2;
        double oatKelvin = oat - AbsoluteZeroCelsius;
        double tatKelvin = oatKelvin * (1.0 + (recoveryFactor * mach * mach));
        double tat = tatKelvin + AbsoluteZeroCelsius;

        if (tat is < MinTemperature or > MaxTemperature)
        {
            return null;
        }

        return tat;
    }
}
