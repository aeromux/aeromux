# Aviation/Mode-S Documentation Standards

Detailed reference for documenting Mode-S, ADS-B, and aviation-specific code in aeromux.

## Mode-S Message Type Documentation

### Downlink Format (DF) Reference

Always document the DF code in message parser methods:

| DF | Name | Documentation Template |
|----|------|------------------------|
| 0 | Short Air-Air Surveillance (ACAS) | `/// Parses short air-air surveillance (DF 0).` |
| 4 | Surveillance Altitude Reply | `/// Parses surveillance altitude reply (DF 4).` |
| 5 | Surveillance Identity Reply | `/// Parses surveillance identity reply (DF 5).` |
| 11 | All-Call Reply | `/// Parses all-call reply (DF 11).` |
| 16 | Long Air-Air Surveillance (ACAS) | `/// Parses long air-air surveillance (DF 16).` |
| 17 | Extended Squitter | `/// Parses Extended Squitter (DF 17, TC XX).` |
| 18 | Extended Squitter (Non-Transponder) | `/// Parses Extended Squitter from non-transponder devices (DF 18, TC XX).` |
| 20/21 | Comm-B Altitude/Identity | `/// Parses Comm-B altitude reply (DF 20, BDS X,X).` |

### Extended Squitter Type Codes (TC)

For DF 17/18 messages, always document the TC:

| TC Range | Message Type | Example |
|----------|--------------|---------|
| 1-4 | Aircraft Identification | `/// Decodes callsign from aircraft identification (DF 17, TC 1-4).` |
| 5-8 | Surface Position | `/// Decodes surface position using CPR (DF 17, TC 5-8, BDS 0,6).` |
| 9-18 | Airborne Position | `/// Decodes airborne position using CPR (DF 17, TC 9-18, BDS 0,5).` |
| 19 | Airborne Velocity | `/// Decodes ground speed and heading (DF 17, TC 19).` |
| 20-22 | Airborne Position (GNSS) | `/// Decodes GNSS-based position (DF 17, TC 20-22).` |
| 28 | Aircraft Status | `/// Parses aircraft status (DF 17, TC 28).` |
| 29 | Target State and Status | `/// Parses autopilot selected altitude/heading (DF 17, TC 29).` |
| 31 | Operational Status | `/// Parses aircraft operational status and capabilities (DF 17, TC 31).` |

### Comm-B BDS (Binary Data Selector) Codes

BDS codes identify the type of information in Comm-B messages (DF 20/21). The BDS code is transmitted in the uplink interrogation but not included in the downlink response, making proper documentation critical for understanding message content.

#### ADS-B Extended Squitter (DF 17/18)

These BDS codes are assigned to Extended Squitter messages, even though they use a different message format:

| BDS | Content | Example |
|-----|---------|---------|
| 0,5 | Extended squitter airborne position | `/// Decodes airborne position using CPR (DF 17, TC 9-18, BDS 0,5).` |
| 0,6 | Extended squitter surface position | `/// Decodes surface position using CPR (DF 17, TC 5-8, BDS 0,6).` |
| 0,7 | Extended squitter status | `/// Parses extended squitter status (DF 17, TC 28, BDS 0,7).` |
| 0,8 | Extended squitter identification and category | `/// Decodes callsign and emitter category (DF 17, TC 1-4, BDS 0,8).` |
| 0,9 | Extended squitter airborne velocity | `/// Decodes velocity vector and vertical rate (DF 17, TC 19, BDS 0,9).` |
| 0,A | Extended squitter event-driven information | `/// Parses event-driven data (BDS 0,A).` |

#### Mode S Elementary Surveillance (ELS) - DF 20/21

Basic surveillance capabilities supported by all Mode S transponders:

