# TCP Broadcast

Aeromux continuously broadcasts live Mode S and ADS-B data over TCP, delivering decoded aircraft information to connected clients in real time. Unlike the REST API, which requires clients to poll for updates, the broadcast system pushes data automatically as soon as new frames are received and decoded. This makes it ideal for feeding data into other ADS-B tools, building real-time dashboards, or contributing to multilateration networks.

Each broadcast format runs as an independent TCP server on its own port. Multiple clients can connect simultaneously to any broadcaster, and all connected clients receive the same data stream. Disconnected clients are detected and cleaned up automatically, so there is no need for explicit session management.

## Configuration

Each broadcast format can be independently enabled or disabled, and each listens on its own configurable TCP port. Configuration can be set through the YAML configuration file, through CLI options, or through a combination of both.

### YAML

Configure broadcast settings in the `network` section of `aeromux.yaml`:

```yaml
network:
  beastPort: 30005
  beastOutputEnabled: true

  jsonPort: 30006
  jsonOutputEnabled: false

  sbsPort: 30003
  sbsOutputEnabled: false

  bindAddress: "0.0.0.0"
```

### CLI

CLI options allow you to override any YAML setting. Each format has a port option and an enable/disable toggle:

```bash
# Beast broadcast is enabled by default on port 30005
aeromux daemon --beast-port 30005 --beast-output-enabled true

# Enable JSON broadcast on port 30006
aeromux daemon --json-port 30006 --json-output-enabled true

# Enable SBS broadcast on port 30003
aeromux daemon --sbs-port 30003 --sbs-output-enabled true

# Restrict all broadcasters to accept connections only from the local machine
aeromux daemon --bind-address 127.0.0.1

# Disable the Beast broadcaster entirely (its port will not be opened)
aeromux daemon --beast-output-enabled false
```

### Defaults

The following table summarizes the default port and enable state for each broadcast format:

| Format | Port  | Enabled by Default |
|--------|-------|--------------------|
| Beast  | 30005 | Yes                |
| JSON   | 30006 | No                 |
| SBS    | 30003 | No                 |

CLI options take precedence over YAML configuration, which in turn takes precedence over built-in defaults. When a format is disabled, its TCP port is not opened and no resources are allocated for that broadcaster.

### Receiver UUID (Beast only)

For multilateration (MLAT) networks, each receiver needs a unique identifier so that the MLAT server can distinguish between data sources. You can configure an optional UUID that will be transmitted to connected clients as part of the Beast protocol:

```yaml
receiver:
  receiverUuid: "550e8400-e29b-41d4-a716-446655440000"
```

```bash
aeromux daemon --receiver-uuid "550e8400-e29b-41d4-a716-446655440000"
```

You can generate a UUID using `uuidgen` on macOS or Linux, or `[guid]::NewGuid()` in PowerShell.

---

## Format Overview

The three broadcast formats serve different use cases and ecosystems. The following table provides a high-level comparison to help you choose the right format for your application:

| Aspect               | Beast                    | JSON                     | SBS                          |
|-----------------------|--------------------------|--------------------------|------------------------------|
| **Type**              | Binary                   | Text (UTF-8)             | Text (CSV)                   |
| **Line Terminator**   | None (binary framing)    | `\n` (LF)                | `\r\n` (CRLF)               |
| **Rate Limiting**     | None                     | 1 update/sec per aircraft | None                         |
| **Requires Tracker**  | No                       | Yes                      | Yes                          |
| **Messages Per Frame** | 1                       | 0 or 1                   | 0 to 3 (AIR, ID, MSG)       |
| **Compatibility**     | dump1090, readsb, MLAT   | Web applications         | Virtual Radar Server         |

---

## Beast Format

The Beast format is a compact binary protocol originally developed for Mode S Beast hardware receivers. It has since become the de facto standard for exchanging raw Mode S data between ADS-B applications. Aeromux's Beast output is fully compatible with dump1090, readsb, and MLAT networks such as mlat-client and adsbexchange.

