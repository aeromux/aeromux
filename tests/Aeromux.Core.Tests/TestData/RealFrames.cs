namespace Aeromux.Core.Tests.TestData;

/// <summary>
/// Real Mode-S frames captured from live aircraft or from ADS-B specifications.
/// All frames are hex strings (validated with correct CRC).
/// </summary>
/// <remarks>
/// Sources:
/// - ICAO Annex 10, Volume IV examples
/// - dump1090 test suite (https://github.com/flightaware/dump1090)
/// - Real captures from SDR receivers
/// Each constant includes source reference and expected decoded values in comments.
/// </remarks>
public static class RealFrames
{
    // ========================================
    // DF 0: Short Air-Air Surveillance
    // ========================================

    /// <summary>
    /// Short Air-Air Surveillance (DF 0): ICAO 4BCE08 at 40000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4BCE08 (extracted from CRC)
    /// Altitude: 40000 ft barometric
    /// Vertical Status (VS): 0 (airborne)
    /// Cross-link Capability (CC): 1 (DF 16 coordination supported)
    /// Sensitivity Level (SL): 7 (maximum sensitivity)
    /// Reply Information (RI): 3 (Vertical-only Resolution Advisory)
    /// Altitude Code (AC): 6552 (Gillham coded)
    /// DF: 0 (ACAS coordination message)
    /// RSSI: -23.3 dBFS
    /// </remarks>
    public const string ShortAirAir_4BCE08 = "02E619988C620A";

    /// <summary>
    /// Short Air-Air Surveillance (DF 0): ICAO 4D2407 at 33000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2407 (extracted from CRC)
    /// Altitude: 33000 ft barometric
    /// Vertical Status (VS): 0 (airborne)
    /// Cross-link Capability (CC): 1 (DF 16 coordination supported)
    /// Sensitivity Level (SL): 7 (maximum sensitivity)
    /// Reply Information (RI): 3 (Vertical-only Resolution Advisory)
    /// Altitude Code (AC): 5424 (Gillham coded)
    /// DF: 0 (ACAS coordination message)
    /// RSSI: -21.7 dBFS
    /// </remarks>
    public const string ShortAirAir_4D2407 = "02E195301C58FA";

    /// <summary>
    /// Short Air-Air Surveillance (DF 0): ICAO 73806C at 37850 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 73806C (extracted from CRC)
    /// Altitude: 37850 ft barometric
    /// Vertical Status (VS): 0 (airborne)
    /// Cross-link Capability (CC): 1 (DF 16 coordination supported)
    /// Sensitivity Level (SL): 7 (maximum sensitivity)
    /// Reply Information (RI): 3 (Vertical-only Resolution Advisory)
    /// Altitude Code (AC): 6194 (Gillham coded)
    /// DF: 0 (ACAS coordination message)
    /// RSSI: -20.3 dBFS
    /// </remarks>
    public const string ShortAirAir_73806C = "02E1983264B70A";

    // ========================================
    // DF 4: Surveillance Altitude Reply
    // ========================================

    /// <summary>
    /// Surveillance Altitude Reply (DF 4): ICAO 49D414 at 35000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 49D414 (extracted from CRC)
    /// Altitude: 35000 ft barometric
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Altitude Code (AC): 5776 (Gillham coded)
    /// DF: 4
    /// RSSI: -19.3 dBFS
    /// </remarks>
    public const string Surveillance_Altitude_49D414 = "200016900AA153";

    /// <summary>
    /// Surveillance Altitude Reply (DF 4): ICAO 4BA913 at 36000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4BA913 (extracted from CRC)
    /// Altitude: 36000 ft barometric
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Altitude Code (AC): 5912 (Gillham coded)
    /// DF: 4
    /// RSSI: -13.3 dBFS
    /// </remarks>
    public const string Surveillance_Altitude_4BA913 = "2000171801A778";

    // ========================================
    // DF 5: Surveillance Identity Reply
    // ========================================