| BDS | Content | Example |
|-----|---------|---------|
| 1,0 | Data link capability report | `/// Parses transponder capabilities and protocol version (DF 20/21, BDS 1,0).` |
| 1,7 | Common usage GICB capability report | `/// Decodes Ground-Initiated Comm-B register availability (DF 20/21, BDS 1,7).` |
| 2,0 | Aircraft identification | `/// Extracts 8-character callsign (DF 20/21, BDS 2,0).` |
| 2,1 | Aircraft registration number | `/// Decodes aircraft registration/tail number (DF 20/21, BDS 2,1).` |
| 3,0 | ACAS active resolution advisory | `/// Parses TCAS resolution advisory details (DF 20/21, BDS 3,0).` |

#### Mode S Enhanced Surveillance (EHS) - DF 20/21

Advanced surveillance data for air traffic management:

| BDS | Content | Example |
|-----|---------|---------|
| 4,0 | Selected vertical intention | `/// Decodes MCP/FCU selected altitude and barometric setting (DF 20/21, BDS 4,0).` |
| 4,1 | Next waypoint identifier | `/// Extracts next waypoint identifier from FMS (DF 20/21, BDS 4,1).` |
| 4,2 | Next waypoint position | `/// Decodes next waypoint latitude/longitude (DF 20/21, BDS 4,2).` |
| 4,3 | Next waypoint information | `/// Parses ETA and altitude for next waypoint (DF 20/21, BDS 4,3).` |
| 4,8 | VHF channel report | `/// Decodes current VHF communication channel (DF 20/21, BDS 4,8).` |
| 5,0 | Track and turn report | `/// Parses roll angle, track angle, track rate, ground speed, and TAS (DF 20/21, BDS 5,0).` |
| 5,1 | Position coarse | `/// Decodes coarse latitude/longitude position (DF 20/21, BDS 5,1).` |
| 5,2 | Position fine | `/// Decodes fine-resolution latitude/longitude (DF 20/21, BDS 5,2).` |
| 5,3 | Air-referenced state vector | `/// Parses air-relative velocity components (DF 20/21, BDS 5,3).` |
| 5,4 | Waypoint 1 | `/// Decodes first waypoint from flight plan (DF 20/21, BDS 5,4).` |
| 5,5 | Waypoint 2 | `/// Decodes second waypoint from flight plan (DF 20/21, BDS 5,5).` |
| 5,6 | Waypoint 3 | `/// Decodes third waypoint from flight plan (DF 20/21, BDS 5,6).` |
| 5,F | Quasi-static parameter monitoring | `/// Parses quasi-static aircraft parameters (DF 20/21, BDS 5,F).` |
| 6,0 | Heading and speed report | `/// Decodes magnetic heading, IAS, Mach, and vertical rates (DF 20/21, BDS 6,0).` |

#### Meteorological Services - DF 20/21

Weather-related information transmitted by equipped aircraft:

| BDS | Content | Example |
|-----|---------|---------|
| 4,4 | Meteorological routine air report (MRAR) | `/// Parses wind, temperature, pressure, and humidity (DF 20/21, BDS 4,4).` |
| 4,5 | Meteorological hazard report (MHR) | `/// Decodes turbulence, wind shear, icing, and hazard conditions (DF 20/21, BDS 4,5).` |

#### Reserved and Special Purpose

| BDS | Content | Example |
|-----|---------|---------|
| E,1 | Mode S BITE (Built-In Test Equipment) | `/// Reserved for transponder diagnostics (DF 20/21, BDS E,1).` |
| E,2 | Mode S BITE (Built-In Test Equipment) | `/// Reserved for transponder diagnostics (DF 20/21, BDS E,2).` |
| F,1 | Military applications | `/// Military-specific data (DF 20/21, BDS F,1).` |

#### BDS Code Inference

**Important:** BDS codes are NOT included in Comm-B downlink messages (DF 20/21). They must be inferred using:
1. **Reserved bits** - Must be zero for valid BDS codes
2. **Status bits** - When status bit = 0, corresponding field must be all zeros
3. **Value ranges** - Physical constraints (e.g., Mach < 1, temperature between -80 and +60 C)
4. **Cross-validation** - Compare with ADS-B data when available