Each Beast message contains the raw Mode S frame exactly as received, accompanied by a precise timestamp and a signal strength measurement. Because the format transmits raw frames rather than decoded data, it preserves all information and allows the receiving application to perform its own decoding and interpretation.

### Framing

Every Beast message begins with an escape byte (`0x1A`) that acts as a frame delimiter, followed by a single type byte that identifies the message kind, and then the payload:

```
[0x1A] [Type] [Payload...]
```

| Type   | Description            | Payload                                     |
|--------|------------------------|---------------------------------------------|
| `'2'`  | Short Mode S (56-bit)  | 6-byte timestamp + 1-byte signal + 7-byte data  |
| `'3'`  | Long Mode S (112-bit)  | 6-byte timestamp + 1-byte signal + 14-byte data |
| `0xe3` | Receiver ID            | 8-byte UUID (first 64 bits)                 |

The receiver ID message is transmitted once when the first data frame is broadcast, provided a UUID has been configured. Clients that connect after this initial transmission will not receive the receiver ID message.

### Escape Handling

Since the byte `0x1A` is used as a frame delimiter, any occurrence of `0x1A` within the payload must be escaped by doubling it to `0x1A 0x1A`. This escaping applies to all payload bytes, including the timestamp, signal strength, and Mode S data. Receiving applications must reverse this escaping by collapsing each `0x1A 0x1A` pair back into a single `0x1A` byte.

### Timestamp

The timestamp is encoded as a 48-bit unsigned integer in big-endian byte order (most significant byte first). It represents a 12 MHz counter value derived from the frame's reception time using the formula `ticks * 12 / 10`, where ticks are .NET 100-nanosecond intervals. The 48-bit counter wraps approximately every 271 days, which Beast receivers handle automatically without any special logic.

### Signal Strength

The signal strength is encoded as a single byte with a value range of 0 to 255. A square-root transformation is applied before encoding: the raw value is normalized to the 0-1 range, the square root is taken, and the result is scaled back to 0-255. This non-linear transformation preserves more resolution for weak signals (which are more common and more interesting for coverage analysis) while compressing the range used by strong signals.

### Example

You can connect to the Beast broadcaster using standard TCP tools or feed the output directly into compatible ADS-B applications:

```bash
# View the raw binary stream in hexadecimal
nc localhost 30005 | hexdump -C

# Feed Beast data into readsb for further processing
readsb --net-connector localhost,30005,beast_in
```

---

## JSON Format

The JSON broadcast delivers consolidated aircraft state as a newline-delimited JSON (NDJSON) stream over TCP. Each line contains a complete JSON object representing the full known state of a single aircraft at the time of broadcast. The JSON structure is identical to the response from the REST API detail endpoint (`/api/v1/aircraft/{icao}`), so applications that already consume the API can use the broadcast stream without any changes to their parsing logic.

Unlike the Beast and SBS formats, which emit data for every incoming frame, the JSON format operates on the consolidated aircraft state maintained by the tracker. When a new Mode S frame arrives, the tracker updates the aircraft's state, and the encoder serializes the full state into a single JSON object. This means each JSON line contains all known information about the aircraft, not just the data from the most recent frame.

### Output