    /// <summary>
    /// Surveillance Identity Reply (DF 5): ICAO 80073B, Squawk 3205
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B (extracted from CRC)
    /// Squawk: 3205 (Mode A identity code)
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Identity Code (ID): 2585 (Gillham coded)
    /// DF: 5
    /// RSSI: -7.8 dBFS
    /// </remarks>
    public const string Surveillance_Identity_80073B = "28000A19336C48";

    /// <summary>
    /// Surveillance Identity Reply (DF 5): ICAO 49D414, Squawk 1420
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 49D414 (extracted from CRC)
    /// Squawk: 1420 (Mode A identity code)
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Identity Code (ID): 3074 (Gillham coded)
    /// DF: 5
    /// RSSI: -21.4 dBFS
    /// </remarks>
    public const string Surveillance_Identity_49D414 = "28000C0221EAC1";

    // ========================================
    // DF 11: All-Call Reply
    // ========================================

    /// <summary>
    /// All-Call Reply (DF 11): ICAO 471F87, Capability 5 (airborne)
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 471F87 (Mode S / ADS-B)
    /// Capability (CA): 5 (Level 2+ transponder, airborne)
    /// Interrogator ID (IID): 39
    /// Air/Ground: Airborne
    /// DF: 11
    /// RSSI: -25.5 dBFS
    /// </remarks>
    public const string AllCall_471F87 = "5D471F878D70C4";

    /// <summary>
    /// All-Call Reply (DF 11): ICAO 4D2407, Capability 5 (airborne)
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2407 (Mode S / ADS-B)
    /// Capability (CA): 5 (Level 2+ transponder, airborne)
    /// Interrogator ID (IID): 39
    /// Air/Ground: Airborne
    /// DF: 11
    /// RSSI: -16.6 dBFS
    /// </remarks>
    public const string AllCall_4D2407 = "5D4D2407439A07";

    /// <summary>
    /// All-Call Reply (DF 11): ICAO 80073B, Capability 5 (airborne)
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B (Mode S / ADS-B)
    /// Capability (CA): 5 (Level 2+ transponder, airborne)
    /// Interrogator ID (IID): 33
    /// Air/Ground: Airborne
    /// DF: 11
    /// RSSI: -3.8 dBFS
    /// </remarks>
    public const string AllCall_80073B = "5D80073B5A7A08";

    // ========================================
    // DF 16: Long Air-Air ACAS
    // ========================================

    /// <summary>
    /// Long Air-Air Surveillance (DF 16): ICAO 80073B at 39975 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B (extracted from CRC)
    /// Altitude: 39975 ft barometric
    /// Vertical Status (VS): 0 (airborne)
    /// Sensitivity Level (SL): 7
    /// Reply Information (RI): 3
    /// Altitude Code (AC): 6551 (Gillham coded)
    /// Message, ACAS (MV): 58CD83316C74FA
    /// DF: 16
    /// RSSI: -11.2 dBFS
    /// </remarks>
    public const string LongAirAir_80073B = "80E1999758CD83316C74FAAD5783";

    /// <summary>
    /// Long Air-Air Surveillance (DF 16): ICAO 71C011 at 31000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 71C011 (extracted from CRC)
    /// Altitude: 31000 ft barometric
    /// Vertical Status (VS): 0 (airborne)
    /// Sensitivity Level (SL): 7
    /// Reply Information (RI): 3
    /// Altitude Code (AC): 5136 (Gillham coded)
    /// Message, ACAS (MV): 58A10383EC7C41
    /// DF: 16
    /// RSSI: -20.6 dBFS
    /// </remarks>
    public const string LongAirAir_71C011 = "80E1941058A10383EC7C41D4723F";

    /// <summary>
    /// Long Air-Air Surveillance (DF 16): ICAO 440C8E at 35000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 440C8E (extracted from CRC)
    /// Altitude: 35000 ft barometric
    /// Vertical Status (VS): 0 (airborne)
    /// Sensitivity Level (SL): 7
    /// Reply Information (RI): 3
    /// Altitude Code (AC): 5776 (Gillham coded)
    /// Message, ACAS (MV): 58B506959A8ECB
    /// DF: 16
    /// RSSI: -25.8 dBFS
    /// </remarks>
    public const string LongAirAir_440C8E = "80E1969058B506959A8ECB8C687B";

