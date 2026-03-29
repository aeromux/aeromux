# Add Real Mode-S Frame to Test Data

Add a real Mode-S frame from dump1090 output to `RealFrames.cs` test data file.

## Usage

Paste the dump1090 decoded output for a frame, and I will:
1. Parse the hex frame and decoded information
2. Generate appropriate constant name based on message type and ICAO
3. Add it to `RealFrames.cs` with full documentation
4. Place it in the correct section (Aircraft ID, Airborne Position, Airborne Velocity, etc.)

## Expected Input Format

Paste any dump1090 output like this (just examples, further types exist but not mentioned here):

```
*8d4d24079989b29bf0208543de56;
CRC: 000000
RSSI: -13.7 dBFS
Score: 1800
Time: 297213054.75us
DF:17 AA:4D2407 CA:5 ME:9989B29BF02085
 Extended Squitter Airborne velocity over ground, subsonic (19/1)
  ICAO Address:  4D2407 (Mode S / ADS-B)
  Air/Ground:    airborne
  GNSS delta:    -100 ft
  Heading:       117
  Speed:         486 kt groundspeed
  Vertical rate: 448 ft/min GNSS
```

Or for Aircraft Identification:

```
*8d503d7423048670df382093b2d4;
CRC: 000000
RSSI: -15.2 dBFS
DF:17 AA:503D74 CA:5 ME:23048670DF3820
 Extended Squitter Aircraft identification and category (BDS 2,0)
  ICAO Address:  503D74 (Mode S / ADS-B)
  Callsign:      AHY073
  Category:      A3
```

Or for Airborne Position:

```
*8d4840d658b982d0c59e91bf7c91;
CRC: 000000
RSSI: -18.5 dBFS
DF:17 AA:4840D6 CA:5 ME:58B982D0C59E91
 Extended Squitter Airborne position (barometric altitude) (11)
  ICAO Address:  4840D6 (Mode S / ADS-B)
  Air/Ground:    airborne
  Altitude:      38000 ft barometric
  CPR type:      Airborne
  CPR format:    even
  CPR latitude:  93000 (0x16AD8)
  CPR longitude: 51372 (0xC8BC)
```

## What I Will Do

1. **Extract Information**:
   - Hex frame (remove `*` and `;`)
   - ICAO address
   - Message type (DF/TC)
   - Decoded values (callsign, altitude, speed, heading, etc.)

2. **Generate Constant Name** based on message type:
   - DF 0: `ShortAcas_{ICAO}` (e.g., `ShortAcas_4BCE08`)
   - DF 4: `SurvAlt_{ICAO}` (e.g., `SurvAlt_4840D6`)
   - DF 5: `SurvIdent_{ICAO}` (e.g., `SurvIdent_4840D6`)
   - DF 11: `AllCall_{ICAO}` (e.g., `AllCall_4840D6`)
   - DF 16: `LongAcas_{ICAO}` (e.g., `LongAcas_4BCE08`)
   - DF 17/18 TC 1-4: `AircraftId_{ICAO}` (e.g., `AircraftId_503D74`)
   - DF 17/18 TC 5-8: `SurfacePos_{ICAO}_{Even/Odd}` (e.g., `SurfacePos_4840D6_Even`)
   - DF 17/18 TC 9-18: `AirbornePos_{ICAO}_{Even/Odd}` (e.g., `AirbornePos_4840D6_Even`)
   - DF 17/18 TC 19: `AirborneVel_{ICAO}_{Descriptor}` (e.g., `AirborneVel_4D2407_Climbing`)
   - DF 17/18 TC 28: `AircraftStatus_{ICAO}` (e.g., `AircraftStatus_4840D6`)
   - DF 17/18 TC 29: `TargetState_{ICAO}` (e.g., `TargetState_4840D6`)
   - DF 17/18 TC 31: `OpStatus_{ICAO}` (e.g., `OpStatus_4840D6`)
   - DF 20: `CommBAlt_{ICAO}_{BDS}` (e.g., `CommBAlt_4840D6_BDS60`)
   - DF 21: `CommBIdent_{ICAO}_{BDS}` (e.g., `CommBIdent_4840D6_BDS20`)

3. **Add to RealFrames.cs**:
   - Place in appropriate section **ordered by Downlink Format (DF)**
   - Create new DF section if it doesn't exist
   - Add XML documentation with all decoded values
   - Include source reference (dump1090 capture)
   - Within each DF section, group by Type Code (TC) or message subtype

4. **Verify**:
   - Check for duplicates
   - Validate hex format
   - Ensure proper documentation

## Notes

- The frame will be added to `tests/Aeromux.Core.Tests/TestData/RealFrames.cs`
- All values from dump1090 output will be documented in XML comments
- Constant names follow existing naming conventions
- **Frames are organized by Downlink Format (DF) in ascending order**:
  - DF 0: Short Air-Air Surveillance
  - DF 4: Surveillance Altitude Reply
  - DF 5: Surveillance Identity Reply
  - DF 11: All-Call Reply
  - DF 16: Long Air-Air Surveillance
  - DF 17: Extended Squitter (ADS-B) - with Type Code (TC) subsections
  - DF 18: Extended Squitter (Non-Transponder)
  - DF 20: Comm-B Altitude Reply
  - DF 21: Comm-B Identity Reply
  - DF 24: Comm-D Extended Length Message (rare, placeholder for future use)
- Within DF 17 section, frames are grouped by Type Code (TC 1-4, TC 9-18, TC 19, etc.)
- If a DF section doesn't exist, a new section will be created in the correct numerical order

