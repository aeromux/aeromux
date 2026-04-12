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

using System.Runtime.CompilerServices;

namespace Aeromux.Core.ModeS;

/// <summary>
/// High-performance enum validation using range checks and lookup tables.
/// Replaces Enum.IsDefined() to eliminate reflection and boxing allocations in the hot path.
/// Each method is aggressively inlined — the JIT compiles them to a single comparison instruction.
/// </summary>
public static class EnumValidator
{
    // --- Sparse enum lookup tables (allocated once at startup) ---

    private static readonly bool[] ValidAcasReplyInformation = CreateLookup(15,
        [0, 2, 3, 4, 8, 9, 10, 11, 12, 13, 14]);

    private static readonly bool[] ValidAircraftCategory = CreateLookup(48,
        [0, 10, 21, 23, 31, 32, 33, 40, 41, 42, 43, 44, 45, 46, 47]);

    private static readonly bool[] ValidSurfaceMovement = CreateLookup(128,
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 124, 127]);

    // --- Contiguous enums: single unsigned comparison ---

    /// <summary>Validates SurveillanceStatus (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSurveillanceStatus(int value) => (uint)value <= 3;

    /// <summary>Validates AntennaFlag (1-bit, range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidAntennaFlag(int value) => (uint)value <= 1;

    /// <summary>Validates CprFormat (1-bit, range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidCprFormat(int value) => (uint)value <= 1;

    /// <summary>Validates NavigationAccuracyCategoryVelocity (3-bit, range 0-7).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidNavigationAccuracyCategoryVelocity(int value) => (uint)value <= 7;

    /// <summary>Validates AircraftStatusSubtype (3-bit, valid range 0-2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidAircraftStatusSubtype(int value) => (uint)value <= 2;

    /// <summary>Validates EmergencyState (3-bit, range 0-7).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidEmergencyState(int value) => (uint)value <= 7;

    /// <summary>Validates OperationalStatusSubtype (3-bit, valid range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidOperationalStatusSubtype(int value) => (uint)value <= 1;

    /// <summary>Validates AdsbVersion (3-bit, range 0-7).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidAdsbVersion(int value) => (uint)value <= 7;

    /// <summary>Validates TrajectoryChangeReportCapability (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTrajectoryChangeReportCapability(int value) => (uint)value <= 3;

    /// <summary>Validates NavigationAccuracyCategoryPosition (4-bit, range 0-15).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidNavigationAccuracyCategoryPosition(int value) => (uint)value <= 15;

    /// <summary>Validates SourceIntegrityLevel (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSourceIntegrityLevel(int value) => (uint)value <= 3;

    /// <summary>Validates HorizontalReferenceDirection (1-bit, range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidHorizontalReferenceDirection(int value) => (uint)value <= 1;

    /// <summary>Validates SdaSupportedFailureCondition (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSdaSupportedFailureCondition(int value) => (uint)value <= 3;

    /// <summary>Validates GeometricVerticalAccuracy (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidGeometricVerticalAccuracy(int value) => (uint)value <= 3;

    /// <summary>Validates BarometricAltitudeIntegrityCode (1-bit, range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidBarometricAltitudeIntegrityCode(int value) => (uint)value <= 1;

    /// <summary>Validates TargetHeadingType (1-bit, range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTargetHeadingType(int value) => (uint)value <= 1;

    /// <summary>Validates SilSupplement (1-bit, range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSilSupplement(int value) => (uint)value <= 1;

    /// <summary>Validates TargetStateSubtype (2-bit, valid range 0-1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTargetStateSubtype(int value) => (uint)value <= 1;

    /// <summary>Validates VerticalMode (2-bit, valid range 0-2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidVerticalMode(int value) => (uint)value <= 2;

    /// <summary>Validates HorizontalMode (2-bit, valid range 0-2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidHorizontalMode(int value) => (uint)value <= 2;

    /// <summary>Validates FlightStatus (3-bit, range 0-7).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidFlightStatus(int value) => (uint)value <= 7;

    /// <summary>Validates TransponderCapability (3-bit, range 0-7).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTransponderCapability(int value) => (uint)value <= 7;

    /// <summary>Validates Severity (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSeverity(int value) => (uint)value <= 3;

    /// <summary>Validates Bds40AltitudeSource (2-bit, range 0-3).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidBds40AltitudeSource(int value) => (uint)value <= 3;

    /// <summary>Validates ConfidenceLevel (sparse: 5, 10, 15). Startup-only.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidConfidenceLevel(int value) => value is 5 or 10 or 15;

    // --- Sparse enums: pre-computed lookup tables ---

    /// <summary>Validates AcasReplyInformation (4-bit, sparse: 0, 2-4, 8-14).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidAcasReplyInformation(int value) =>
        (uint)value < (uint)ValidAcasReplyInformation.Length && ValidAcasReplyInformation[value];

    /// <summary>Validates AircraftCategory (sparse: 0, 10, 21, 23, 31-33, 40-47).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidAircraftCategory(int value) =>
        (uint)value < (uint)ValidAircraftCategory.Length && ValidAircraftCategory[value];

    /// <summary>Validates SurfaceMovement (7-bit, sparse: 0-24, 124, 127).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSurfaceMovement(int value) =>
        (uint)value < (uint)ValidSurfaceMovement.Length && ValidSurfaceMovement[value];

    // --- Helper ---

    private static bool[] CreateLookup(int size, int[] validValues)
    {
        bool[] lookup = new bool[size];
        foreach (int v in validValues)
        {
            lookup[v] = true;
        }
        return lookup;
    }
}