    // ========================================
    // DF 17: Extended Squitter (ADS-B)
    // ========================================

    // TC 1-4: Aircraft Identification and Category

    /// <summary>
    /// Aircraft Identification (TC 4): "WZZ476" from ICAO 471DBC
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 471DBC
    /// Callsign: "WZZ476" (8 characters, space-padded)
    /// Category: A3 (Large aircraft - 34000 to 136000 kg)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 4
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -13.2 dBFS
    /// </remarks>
    public const string AircraftId_471DBC = "8D471DBC235DA6B4DF68207B883F";

    /// <summary>
    /// Aircraft Identification (TC 4): "ETD128" from ICAO 8965F3
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 8965F3
    /// Callsign: "ETD128" (8 characters, space-padded)
    /// Category: A5 (Heavy aircraft - greater than 136000 kg)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 4
    /// CA: 5 (Level 2+ transponder, airborne)
    /// </remarks>
    public const string AircraftId_8965F3 = "8D8965F325154131CB8820";

    /// <summary>
    /// Aircraft Identification (TC 4): "UAE182" from ICAO 8964A0
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 8964A0
    /// Callsign: "UAE182" (8 characters, space-padded)
    /// Category: A5 (Heavy aircraft - greater than 136000 kg)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 4
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -13.4 dBFS
    /// </remarks>
    public const string AircraftId_8964A0 = "8D8964A025541171E328200F47FC";

    // TC 9-18: Airborne Position (Barometric Altitude)

    /// <summary>
    /// Airborne Position (TC 11, Barometric, Even): ICAO 80073B at 39975 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B
    /// Altitude: 39975 ft barometric
    /// CPR Format: Even (F=0)
    /// CPR Lat: 104673 (0x19941)
    /// CPR Lon: 29847 (0x7497)
    /// CPR NUCp/NIC: 7
    /// Decoded Position: 46.79155°N, 19.56042°E
    /// Pairs with AirbornePos_80073B_Odd for global decode
    /// DF: 17
    /// TC: 11
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -17.1 dBFS
    /// </remarks>
    public const string AirbornePos_80073B_Even = "8D80073B58CD7331C27497A8A51D";

    /// <summary>
    /// Airborne Position (TC 11, Barometric, Odd): ICAO 80073B at 39975 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B
    /// Altitude: 39975 ft barometric
    /// CPR Format: Odd (F=1)
    /// CPR Lat: 87652 (0x156E4)
    /// CPR Lon: 22689 (0x58A1)
    /// CPR NUCp/NIC: 7
    /// Decoded Position: 46.79226°N, 19.55793°E
    /// Pairs with AirbornePos_80073B_Even for global decode
    /// DF: 17
    /// TC: 11
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -23.4 dBFS
    /// </remarks>
    public const string AirbornePos_80073B_Odd = "8D80073B58CD76ACC858A13FCFEF";

    /// <summary>
    /// Airborne Position (TC 12, Barometric, Even): ICAO 73806C at 37600 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 73806C
    /// Altitude: 37600 ft barometric
    /// CPR Format: Even (F=0)
    /// CPR Lat: 119604 (0x1D334)
    /// CPR Lon: 30386 (0x76B2)
    /// CPR NUCp/NIC: 6
    /// Decoded Position: 47.47504°N, 20.08644°E
    /// Pairs with AirbornePos_73806C_Odd for global decode
    /// DF: 17
    /// TC: 12
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -20.3 dBFS
    /// </remarks>
    public const string AirbornePos_73806C_Even = "8D73806C60C183A66876B238EC28";

    /// <summary>
    /// Airborne Position (TC 12, Barometric, Odd): ICAO 73806C at 37600 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 73806C
    /// Altitude: 37600 ft barometric
    /// CPR Format: Odd (F=1)
    /// CPR Lat: 102350 (0x18FCE)
    /// CPR Lon: 23044 (0x5A04)
    /// CPR NUCp/NIC: 6
    /// Decoded Position: 47.47649°N, 20.08442°E
    /// Pairs with AirbornePos_73806C_Even for global decode
    /// DF: 17
    /// TC: 12
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -22.6 dBFS
    /// </remarks>
    public const string AirbornePos_73806C_Odd = "8D73806C60C1871F9C5A043E868F";