## Examples

### Example 1: Aircraft Identification
**Input**: `*8d503d7423048670df382093b2d4;` with callsign "AHY073"
**Output**: Adds `AircraftId_503D74` constant to Aircraft Identification section

### Example 2: Airborne Velocity (Climbing)
**Input**: `*8d4d24079989b29bf0208543de56;` with 486 kts, heading 117°, climbing
**Output**: Adds `AirborneVel_4D2407_Climbing` constant to Airborne Velocity section

### Example 3: Airborne Position
**Input**: `*8d4840d658b982d0c59e91bf7c91;` with even CPR frame
**Output**: Adds `AirbornePos_4840D6_Even` constant to Airborne Position section

## XML Documentation Examples

Below are examples of the exact C# code that will be generated for each message type. You can review and modify the format before I add it.

### Example 1: Aircraft Identification Frame

```csharp
/// <summary>
/// Aircraft Identification (TC 4): "AHY073" from ICAO 503D74
/// </summary>
/// <remarks>
/// Source: Real capture (dump1090)
/// ICAO: 503D74
/// Callsign: "AHY073" (8 characters, space-padded)
/// Category: A3 (Large aircraft - 34000 to 136000 kg)
/// DF: 17
/// TC: 4
/// RSSI: -15.2 dBFS
/// </remarks>
public const string AircraftId_503D74 = "8D503D7423048670DF382093B2D4";
```

### Example 2: Airborne Velocity Frame (Climbing)

```csharp
/// <summary>
/// Airborne Velocity (TC 19, Subtype 1): Ground speed 486 kts, Heading 117°, Climbing
/// </summary>
/// <remarks>
/// Source: Real capture (dump1090)
/// ICAO: 4D2407
/// Subtype: 1 (Ground speed, subsonic)
/// Velocity: 486 kts ground speed
/// Heading: 117° (track angle)
/// Vertical Rate: 448 ft/min GNSS (climbing)
/// GNSS delta: -100 ft
/// DF: 17
/// TC: 19
/// RSSI: -13.7 dBFS
/// </remarks>
public const string AirborneVel_4D2407_Climbing = "8D4D24079989B29BF0208543DE56";
```

### Example 3: Airborne Position Frame (Even)

```csharp
/// <summary>
/// Airborne Position (TC 11, Barometric, Even): ICAO 4840D6 at 38000 ft
/// </summary>
/// <remarks>
/// Source: Real capture (dump1090)
/// ICAO: 4840D6
/// Altitude: 38000 ft barometric
/// CPR Format: Even (F=0)
/// CPR Lat: 93000 (0x16AD8)
/// CPR Lon: 51372 (0xC8BC)
/// Pairs with AirbornePos_4840D6_Odd for global decode
/// DF: 17
/// TC: 11
/// RSSI: -18.5 dBFS
/// </remarks>
public const string AirbornePos_4840D6_Even = "8D4840D658B982D0C59E91BF7C91";
```

### Example 4: Airborne Velocity Frame (Descending)

```csharp
/// <summary>
/// Airborne Velocity (TC 19, Subtype 1): Ground speed 375 kts, Track 245.0°, Descending
/// </summary>
/// <remarks>
/// Source: Real capture (dump1090)
/// ICAO: A05F21
/// Subtype: 1 (Ground speed, subsonic)
/// Velocity: 375 kts ground speed
/// Heading: 245.0° (track angle)
/// Vertical Rate: -1920 ft/min (descending)
/// DF: 17
/// TC: 19
/// </remarks>
public const string AirborneVel_A05F21_Descending = "8DA05F21990902B075A0B0B7B398";
```

### Example 5: All-Call Reply (DF 11)

```csharp
/// <summary>
/// All-Call Reply with Capability=5 (DF=11, CA=5)
/// </summary>
/// <remarks>
/// Source: Real capture (dump1090)
/// ICAO: 4840D6 (extracted from CRC)
/// Capability: 5 (Level 2+ transponder, on-ground)
/// DF: 11
/// </remarks>
public const string AllCall_4840D6 = "5D4840D6F12AF9";
```

## XML Documentation Format Rules

1. **Summary**: Brief description with TC, message type, and key identifying info
2. **Remarks Section** includes (in order):
   - `Source: Real capture (dump1090)` - always included
   - `ICAO: {address}` - always included
   - Message-specific fields (callsign, altitude, speed, heading, etc.)
   - `DF: {number}` - always included
   - `TC: {number}` - for Extended Squitter (DF 17/18)
   - `RSSI: {value} dBFS` - if available in output

3. **Constant Name**: Uppercase hex string, matching the frame exactly

4. **Special Notes**:
   - Position frames: Add "Pairs with {other_frame_name} for global decode"
   - Velocity frames: Add descriptor (Climbing/Descending/Level) based on vertical rate
   - All-Call (DF 11) and Comm-B (DF 20/21): Note ICAO extraction method (from CRC)

5. **File Organization by DF**:
   - Frames are added to sections ordered by Downlink Format number
   - New DF sections are created between existing sections if needed
   - Section headers follow format: `// DF {number}: {Description}`
   - Example: `// DF 21: Comm-B Identity Reply`

## Tips

- For position frames, provide both even AND odd frames together for complete test coverage
- For velocity frames, include descriptor (Climbing, Descending, Level, Fast, etc.) to make constant names meaningful
- Always include the full dump1090 output for accurate documentation