Each output line is a self-contained JSON object terminated by a newline character (`\n`):

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "Identification": {
    "ICAO": "3C6545",
    "Callsign": "DLH1234",
    "Squawk": "1000",
    "Category": "Large",
    "EmergencyState": "No Emergency",
    "FlightStatus": "Airborne",
    "AdsbVersion": "DO-260B"
  },
  "DatabaseRecord": {
    "Registration": "D-AIBL",
    "Country": "Germany",
    "TypeCode": "A320",
    "TypeDescription": "Airbus A320-214",
    "TypeIcaoClass": "L2J",
    "Model": "A320-214",
    "ManufacturerIcao": "AIRBUS",
    "ManufacturerName": "AIRBUS",
    "OperatorName": "Lufthansa",
    "Pia": false,
    "Ladd": false,
    "Military": false
  },
  "Status": {
    "FirstSeen": "2026-03-13T14:25:30Z",
    "LastSeen": "2026-03-13T14:30:00Z",
    "TotalMessages": 1547,
    "PositionMessages": 312,
    "VelocityMessages": 198,
    "IdentificationMessages": 24,
    "SignalStrength": -8.5
  },
  "Position": {
    "Coordinate": { "Latitude": 47.4502, "Longitude": 19.2619 },
    "BarometricAltitude": { "Type": "Barometric", "Feet": 36000, "Meters": 10972, "FlightLevel": 360 },
    "GeometricAltitude": { "Type": "Geometric", "Feet": 35750, "Meters": 10896, "FlightLevel": null },
    "GeometricBarometricDelta": -250,
    "IsOnGround": false,
    "MovementCategory": null,
    "Source": "SDR",
    "HadMlatPosition": false,
    "LastUpdate": "2026-03-13T14:30:00Z"
  },
  "VelocityAndDynamics": {
    "Speed": { "Type": "GroundSpeed", "Knots": 450, "KilometersPerHour": 833, "MilesPerHour": 518, "MetersPerSecond": 231.48 },
    "IndicatedAirspeed": null,
    "TrueAirspeed": null,
    "GroundSpeed": null,
    "MachNumber": null,
    "Track": 125.3,
    "TrackAngle": null,
    "MagneticHeading": null,
    "TrueHeading": null,
    "Heading": null,
    "HeadingType": null,
    "HorizontalReference": null,
    "VerticalRate": 0,
    "BarometricVerticalRate": null,
    "InertialVerticalRate": null,
    "RollAngle": null,
    "TrackRate": null,
    "SpeedOnGround": null,
    "TrackOnGround": null,
    "LastUpdate": "2026-03-13T14:30:00Z"
  },
  "Autopilot": null,
  "Meteorology": null,
  "Acas": null,
  "Capabilities": null,
  "DataQuality": null
}
```

### Sections

The JSON output is organized into sections, each representing a logical group of aircraft data. Some sections are always present with populated data, while others start as `null` and become populated only after the relevant Mode S messages have been received and decoded:

| Section                | Always Present | Description                                       |
|------------------------|:--------------:|---------------------------------------------------|
| `Timestamp`            | Yes            | The UTC time when the JSON object was generated   |
| `Identification`       | Yes            | Aircraft identity including ICAO address, callsign, squawk code, category, emergency state, and ADS-B version |
| `DatabaseRecord`       | Yes            | Static aircraft metadata from the database, such as registration, type, and operator. Returns `null` when database enrichment is disabled |
| `Status`               | Yes            | Tracking statistics including first and last seen timestamps, message counts by type, and signal strength in dBFS |
| `Position`             | Yes            | Geographic coordinates, barometric and geometric altitudes, ground state, position source, and MLAT flag |
| `VelocityAndDynamics`  | Yes            | Airborne and surface speeds, track and heading angles, vertical rates from multiple sources, roll angle, and turn rate |
| `Autopilot`            | Nullable       | Autopilot engagement state, selected altitude and heading, barometric pressure setting, and vertical/horizontal navigation modes. Populated from TC 29 and BDS 4,0 messages |
| `Meteorology`          | Nullable       | Wind speed and direction, air temperatures, atmospheric pressure, radio height, and hazard severities for turbulence, wind shear, microburst, icing, and wake vortex. Populated from BDS 4,4 and BDS 4,5 messages |
| `Acas`                 | Nullable       | TCAS operational status, sensitivity level, resolution advisory state and complement, and threat encounter details. Populated from DF 0, DF 16, and TC 29 messages |
| `Capabilities`         | Nullable       | Transponder level, ADS-B features (1090ES, UAT, CDTI), trajectory change reporting, physical dimensions, GPS antenna offsets, and operational flags. Populated from DF 11, TC 31, and BDS 1,0/1,7 messages |
| `DataQuality`          | Nullable       | Navigation accuracy and integrity codes from multiple sources (TC 9-18, TC 19, TC 29, TC 31), antenna configuration, SIL supplement, and geometric vertical accuracy |

Nullable sections remain `null` until the aircraft transmits the specific message types listed in the description. Once populated, sections are never reset back to `null` during the aircraft's tracking lifetime. All sections are always present in the JSON output — they are never omitted, even when `null` — so clients can rely on a consistent schema for every message.

### Format Details

The JSON broadcast uses the same serialization conventions as the REST API, ensuring consistency across both interfaces:

- **Field names** use PascalCase, matching the C# property names in the underlying data model.
- **Enums** are serialized as human-readable strings rather than numeric values. For example, emergency state appears as `"No Emergency"` rather than `0`, and ADS-B version appears as `"DO-260B"` rather than `2`.
- **Null handling** follows an explicit inclusion policy: every field defined in the schema is always present in the output. Fields that have no data are set to `null` rather than being omitted, which allows clients to discover the full schema from any single message.
- **Altitude** is represented as a rich value object containing the altitude in multiple units: `{ "Type": "Barometric", "Feet": 36000, "Meters": 10972, "FlightLevel": 360 }`.
- **Speed** is similarly represented with multiple units: `{ "Type": "GroundSpeed", "Knots": 450, "KilometersPerHour": 833, "MilesPerHour": 518, "MetersPerSecond": 231.48 }`.
- **Timestamps** use ISO 8601 format in UTC (e.g., `"2026-03-13T14:30:00Z"`).

### Rate Limiting

To prevent excessive bandwidth consumption, the JSON broadcaster enforces a per-aircraft rate limit of one update per second. When multiple Mode S frames arrive for the same aircraft within a one-second window, only the first frame triggers a JSON broadcast. Subsequent frames still update the aircraft's state in the tracker, but the updated state is not serialized and sent until the rate limit window expires. This means clients receive at most one update per aircraft per second, each containing the most recent consolidated state.

### Example

You can connect to the JSON broadcaster using any TCP client and process the output with standard command-line tools:

```bash
# Stream all aircraft updates and pretty-print each JSON object
nc localhost 30006 | jq .