**Example documentation for BDS inference challenges:**
```csharp
/// <remarks>
/// BDS 5,0 and BDS 6,0 have similar structures and may require additional validation.
/// Use ground speed vs TAS differential for BDS 5,0 (should be < 200 kt with wind).
/// For BDS 6,0, compare Mach-derived CAS with IAS (should agree within ~15 kt).
/// See ICAO Doc 9871 and "The 1090MHz Riddle" (Junzi Sun) for inference algorithms.
/// </remarks>
```

## CPR (Compact Position Reporting) Documentation

When implementing CPR decoding, explain:

```csharp
/// <summary>
/// Decodes global position from even and odd CPR frames.
/// </summary>
/// <param name="evenFrame">The even CPR-encoded latitude/longitude.</param>
/// <param name="oddFrame">The odd CPR-encoded latitude/longitude.</param>
/// <returns>Decoded WGS84 position in decimal degrees.</returns>
/// <remarks>
/// Global CPR decoding requires both even (CPR format = 0) and odd (CPR format = 1)
/// frames to resolve position ambiguity. Frames must be less than 10 seconds apart.
///
/// The algorithm divides the globe into latitude zones and uses the format bit
/// to determine which zone contains the aircraft. See ICAO Annex 10 Volume IV,
/// Section 3.1.2.8.7 for full algorithm details.
///
/// If frames are more than 10 seconds apart, position may be incorrect due to
/// aircraft movement between transmissions.
/// </remarks>
public Position DecodeGlobalPosition(CprFrame evenFrame, CprFrame oddFrame)
```

**Key points to document:**
- Even vs odd frame requirement
- Timeout constraints (typically 10 seconds)
- Local vs global decoding
- Latitude zone calculation
- Reference to ICAO Annex 10

## State Tracking Documentation

When documenting aircraft state trackers or handlers:

```csharp
/// <summary>
/// Updates aircraft position from surface position messages.
/// </summary>
/// <remarks>
/// Surface position messages (TC 5-8) use a different CPR encoding than airborne
/// messages. The latitude zone is smaller (90 NM vs 360 NM) to provide better
/// resolution for ground movement.
///
/// Position is only updated if:
/// - Both even and odd frames received within 10 seconds
/// - Frame timestamp is newer than last position update
/// - Decoded position is within valid airport surface area
///
/// If position decode fails, the message is buffered for up to 30 seconds
/// waiting for the complementary frame.
/// </remarks>
public void UpdateSurfacePosition(SurfacePositionMessage message)
```

**Document:**
- Update conditions and constraints
- Buffering/timeout behavior
- Data validation rules
- Assumptions about message ordering
- How stale data is handled

## Accuracy and Integrity Documentation

When working with NIC, NACp, SIL, etc.:

```csharp
/// <summary>
/// Gets the Navigation Accuracy Category for Position (NACp).
/// </summary>
/// <remarks>
/// NACp indicates the accuracy of the reported position:
/// - 11: &lt; 3 meters
/// - 10: &lt; 10 meters
/// - 9: &lt; 30 meters
/// - 8: &lt; 93 meters (0.05 NM)
/// - Lower values indicate decreasing accuracy
///
/// Derived from TC 31 operational status messages. If not received,
/// defaults to 0 (unknown).
/// </remarks>
public byte NavigationAccuracyPosition { get; set; }
```

## Common Pitfalls to Document

**Bit ordering:**
```csharp
// Mode-S uses MSB-first bit numbering, but C# BitArray is LSB-first
// Bit 0 in ICAO spec = bit 7 in our byte, bit 1 = bit 6, etc.
```

**Parity checks:**
```csharp
// All Mode-S messages have 24-bit parity in the last 3 bytes
// DF 11 and 17 use Address/Parity (AP) where parity = ICAO address
```