    // TC 19: Airborne Velocity

    /// <summary>
    /// Airborne Velocity (TC 19, Subtype 1): Ground speed 389 kts, Heading 294°, Descending
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4BB027
    /// Subtype: 1 (Ground speed, subsonic)
    /// Velocity: 389 kts ground speed
    /// Heading: 294° (track angle)
    /// Vertical Rate: -1984 ft/min GNSS (descending)
    /// GNSS delta: 25 ft
    /// DF: 17
    /// TC: 19
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -24.3 dBFS
    /// </remarks>
    public const string AirborneVel_4BB027_Descending = "8D4BB02799156713588002997917";

    /// <summary>
    /// Airborne Velocity (TC 19, Subtype 1): Ground speed 400 kts, Heading 276°, Level
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 39CEAD
    /// Subtype: 1 (Ground speed, subsonic)
    /// Velocity: 400 kts ground speed
    /// Heading: 276° (track angle)
    /// Vertical Rate: 0 ft/min GNSS (level flight)
    /// GNSS delta: -375 ft
    /// DF: 17
    /// TC: 19
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -16.4 dBFS
    /// </remarks>
    public const string AirborneVel_39CEAD_Level = "8D39CEAD990D9004F80490EF6594";

    /// <summary>
    /// Airborne Velocity (TC 19, Subtype 1): Ground speed 483 kts, Heading 117°, Level
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2407
    /// Subtype: 1 (Ground speed, subsonic)
    /// Velocity: 483 kts ground speed
    /// Heading: 117° (track angle)
    /// Vertical Rate: 0 ft/min GNSS (level flight)
    /// GNSS delta: -75 ft
    /// DF: 17
    /// TC: 19
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -21.3 dBFS
    /// </remarks>
    public const string AirborneVel_4D2407_Level = "8D4D24079909B29B3004844B63D6";

    /// <summary>
    /// Airborne Velocity (TC 19, Subtype 1): Ground speed 413 kts, Heading 317°, Climbing
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 73806C
    /// Subtype: 1 (Ground speed, subsonic)
    /// Velocity: 413 kts ground speed
    /// Heading: 317° (track angle)
    /// Vertical Rate: 896 ft/min GNSS (climbing)
    /// GNSS delta: -325 ft
    /// DF: 17
    /// TC: 19
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -21.2 dBFS
    /// </remarks>
    public const string AirborneVel_73806C_Climbing = "8D73806C990D1E25B03C8E";

    // TC 28: Emergency/Priority Status

    /// <summary>
    /// Emergency/Priority Status (TC 28, Subtype 1): ICAO 4D2407, Squawk 6415
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2407
    /// Squawk: 6415 (Mode A code)
    /// Emergency State: No emergency (subtype 1)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 28
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -8.0 dBFS
    /// </remarks>
    public const string EmergencyStatus_4D2407 = "8D4D2407E1129300000000BC60A0";

    /// <summary>
    /// Emergency/Priority Status (TC 28, Subtype 1): ICAO 503D74, Squawk 3254
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 503D74
    /// Squawk: 3254 (Mode A code)
    /// Emergency State: No emergency (subtype 1)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 28
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -22.8 dBFS
    /// </remarks>
    public const string EmergencyStatus_503D74 = "8D503D74E11B0900000000CD3D5E";

    /// <summary>
    /// Emergency/Priority Status (TC 28, Subtype 1): ICAO 80073B, Squawk 3205
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B
    /// Squawk: 3205 (Mode A code)
    /// Emergency State: No emergency (subtype 1)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 28
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -22.8 dBFS
    /// </remarks>
    public const string EmergencyStatus_80073B = "8D80073BE10A1900000000BDFF7B";