# Filter the stream to show updates for a specific aircraft only
nc localhost 30006 | jq 'select(.Identification.ICAO == "3C6545")'

# Extract just the ICAO address and coordinates for feeding into a map application
nc localhost 30006 | jq '{icao: .Identification.ICAO, lat: .Position.Coordinate.Latitude, lon: .Position.Coordinate.Longitude}'
```

---

## SBS Format

The SBS format (also known as BaseStation format) is a CSV-based text protocol originally defined by Kinetic Avionics for their BaseStation application. It has since been widely adopted by other ADS-B software, most notably Virtual Radar Server. Each output line contains 22 comma-separated fields terminated by a carriage return and line feed (`\r\n`).

Unlike the JSON format, which transmits consolidated aircraft state, the SBS format emits individual messages as they are decoded. A single incoming Mode S frame typically produces one MSG line, but certain events can generate additional AIR or ID lines alongside the MSG, resulting in up to three output lines per frame.

### Message Types

The SBS protocol defines three message types that Aeromux emits:

| Type | Description             | When Emitted                                    |
|------|-------------------------|-------------------------------------------------|
| AIR  | New aircraft detected   | Emitted once when an aircraft with a previously unseen ICAO address is first detected by the tracker |
| ID   | Callsign identified     | Emitted once when the aircraft's callsign becomes available for the first time, triggered by the first TC 1-4 identification message with a valid callsign |
| MSG  | Transmission message    | Emitted for every successfully decoded Mode S frame, with one of eight subtypes depending on the message content |

### MSG Subtypes

Each MSG line includes a subtype number (1 through 8) that indicates the kind of Mode S message that was decoded. The subtype determines which fields in the CSV line are populated:

| Subtype | Name                        | Source               | Key Fields                          |
|---------|-----------------------------|----------------------|-------------------------------------|
| MSG,1   | ES Identification           | DF 17/18, TC 1-4     | Callsign                            |
| MSG,2   | ES Surface Position         | DF 17/18, TC 5-8     | Position, ground speed, track       |
| MSG,3   | ES Airborne Position        | DF 17/18, TC 9-18    | Position, altitude, alert/emergency |
| MSG,4   | ES Airborne Velocity        | DF 17/18, TC 19      | Speed, track, vertical rate         |
| MSG,5   | Surveillance Alt Reply      | DF 4, 20             | Altitude                            |
| MSG,6   | Surveillance ID Reply       | DF 5, 21             | Squawk                              |
| MSG,7   | Air-to-Air Surveillance     | DF 0, 16             | Altitude                            |
| MSG,8   | All-Call Reply              | DF 11                | Ground flag only                    |

MSG,5 and MSG,6 messages are subject to an Extended Squitter filter: they are only emitted for aircraft that have previously sent at least one Extended Squitter message (MSG,1, MSG,2, MSG,3, MSG,4, or MSG,8). This filtering prevents surveillance replies from non-ADS-B transponders from appearing in the output before the aircraft has been confirmed as an Extended Squitter participant.

### Field Layout

Each SBS line consists of exactly 22 comma-separated fields. Here is an annotated example of a MSG,3 (airborne position) message:

```
MSG,3,1,1,3C6545,1,2026/03/13,14:30:00.123,2026/03/13,14:30:00.456,,36000,,,,47.4502,19.2619,,,,0,,0
```

The following table describes each field position and its meaning:

| # | Field              | Description                                           |
|---|--------------------|-------------------------------------------------------|
| 1 | Message type       | The message category: `MSG`, `AIR`, or `ID`           |
| 2 | Transmission type  | The MSG subtype number (1-8), or empty for AIR and ID messages |
| 3 | Session ID         | Database session record number, always `1` in Aeromux |
| 4 | Aircraft ID        | Database aircraft record number, always `1` in Aeromux |
| 5 | Hex ident          | The 24-bit ICAO address as a 6-character hexadecimal string (e.g., `3C6545`) |
| 6 | Flight ID          | Database flight record number, always `1` in Aeromux  |
| 7 | Date generated     | The date when the aircraft transmitted the message, formatted as `yyyy/MM/dd` |
| 8 | Time generated     | The time when the aircraft transmitted the message, formatted as `HH:mm:ss.fff` with millisecond precision |
| 9 | Date logged        | The current UTC date when Aeromux generated the SBS line, formatted as `yyyy/MM/dd` |
| 10| Time logged        | The current UTC time when Aeromux generated the SBS line, formatted as `HH:mm:ss.fff` with millisecond precision |
| 11| Callsign           | The aircraft's 8-character flight identification (e.g., `DLH1234 `), space-padded |
| 12| Altitude           | Barometric altitude in feet above mean sea level      |
| 13| Ground speed       | Speed over the ground in knots                        |
| 14| Track              | Track angle in degrees (0-360), measured clockwise from true north |
| 15| Latitude           | Position latitude in decimal degrees (positive values indicate North, negative values indicate South) |
| 16| Longitude          | Position longitude in decimal degrees (positive values indicate East, negative values indicate West) |
| 17| Vertical rate      | Rate of climb or descent in feet per minute (positive values indicate climb, negative values indicate descent) |
| 18| Squawk             | The 4-digit octal Mode A transponder code (e.g., `7700` for emergency) |
| 19| Alert              | Squawk change flag: `-1` indicates the squawk has changed, `0` indicates no change, and an empty field means not applicable for this message type |
| 20| Emergency          | Emergency flag: `-1` indicates an emergency condition, `0` indicates normal operations, and an empty field means not applicable |
| 21| SPI                | Special Position Identification (ident) flag: `-1` indicates the pilot has pressed the ident button, `0` indicates no ident, and an empty field means not applicable |
| 22| Is on ground       | Ground status flag: `-1` indicates the aircraft is on the ground, `0` indicates it is airborne |

When a field is not applicable for a particular message type, it is left empty (two consecutive commas with no content between them).

### Field Population by Message Type

The first ten fields (message type through time logged) are always populated for every message. Fields 11 through 22 are populated selectively depending on the message type. The following matrix shows which data fields are present in each message type:

| Field     | MSG,1 | MSG,2 | MSG,3 | MSG,4 | MSG,5 | MSG,6 | MSG,7 | MSG,8 | AIR | ID |
|-----------|:-----:|:-----:|:-----:|:-----:|:-----:|:-----:|:-----:|:-----:|:---:|:--:|
| Callsign  |   *   |       |       |       |       |       |       |       |     | *  |
| Altitude  |       |   *   |   *   |       |   *   |   *   |   *   |       |     |    |
| Speed     |       |   *   |       |   *   |       |       |       |       |     |    |
| Track     |       |   *   |       |   *   |       |       |       |       |     |    |
| Latitude  |       |   *   |   *   |       |       |       |       |       |     |    |
| Longitude |       |   *   |   *   |       |       |       |       |       |     |    |
| Vert rate |       |       |       |   *   |       |       |       |       |     |    |
| Squawk    |       |       |       |       |       |   *   |       |       |     |    |
| Alert     |       |       |   *   |       |   *   |   *   |       |       |     |    |
| Emergency |       |       |   *   |       |       |   *   |       |       |     |    |
| SPI       |       |       |   *   |       |   *   |   *   |       |       |     |    |
| On ground |       |   *   |   *   |       |   *   |   *   |   *   |   *   |     |    |

### Example

You can connect to the SBS broadcaster with any TCP client. The output is human-readable CSV text that can be processed with standard text tools or fed directly into compatible applications:

```bash
# Stream SBS data to the terminal
nc localhost 30003