**Reserved/special values:**
```csharp
// Altitude encoding: 0 means "information not available"
// Velocity subtype 0 is reserved and should not occur
```

## ICAO References

When referencing standards, use this format:

```csharp
/// <remarks>
/// Implements the CPR decoding algorithm from ICAO Annex 10, Volume IV,
/// Section 3.1.2.8.7 (Edition 3, July 2007).
/// </remarks>
```

Common references:
- **ICAO Annex 10, Volume IV** - Surveillance and Collision Avoidance Systems
- **RTCA DO-260B** - ADS-B MOPS (Minimum Operational Performance Standards)
- **EUROCAE ED-102A** - European equivalent of DO-260B

## Complete Message Parser Example

```csharp
/// <summary>
/// Parses Extended Squitter airborne velocity messages (DF 17, TC 19).
/// </summary>
/// <param name="message">The 112-bit Extended Squitter message.</param>
/// <returns>Decoded velocity data including ground speed, heading, and vertical rate.</returns>
/// <exception cref="ArgumentException">Thrown when message format is not TC 19.</exception>
/// <remarks>
/// TC 19 messages encode velocity in one of four subtypes:
/// - Subtype 1: Ground speed (subsonic), heading via East/West and North/South components
/// - Subtype 2: Ground speed (supersonic), same encoding as subtype 1
/// - Subtype 3: True airspeed, magnetic heading
/// - Subtype 4: Indicated airspeed, magnetic heading
///
/// Vertical rate is encoded in all subtypes as 64 fpm resolution with sign bit.
///
/// GNSS and barometric altitude difference is included when available, indicating
/// the delta between GPS altitude and pressure altitude.
///
/// See ICAO Annex 10 Volume IV, Section 3.1.2.8.4.2.
/// </remarks>
public VelocityData ParseAirborneVelocity(byte[] message)
{
    // Implementation...
}
```

---

## Inline Comment Standards

### Explain WHY, Not WHAT

The code already shows WHAT it does. Comments should explain WHY.

**Bad - Explains WHAT:**
```csharp
// Check if frame is even
if (cprFormat == 0)
{
    // Store even frame
    evenFrame = frame;
}
```

**Good - Explains WHY:**
```csharp
// CPR global decoding requires both even (format=0) and odd (format=1) frames
// to resolve latitude zone ambiguity. Buffer frames until we have both.
if (cprFormat == 0)
{
    evenFrame = frame;
}
```

### When to Use Inline Comments

**Use inline comments for:**

1. **Complex algorithms that aren't self-evident:**
```csharp
// Calculate NL (number of longitude zones) using the ICAO formula
// NL decreases from 59 at equator to 1 at poles
double nl = Math.Floor(2 * Math.PI / Math.Acos(1 - (1 - Math.Cos(Math.PI / (2 * nz))) / Math.Pow(Math.Cos(Math.PI / 180 * lat), 2)));
```

2. **Non-obvious bit manipulation:**
```csharp
// Extract Q-bit (bit 48) which indicates 25-foot encoding vs Gillham code
// Q=1: 25-foot LSB encoding, Q=0: 100-foot Gillham code (legacy)
bool qBit = (message[5] & 0x01) != 0;
```

3. **ICAO/domain-specific requirements:**
```csharp
// ICAO Annex 10: CPR frames must be within 10 seconds to avoid position jumps
// due to aircraft movement between transmissions
if ((oddFrame.Timestamp - evenFrame.Timestamp).TotalSeconds > 10)
    return null;
```

4. **Edge cases and gotchas:**
```csharp
// Special case: Altitude code 0 means "information not available", not sea level
if (altitudeCode == 0)
    return null;
```

5. **Why code differs from the obvious approach:**
```csharp
// Don't validate parity here - already done by demodulator
// Re-validating would waste 30% of CPU time
var df = ExtractDownlinkFormat(message);
```