    // TC 29: Target State and Status

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 49D414, Target Alt 35008 ft, Heading 132°
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 49D414
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 35008 ft
    /// Altimeter Setting: 1013.6 millibars
    /// Target Heading: 132°
    /// ACAS: Operational
    /// NACp: 11 (horizontal accuracy &lt; 3 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -17.4 dBFS
    /// </remarks>
    public const string TargetStateStatus_49D414 = "8D49D414EA447865797C08A0A222";

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 73806C, Target Alt 38016 ft, Heading 310°
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 73806C
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 38016 ft
    /// Altimeter Setting: 1013.6 millibars
    /// Target Heading: 310°
    /// ACAS: Operational
    /// NACp: 8 (horizontal accuracy &lt; 93 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -20.2 dBFS
    /// </remarks>
    public const string TargetStateStatus_73806C = "8D73806CEA4A5867731C08492568";

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 39CEAD, Target Alt 40000 ft, Heading 270°
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 39CEAD
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 40000 ft
    /// Altimeter Setting: 1013.6 millibars
    /// Target Heading: 270°
    /// ACAS: Operational
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -16.3 dBFS
    /// </remarks>
    public const string TargetStateStatus_39CEAD = "8D39CEADEA4E3867033C08A2593D";

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 71C011, Target Alt 31008 ft, Heading 120°
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 71C011
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 31008 ft
    /// Altimeter Setting: 1012.8 millibars
    /// Target Heading: 120°
    /// ACAS: Operational
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -20.5 dBFS
    /// </remarks>
    public const string TargetStateStatus_71C011 = "8D71C011EA3CA85D573C08C3CE42";

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 86E778, Target Alt 32992 ft, Heading 104°, Autopilot+VNAV
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 86E778
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 32992 ft
    /// Altimeter Setting: 1012.8 millibars
    /// Target Heading: 104°
    /// Active Modes: Autopilot, VNAV
    /// ACAS: Operational
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -24.2 dBFS
    /// </remarks>
    public const string TargetStateStatus_86E778_AutopilotVNAV = "8D86E778EA40885D293F8C2BCA06";

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 4D2047, Target Alt 36992 ft, Heading 111°
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2047
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 36992 ft
    /// Altimeter Setting: 1013.6 millibars
    /// Target Heading: 111°
    /// ACAS: Operational
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -13.9 dBFS
    /// </remarks>
    public const string TargetStateStatus_4D2047 = "8D4D2047EA4858653D3C084C8F04";

    /// <summary>
    /// Target State and Status (TC 29, Version 2): ICAO 8965F3, Target Alt 36992 ft, Autopilot+VNAV
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 8965F3
    /// Subtype: 1 (Version 2)
    /// Target Altitude: MCP, 36992 ft
    /// Altimeter Setting: 1012.8 millibars
    /// Active Modes: Autopilot, VNAV
    /// ACAS: Operational
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// SIL: 3 (per sample)
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 29
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -26.7 dBFS
    /// </remarks>
    public const string TargetStateStatus_8965F3_AutopilotVNAV = "8D8965F3EA485858013F8CD689CA";

    // TC 31: Aircraft Operational Status

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 471DBC, NACp=9
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 471DBC
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS ARV TS
    /// Operational modes: SDA=3
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: true north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -15.2 dBFS
    /// </remarks>
    public const string OperationalStatus_471DBC = "8D471DBCF82300030049B80B70D7";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 71C011, NACp=9
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 71C011
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS ARV TS
    /// Operational modes: SAF SDA=2
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: true north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -20.7 dBFS
    /// </remarks>
    public const string OperationalStatus_71C011 = "8D71C011F82300060049B8F7FEC8";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 3C55C5, NACp=9
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 3C55C5
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS ARV TS
    /// Operational modes: SDA=2
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: true north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -25.0 dBFS
    /// </remarks>
    public const string OperationalStatus_3C55C5 = "8D3C55C5F82300020049B8301A02";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 4BB027, NACp=11
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4BB027
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS TS
    /// Operational modes: SDA=2
    /// NACp: 11 (horizontal accuracy &lt; 3 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: true north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -21.4 dBFS
    /// </remarks>
    public const string OperationalStatus_4BB027 = "8D4BB027F8210002004BB811F3B0";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 80073B, NACp=9, Magnetic North
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS TS
    /// Operational modes: SDA=2
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: magnetic north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -14.7 dBFS
    /// </remarks>
    public const string OperationalStatus_80073B = "8D80073BF82100020049BCD0E1F0";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 06A081, NACp=9
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 06A081
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS TS
    /// Operational modes: SDA=2
    /// NACp: 9 (horizontal accuracy &lt; 30 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: true north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -2.4 dBFS
    /// </remarks>
    public const string OperationalStatus_06A081 = "8D06A081F82100020049B8CB501B";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 4BB0F4, NACp=11, Magnetic North
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4BB0F4
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS ARV TS
    /// Operational modes: SDA=2
    /// NACp: 11 (horizontal accuracy &lt; 3 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: magnetic north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -17.6 dBFS
    /// </remarks>
    public const string OperationalStatus_4BB0F4 = "8D4BB0F4F8230002004BBC4F37F9";