# Configure Virtual Radar Server to consume this data by adding a BaseStation
# data source with host "localhost" and port 30003
```

The following sample output shows the typical sequence of messages when a new aircraft is detected. First, an AIR message announces the new aircraft. Then, as Mode S frames are decoded, MSG lines appear with the relevant data. When the callsign is received for the first time, an ID message is emitted alongside the corresponding MSG,1:

```
AIR,,1,1,3C6545,1,2026/03/13,14:29:55.000,2026/03/13,14:29:55.100,,,,,,,,,,,,
MSG,3,1,1,3C6545,1,2026/03/13,14:30:00.123,2026/03/13,14:30:00.456,,36000,,,,47.4502,19.2619,,,,0,,0
MSG,4,1,1,3C6545,1,2026/03/13,14:30:00.234,2026/03/13,14:30:00.567,,,450,125.3,,,,0,,,,
MSG,1,1,1,3C6545,1,2026/03/13,14:30:01.345,2026/03/13,14:30:01.678,DLH1234 ,,,,,,,,,,,
ID,,1,1,3C6545,1,2026/03/13,14:30:01.345,2026/03/13,14:30:01.678,DLH1234 ,,,,,,,,,,,
```

---

## Connection

All three broadcast formats use plain, unencrypted TCP connections. There is no handshake, authentication, or protocol negotiation — clients simply open a TCP connection to the appropriate port and immediately begin receiving data.

You can connect using any TCP client, such as `nc` (netcat):

```bash
# Connect to the Beast broadcaster (binary output)
nc localhost 30005

# Connect to the JSON broadcaster (one JSON object per line)
nc localhost 30006

# Connect to the SBS broadcaster (CSV text, one message per line)
nc localhost 30003
```

Clients may connect and disconnect at any time without affecting other connected clients or the broadcaster itself. The broadcaster begins sending data to a new client immediately upon connection, starting with whatever frames arrive next. There is no mechanism to request historical data or replay missed messages — the broadcast is strictly real-time.