6. **Workarounds for external issues:**
```csharp
// Some transponders incorrectly set reserved bits to 1 (non-compliant)
// Accept message anyway per operational practice
if (!ValidateReservedBits(message, strict: false))
    return ParseNonCompliant(message);
```

### When NOT to Use Inline Comments

**Don't comment what is obvious from the code itself:**

```csharp
// BAD: Comment just repeats the code
// Increment counter
counter++;

// BAD: Poor name requires explanation - use a better name instead
int x = 0; // ICAO address

// GOOD: Self-documenting name, no comment needed
int icaoAddress = 0;
```

### Magic Numbers and Constants

Always explain magic numbers from specifications:

```csharp
// ICAO Annex 10: Altitude LSB is 25 feet when Q-bit is set
const int AltitudeLsb = 25;

// ICAO uses 1013.25 mb as standard pressure (ISA)
const double StandardPressure = 1013.25;

// BDS 6,0: Mach number LSB is 0.004 (2.048 / 512)
double mach = rawValue * 0.004;  // 10 bits, range 0 to 2.048
```

### Algorithm Step Markers

For complex multi-step algorithms, mark the steps:

```csharp
// CPR Global Position Decoding (ICAO Annex 10, Vol IV, 3.1.2.8.7)

// Step 1: Calculate latitude index
int j = (int)Math.Floor(((59 * latEven) - (60 * latOdd)) / 131072.0 + 0.5);

// Step 2: Calculate latitude from even and odd frames
double latEvenDecoded = (360.0 / 60) * (j + latEven / 131072.0);
double latOddDecoded = (360.0 / 59) * (j + latOdd / 131072.0);

// Step 3: Select most recent latitude based on frame timestamps
double latitude = evenFrameNewer ? latEvenDecoded : latOddDecoded;

// Step 4: Calculate longitude zones (NL function)
int nl = CalculateNL(latitude);

// Step 5: Calculate longitude using selected latitude zone
double longitude = CalculateLongitude(lonEven, lonOdd, nl, evenFrameNewer);
```

### Bit Layout Diagrams

For complex bit extraction, show the layout:

```csharp
// DF 20/21 Message Structure (112 bits):
//
// |   DF  | FS | DR |  UM  |  AC/ID  |     MB (56 bits)      | Parity |
// | 1-5   |6-8 |9-13|14-19 |  20-32  |       33-88           | 89-112 |
// | 5 bits|3   | 5  |  6   | 13 bits |      56 bits          | 24 bits|

byte df = (byte)((message[0] >> 3) & 0x1F);           // Bits 1-5
byte fs = (byte)(message[0] & 0x07);                  // Bits 6-8
```

### Units and Coordinate Systems

Always specify units and coordinate systems:

```csharp
// Heading is in degrees true north (0-360)
// Positive clockwise: 0=North, 90=East, 180=South, 270=West
double headingTrue = ExtractHeading(message);

// Position in WGS84 decimal degrees
// Latitude: -90 (South) to +90 (North)
// Longitude: -180 (West) to +180 (East)
var position = new Position(latitude, longitude);
```

### Complex Conditional Comments

Explain complex boolean logic:

```csharp
// Accept message only if:
// 1. Parity is valid (prevents corrupted data)
// 2. ICAO address is non-zero (0x000000 is reserved/invalid)
// 3. Either: frame is recent (<30 sec) OR it's a position update (critical data)
if (ValidateParity(message) &&
    icaoAddress != 0 &&
    (ageSeconds < 30 || messageType == MessageType.Position))
{
    ProcessMessage(message);
}
```

---

**Use this reference when:**
- Documenting new message parsers
- Reviewing existing documentation for completeness
- Ensuring consistent terminology across the codebase
- Adding domain context that non-aviation developers need
- Reviewing inline comments for clarity and necessity