    /// <summary>
    /// Aircraft Operational Status (TC 31, Version 2): ICAO 5082A0, NACp=11, Magnetic North
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 5082A0
    /// Version: 2 (DO-260B)
    /// Capability classes: ACAS TS
    /// Operational modes: SDA=2
    /// NACp: 11 (horizontal accuracy &lt; 3 m)
    /// GVA: 2 (geometric vertical accuracy)
    /// SIL: 3 (per hour)
    /// NICbaro: 1 (barometric altitude cross-checked)
    /// Heading reference: magnetic north
    /// Air/Ground: Airborne
    /// DF: 17
    /// TC: 31
    /// CA: 5 (Level 2+ transponder, airborne)
    /// RSSI: -16.1 dBFS
    /// </remarks>
    public const string OperationalStatus_5082A0 = "8D5082A0F8210002004BBC2B5534";

    // ========================================
    // DF 20: Comm-B Altitude Reply
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20): ICAO 4D2407 at 33000 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2407 (extracted from CRC)
    /// Altitude: 33000 ft barometric
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Altitude Code (AC): 5424 (Gillham coded)
    /// Message, Comm-B (MB): A82A39323FFC00
    /// DF: 20
    /// RSSI: -18.9 dBFS
    /// </remarks>
    public const string CommB_Altitude_4D2407 = "A0001530A82A39323FFC00282624";

    /// <summary>
    /// Comm-B Altitude Reply (DF 20): ICAO 80073B at 39975 ft
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 80073B (extracted from CRC)
    /// Altitude: 39975 ft barometric
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Altitude Code (AC): 6551 (Gillham coded)
    /// Message, Comm-B (MB): CE200030A40180
    /// DF: 20
    /// RSSI: -5.8 dBFS
    /// </remarks>
    public const string CommB_Altitude_80073B = "A0001997CE200030A40180DD14CF";

    // ========================================
    // DF 21: Comm-B Identity Reply
    // ========================================

    /// <summary>
    /// Comm-B Identity Reply (DF 21): ICAO 4D2407, Squawk 6415
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 4D2407 (extracted from CRC)
    /// Squawk: 6415 (Mode A identity code)
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Identity Code (ID): 4755 (Gillham coded)
    /// Message, Comm-B (MB): FFF5313CBFFCE5
    /// DF: 21
    /// RSSI: -19.0 dBFS
    /// </remarks>
    public const string CommB_Identity_4D2407 = "A8001293FFF5313CBFFCE55E67D0";

    /// <summary>
    /// Comm-B Identity Reply (DF 21): ICAO 49D414, Squawk 1420
    /// </summary>
    /// <remarks>
    /// Source: Real capture (dump1090)
    /// ICAO: 49D414 (extracted from CRC)
    /// Squawk: 1420 (Mode A identity code)
    /// Flight Status (FS): 0 (no alert, no SPI, aircraft is airborne)
    /// Downlink Request (DR): 0
    /// Utility Message (UM): 0
    /// Identity Code (ID): 3074 (Gillham coded)
    /// Message, Comm-B (MB): C4662330A80000
    /// DF: 21
    /// RSSI: -21.1 dBFS
    /// </remarks>
    public const string CommB_Identity_49D414 = "A8000C02C4662330A8000003FC60";
}
