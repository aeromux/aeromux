# REST API

Aeromux includes a read-only REST API that provides programmatic access to live aircraft tracking data. The API is designed for building web interfaces, rendering map visualizations, and integrating with third-party tools that need structured, on-demand access to the current tracking state.

All API responses are served over plain HTTP without TLS encryption. This design is intentional — the API is intended for use on local networks or behind a reverse proxy that handles TLS termination. If you need encrypted access from external clients, place a reverse proxy such as nginx or Caddy in front of Aeromux.

## Configuration

The REST API is enabled by default and listens on port 8080. You can change the port or disable the API entirely through the YAML configuration file or via CLI options.

In `aeromux.yaml`, the API settings are part of the `network` section:

```yaml
network:
  apiPort: 8080
  apiEnabled: true
```

The same settings can be controlled via CLI options, which take precedence over the YAML configuration:

```bash
aeromux daemon --api-port 8080 --api-enabled true
aeromux daemon --api-enabled false  # disable the API
```

When the API is disabled, the HTTP server is not started and the port is not opened.

## Endpoints

The API exposes five endpoints, all under the `/api/v1/` prefix. All endpoints accept only `GET` requests:

| Endpoint                              | Description                                                                    |
|---------------------------------------|--------------------------------------------------------------------------------|
| `GET /api/v1/aircraft`                | Returns a compact list of all currently tracked aircraft                       |
| `GET /api/v1/aircraft/{icao}`         | Returns detailed information for a single aircraft, organized into 10 sections |
| `GET /api/v1/aircraft/{icao}/history` | Returns position, altitude, velocity, and state history for a single aircraft  |
| `GET /api/v1/stats`                   | Returns receiver and session statistics including uptime and frame rates       |
| `GET /api/v1/health`                  | Lightweight health check for Docker, monitoring tools, and readiness probes    |

## Aircraft List

The aircraft list endpoint returns a compact summary of all currently tracked aircraft in a single response. Each aircraft entry includes the most commonly needed fields — identification, position, speed, altitude, and signal quality — making it suitable for rendering aircraft tables or map markers without requiring individual detail queries.

```bash
curl -s "http://localhost:8080/api/v1/aircraft"
```

```json
{
  "Count": 42,
  "Timestamp": "2026-03-13T14:30:00Z",
  "Aircraft": [
    {
      "ICAO": "3C6545",
      "Callsign": "DLH1234",
      "Squawk": "1000",
      "Category": "Large",
      "Coordinate": { "Latitude": 47.4502, "Longitude": 19.2619 },
      "BarometricAltitude": { "Type": "Barometric", "Feet": 36000, "Meters": 10972, "FlightLevel": 360 },
      "GeometricAltitude": null,
      "IsOnGround": false,
      "Speed": { "Type": "GroundSpeed", "Knots": 450, "KilometersPerHour": 833, "MilesPerHour": 518, "MetersPerSecond": 231.48 },
      "Track": 125.3,
      "SpeedOnGround": null,
      "TrackOnGround": null,
      "VerticalRate": 0,
      "SignalStrength": -8.5,
      "TotalMessages": 1547,
      "LastSeen": "2026-03-13T14:29:58Z",
      "DatabaseEnabled": true,
      "Registration": "D-AIBL",
      "TypeCode": "A320",
      "OperatorName": "Lufthansa"
    }
  ]
}
```

The response includes a `Count` field for convenience, a `Timestamp` indicating when the response was generated, and an `Aircraft` array containing one entry per tracked aircraft. Aircraft that have timed out (no messages received within the configured timeout period) are automatically removed from the list.

### Query Parameters

The aircraft list endpoint supports optional query parameters for filtering results:

| Parameter | Type   | Default | Description                                                                  |
|-----------|--------|---------|------------------------------------------------------------------------------|
| `bounds`  | string | (none)  | Geographic bounding box: `<south>,<west>,<north>,<east>`                     |
| `search`  | string | (none)  | Case-insensitive substring search across callsign, ICAO, squawk, registration |

The `bounds` and `search` parameters are mutually exclusive. Providing both returns HTTP 400.

**Bounds filtering** returns only aircraft with a known position within the specified bounding box. Aircraft without a decoded position are excluded. Latitude values must be between -90 and 90, longitude values between -180 and 180, and south must be less than north.

```bash
# Aircraft within a geographic bounding box
curl -s "http://localhost:8080/api/v1/aircraft?bounds=46.5,18.0,48.0,20.5"
```

**Search filtering** performs a case-insensitive substring match against ICAO address, callsign, squawk code, and registration (when the aircraft database is enabled). Search queries all tracked aircraft regardless of position — aircraft without a known position are included in search results.

```bash
# Search by callsign substring
curl -s "http://localhost:8080/api/v1/aircraft?search=BAW"

# Search by ICAO address
curl -s "http://localhost:8080/api/v1/aircraft?search=A0B1C2"

# Search by squawk code
curl -s "http://localhost:8080/api/v1/aircraft?search=7700"
```

**Error responses:**

```bash
# Invalid bounds format
curl -s "http://localhost:8080/api/v1/aircraft?bounds=invalid"
# → 400: {"Error": "Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got 1"}

# South >= north
curl -s "http://localhost:8080/api/v1/aircraft?bounds=48.0,18.0,46.5,20.5"
# → 400: {"Error": "Invalid bounds: south (48) must be less than north (46.5)"}

# Both parameters provided
curl -s "http://localhost:8080/api/v1/aircraft?bounds=46.5,18.0,48.0,20.5&search=BAW"
# → 400: {"Error": "Parameters 'bounds' and 'search' are mutually exclusive"}
```

## Aircraft Detail

The aircraft detail endpoint returns comprehensive information for a single aircraft, organized into logical sections. The ICAO address in the URL is case-insensitive — `407f19`, `407F19`, and `407f19` all resolve to the same aircraft.

You can request all sections at once, or use the optional `sections` query parameter to retrieve only the sections you need. This is useful for reducing response size when you only need specific data:

```bash
# Retrieve all sections for the aircraft
curl -s "http://localhost:8080/api/v1/aircraft/407F19"

# Retrieve only the Position and Autopilot sections
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Position,Autopilot"

# Retrieve a single section
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Identification"
```

### Available Sections

The detail endpoint organizes aircraft data into 10 sections. Each section groups related information by topic rather than by message source, combining data from multiple Mode S message types where appropriate:

| Section                | Description                                                                                                                           |
|------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| `Identification`       | Aircraft identity including ICAO address, callsign, squawk code, category, emergency state, and ADS-B version                         |
| `DatabaseRecord`       | Static aircraft metadata from the database, such as registration, type code, manufacturer, and operator                               |
| `Status`               | Tracking statistics including first and last seen timestamps, message counts by type, and signal strength                             |
| `Position`             | Geographic coordinates, barometric and geometric altitudes, ground state, position source, and MLAT flag                              |
| `VelocityAndDynamics`  | Airborne and surface speeds, track and heading angles, vertical rates from multiple sources, roll angle, and turn rate                |
| `Autopilot`            | Autopilot engagement state, selected altitude and heading, barometric pressure setting, and navigation modes                          |
| `Meteorology`          | Wind speed and direction, air temperatures, atmospheric pressure, radio height, and hazard severities                                 |
| `Acas`                 | TCAS operational status, sensitivity level, resolution advisory state and complement, and threat encounter details                    |
| `Capabilities`         | Transponder level, ADS-B features, trajectory change reporting, physical dimensions, GPS antenna offsets, and operational flags       |
| `DataQuality`          | Navigation accuracy and integrity codes from multiple sources, antenna configuration, SIL supplement, and geometric vertical accuracy |

When the `sections` parameter is omitted, all 10 sections are included in the response. When specified, only the requested sections appear — unrequested sections are absent entirely from the response rather than being set to `null`. The `Timestamp` field is always present regardless of which sections are requested.

Sections that depend on specific Mode S message types will be returned as `null` until the aircraft transmits the relevant messages. For example, the `Meteorology` section remains `null` until BDS 4,4 or BDS 4,5 messages are received from the aircraft.

### Example: Identification

The Identification section contains the aircraft's core identity and transponder state:

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Identification"
```

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "Identification": {
    "ICAO": "407F19",
    "Callsign": "VIR359",
    "Squawk": "2646",
    "Category": "Heavy",
    "EmergencyState": "No Emergency",
    "FlightStatus": "Airborne",
    "AdsbVersion": "DO-260B"
  }
}
```

### Example: Position and VelocityAndDynamics

Multiple sections can be requested in a single call by separating section names with commas. This example retrieves both the Position and VelocityAndDynamics sections, which together provide a complete picture of where the aircraft is and how it is moving:

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Position,VelocityAndDynamics"
```

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "Position": {
    "Coordinate": { "Latitude": 47.3975, "Longitude": 18.5238 },
    "BarometricAltitude": { "Type": "Barometric", "Feet": 38000, "Meters": 11582, "FlightLevel": 380 },
    "GeometricAltitude": { "Type": "Geometric", "Feet": 37725, "Meters": 11499, "FlightLevel": null },
    "GeometricBarometricDelta": -275,
    "IsOnGround": false,
    "MovementCategory": null,
    "Source": "SDR",
    "HadMlatPosition": false,
    "LastUpdate": "2026-03-13T14:29:59Z"
  },
  "VelocityAndDynamics": {
    "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93 },
    "IndicatedAirspeed": null,
    "TrueAirspeed": null,
    "GroundSpeed": null,
    "MachNumber": null,
    "Track": 294.59,
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
    "MagneticDeclination": null,
    "LastUpdate": "2026-03-13T14:29:59Z"
  }
}
```

### Example: DatabaseRecord

The DatabaseRecord section provides static aircraft metadata from the [aeromux-db](https://github.com/aeromux/aeromux-db) database, including registration, aircraft type, manufacturer, and operator information. This data is looked up by ICAO address when the aircraft is first detected.

When database enrichment is enabled and a matching record exists:

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=DatabaseRecord"
```

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "DatabaseRecord": {
    "Registration": "G-VWHO",
    "Country": "United Kingdom",
    "TypeCode": "A346",
    "TypeDescription": "Airbus A340-642",
    "TypeIcaoClass": "L4J",
    "Model": "A340-642",
    "ManufacturerIcao": "AIRBUS",
    "ManufacturerName": "AIRBUS",
    "OperatorName": "Virgin Atlantic",
    "Pia": false,
    "Ladd": false,
    "Military": false
  }
}
```

When database enrichment is disabled in the configuration, the `DatabaseRecord` section is returned as `null`:

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "DatabaseRecord": null
}
```

### Example: Null section (no data yet)

Sections that depend on specific Mode S message types are returned as `null` until the aircraft transmits the relevant messages. This is normal behavior for newly tracked aircraft that have not yet sent all message types:

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Meteorology,Autopilot"
```

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "Meteorology": null,
  "Autopilot": null
}
```

## Aircraft History

The history endpoint returns time-series data for a single aircraft, providing a trail of past positions, altitudes, velocities, and combined state snapshots. This data is useful for rendering flight paths on maps, plotting altitude profiles, analyzing speed changes over time, and building correlated views where position, altitude, and speed are needed together.

History data is stored in circular buffers with a configurable capacity. When the buffer is full, the oldest entries are automatically discarded to make room for new ones. The buffer capacity, current entry count, and sequence ID range are included in the response metadata.

Each entry in a history buffer is assigned a **sequence ID** — a monotonically increasing `long` starting at 1, scoped per aircraft per buffer type. Sequence IDs enable efficient incremental polling via the `after` query parameter, so clients only receive entries they have not yet seen.

### Query Parameters

| Parameter | Type   | Default | Description                                                         |
|-----------|--------|---------|---------------------------------------------------------------------|
| `type`    | string | (all)   | Single history type: `Position`, `Altitude`, `Velocity`, or `State` |
| `limit`   | int    | (all)   | Return only the N most recent entries                               |
| `after`   | long   | (none)  | Return only entries with sequence ID greater than this value        |

- `type` accepts a **single value only**. When omitted, all four types are returned.
- `after` **requires** `type` — using `after` without `type` returns HTTP 400.
- When both `after` and `limit` are provided, `after` takes precedence and `limit` is ignored.

```bash
# Retrieve all history types for an aircraft
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history"

# Retrieve only position history
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position"

# Retrieve position history limited to the 50 most recent entries
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&limit=50"

# Retrieve only new position entries since sequence ID 42
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=42"

# Retrieve state history (combined position + altitude + velocity)
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=State"
```

### Response Structure

Each history type in the response includes buffer metadata alongside the entries. Every entry contains a `SequenceId` field, and the buffer metadata includes `MinSequenceId` and `MaxSequenceId` for the current buffer range:

```json
{
  "ICAO": "407F19",
  "Timestamp": "2026-03-13T14:30:00Z",
  "Position": {
    "Enabled": true,
    "Capacity": 1000,
    "Count": 3,
    "MinSequenceId": 1,
    "MaxSequenceId": 3,
    "Entries": [
      {
        "SequenceId": 1,
        "Timestamp": "2026-03-13T14:29:50Z",
        "Position": { "Latitude": 47.3935, "Longitude": 18.5198 },
        "NACp": "< 3 m"
      },
      {
        "SequenceId": 2,
        "Timestamp": "2026-03-13T14:29:52Z",
        "Position": { "Latitude": 47.3955, "Longitude": 18.5218 },
        "NACp": "< 3 m"
      },
      {
        "SequenceId": 3,
        "Timestamp": "2026-03-13T14:29:55Z",
        "Position": { "Latitude": 47.3975, "Longitude": 18.5238 },
        "NACp": "< 3 m"
      }
    ]
  }
}
```

When history tracking is disabled for a particular type, the response indicates this with a minimal object: `{ "Enabled": false }`. The `Capacity`, `Count`, `MinSequenceId`, `MaxSequenceId`, and `Entries` fields are not present in this case.

When a buffer is enabled but empty, `MinSequenceId` and `MaxSequenceId` are `null`.

### Incremental Polling with `after`

The `after` parameter enables efficient incremental polling by returning only entries with a sequence ID greater than the specified value. The server-side filtering logic works as follows:

- **`after` ≥ `MaxSequenceId`** — no new data. Returns empty `Entries` array.
- **`after` < `MinSequenceId`** — client missed entries (buffer wrapped). Returns all entries currently in the buffer.
- **`MinSequenceId` ≤ `after` < `MaxSequenceId`** — returns only entries with `SequenceId` > `after`.

In all cases, `Count` reflects the total number of entries in the buffer, not the number of returned entries.

**Example: Incremental request returning only new entries**

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=2"
```

```json
{
  "ICAO": "407F19",
  "Timestamp": "2026-03-13T14:30:01Z",
  "Position": {
    "Enabled": true,
    "Capacity": 1000,
    "Count": 4,
    "MinSequenceId": 1,
    "MaxSequenceId": 4,
    "Entries": [
      {
        "SequenceId": 3,
        "Timestamp": "2026-03-13T14:29:55Z",
        "Position": { "Latitude": 47.3975, "Longitude": 18.5238 },
        "NACp": "< 3 m"
      },
      {
        "SequenceId": 4,
        "Timestamp": "2026-03-13T14:29:58Z",
        "Position": { "Latitude": 47.3995, "Longitude": 18.5258 },
        "NACp": "< 3 m"
      }
    ]
  }
}
```

**Example: No new data**

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=4"
```

```json
{
  "ICAO": "407F19",
  "Timestamp": "2026-03-13T14:30:02Z",
  "Position": {
    "Enabled": true,
    "Capacity": 1000,
    "Count": 4,
    "MinSequenceId": 1,
    "MaxSequenceId": 4,
    "Entries": []
  }
}
```

### Example: Velocity history with limit

The `limit` parameter restricts the response to the most recent N entries, which is useful for fetching just the latest data points without transferring the entire buffer:

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Velocity&limit=2"
```

```json
{
  "ICAO": "407F19",
  "Timestamp": "2026-03-13T14:30:00Z",
  "Velocity": {
    "Enabled": true,
    "Capacity": 1000,
    "Count": 185,
    "MinSequenceId": 1,
    "MaxSequenceId": 185,
    "Entries": [
      {
        "SequenceId": 184,
        "Timestamp": "2026-03-13T14:29:55Z",
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93 },
        "Heading": null,
        "Track": 294.59,
        "SpeedOnGround": null,
        "TrackOnGround": null,
        "VerticalRate": 0
      },
      {
        "SequenceId": 185,
        "Timestamp": "2026-03-13T14:29:50Z",
        "Speed": { "Type": "GroundSpeed", "Knots": 478, "KilometersPerHour": 885, "MilesPerHour": 550, "MetersPerSecond": 245.90 },
        "Heading": null,
        "Track": 294.61,
        "SpeedOnGround": null,
        "TrackOnGround": null,
        "VerticalRate": -64
      }
    ]
  }
}
```

### Example: State history

The State type provides combined snapshots of position, altitude, and velocity captured at each position update. This eliminates the need to merge separate history streams by timestamp when building correlated views like map trails with altitude and speed labels.

A State snapshot is recorded only when a position update is received. The `Position` field is always present; all other fields (`Altitude`, `Speed`, `Heading`, `Track`, etc.) are `null` until the aircraft has reported that data.

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=State"
```

```json
{
  "ICAO": "407F19",
  "Timestamp": "2026-03-13T14:30:00Z",
  "State": {
    "Enabled": true,
    "Capacity": 1000,
    "Count": 2,
    "MinSequenceId": 1,
    "MaxSequenceId": 2,
    "Entries": [
      {
        "SequenceId": 1,
        "Timestamp": "2026-03-13T14:29:50Z",
        "Position": { "Latitude": 47.3955, "Longitude": 18.5218 },
        "NACp": "< 3 m",
        "Altitude": { "Type": "Barometric", "Feet": 38000, "Meters": 11582, "FlightLevel": 380 },
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 888, "MilesPerHour": 552, "MetersPerSecond": 246.93 },
        "Heading": null,
        "Track": 294.59,
        "SpeedOnGround": null,
        "TrackOnGround": null,
        "VerticalRate": 0
      },
      {
        "SequenceId": 2,
        "Timestamp": "2026-03-13T14:29:55Z",
        "Position": { "Latitude": 47.3975, "Longitude": 18.5238 },
        "NACp": "< 3 m",
        "Altitude": { "Type": "Barometric", "Feet": 38000, "Meters": 11582, "FlightLevel": 380 },
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 888, "MilesPerHour": 552, "MetersPerSecond": 246.93 },
        "Heading": null,
        "Track": 294.59,
        "SpeedOnGround": null,
        "TrackOnGround": null,
        "VerticalRate": 0
      }
    ]
  }
}
```

## Statistics

The statistics endpoint returns information about the receiver's operational state, including how long the daemon has been running, how many aircraft are currently tracked, and detailed frame processing metrics. The frame statistics are useful for monitoring receiver health and diagnosing reception issues.

```bash
curl -s "http://localhost:8080/api/v1/stats"
```

```json
{
  "Version": "0.6.0",
  "Timestamp": "2026-03-13T14:30:00Z",
  "Uptime": 3600,
  "AircraftCount": 42,
  "Devices": 1,
  "Stream": {
    "TotalFrames": 125000,
    "ValidFrames": 98000,
    "CrcErrors": 27000,
    "FramesPerSecond": 34.7
  },
  "Receiver": {
    "Latitude": 46.907982,
    "Longitude": 19.693172,
    "AltitudeMeters": 120,
    "Name": "Kecskemét"
  }
}
```

The `Uptime` field is in seconds. The `Stream` section shows cumulative frame counts since the daemon started, along with the current frame processing rate. The `Receiver` section includes the station's geographic location as configured in the YAML file, which is useful for clients that need to calculate distances or render the receiver on a map.

## Health Check

The health check endpoint provides a lightweight status response designed for Docker health checks, monitoring tools, load balancers, and readiness probes. It returns minimal data to keep the response fast and the payload small.

```bash
curl -s "http://localhost:8080/api/v1/health"
```

```json
{
  "Status": "OK",
  "Uptime": 3600,
  "AircraftCount": 42,
  "Timestamp": "2026-03-13T14:30:00Z"
}
```

During the initial startup period, before the aircraft state tracker has been fully initialized, the endpoint returns HTTP 503 with `{ "Status": "Starting" }`. This allows orchestration tools to distinguish between a healthy running instance and one that is still initializing.

## Response Format

All API responses follow consistent serialization conventions:

- **Content-Type:** All responses are served as `application/json`.
- **Field names** use PascalCase throughout, matching the C# property names in the underlying data model.
- **Timestamps** are formatted as ISO 8601 strings in UTC (e.g., `"2026-03-13T14:30:00Z"`).
- **Null handling** follows an explicit inclusion policy: within any returned section, all fields are always present. Fields that have no data are set to `null` rather than being omitted, which allows clients to discover the full schema from any single response.
- **Altitude** is represented as a rich value object containing the altitude in multiple units simultaneously: `{ "Type": "Barometric", "Feet": 38000, "Meters": 11582, "FlightLevel": 380 }`. The `FlightLevel` field is `null` for geometric altitudes, which do not have a flight level equivalent.
- **Speed** is similarly represented with multiple units: `{ "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 888, "MilesPerHour": 552, "MetersPerSecond": 246.93 }`.
- **Enums** are serialized as human-readable strings rather than numeric values (e.g., `"No Emergency"`, `"DO-260B"`, `"Heavy"`), making the API output more readable without requiring clients to maintain enum lookup tables.

## Error Responses

All error responses use a consistent format with a single `Error` field containing a human-readable message, accompanied by the appropriate HTTP status code:

| Code | Meaning                                                                                                   |
|------|-----------------------------------------------------------------------------------------------------------|
| 200  | The request was successful and the response contains the requested data                                   |
| 400  | The request was malformed — invalid ICAO address format, unknown section name, or invalid parameter value |
| 404  | The specified aircraft was not found in the tracker, or the requested route does not exist                |
| 405  | The request used a method other than GET, which is the only supported HTTP method                         |
| 503  | The API is not yet ready because the aircraft state tracker has not finished initializing                 |

### Error Examples

The following examples demonstrate the error responses for common invalid requests:

```bash
# Invalid ICAO address (must be exactly 6 hexadecimal characters)
curl -s "http://localhost:8080/api/v1/aircraft/ZZZZZZ"
# {"Error":"Invalid ICAO address: ZZZZZZ"}  (HTTP 400)

# Aircraft not currently being tracked
curl -s "http://localhost:8080/api/v1/aircraft/AAAAAA"
# {"Error":"Aircraft not found: AAAAAA"}  (HTTP 404)

# Requesting a section name that does not exist
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=InvalidName"
# {"Error":"Unknown section: InvalidName"}  (HTTP 400)

# Requesting a history type that does not exist
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=InvalidType"
# {"Error":"Unknown history type: InvalidType"}  (HTTP 400)

# Providing a negative or non-numeric limit value
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?limit=-1"
# {"Error":"Invalid limit: -1. Must be a positive integer."}  (HTTP 400)

# Comma-separated types are no longer supported
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position,Altitude"
# {"Error":"Invalid type: Position,Altitude"}  (HTTP 400)

# Invalid after parameter (negative or non-numeric)
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=-1"
# {"Error":"Invalid after: -1"}  (HTTP 400)

curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=abc"
# {"Error":"Invalid after: abc"}  (HTTP 400)

# Using after without specifying a type
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?after=42"
# {"Error":"Parameter 'after' requires a type"}  (HTTP 400)
```

## Common Usage Patterns

The following examples demonstrate typical ways to use the API with `curl` and `jq` for common aircraft tracking tasks.

### Poll the aircraft list for a map display

```bash
# Poll every 1 second and extract the fields needed for map markers
while true; do
  curl -s "http://localhost:8080/api/v1/aircraft" | jq '.Aircraft[] | {ICAO, Coordinate, BarometricAltitude}'
  sleep 1
done
```

### Retrieve full detail for a specific aircraft

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19" | jq .
```

### Fetch only position and speed data to minimize response size

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Position,VelocityAndDynamics" | jq .
```

### Poll for new position entries using incremental `after` parameter

```bash
# First request — get all entries and store the MaxSequenceId
LAST_SEQ=$(curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position" | jq '.Position.MaxSequenceId')

# Subsequent requests — only fetch new entries
while true; do
  RESPONSE=$(curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=$LAST_SEQ")
  NEW_SEQ=$(echo "$RESPONSE" | jq '.Position.MaxSequenceId')
  if [ "$NEW_SEQ" != "$LAST_SEQ" ] && [ "$NEW_SEQ" != "null" ]; then
    echo "$RESPONSE" | jq '.Position.Entries[]'
    LAST_SEQ=$NEW_SEQ
  fi
  sleep 1
done
```

### Extract a position trail for rendering a flight path polyline on a map

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position" | jq '.Position.Entries[] | [.Position.Longitude, .Position.Latitude]'
```

### Retrieve the 10 most recent altitude readings for an altitude profile chart

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Altitude&limit=10" | jq '.Altitude.Entries[] | {Timestamp, Feet: .Altitude.Feet}'
```

### Check how many aircraft are currently being tracked

```bash
curl -s "http://localhost:8080/api/v1/aircraft" | jq .Count
```

### Verify that the receiver is running and healthy

```bash
curl -s "http://localhost:8080/api/v1/health" | jq .Status
```

### Monitor the frame processing rate and error ratio

```bash
curl -s "http://localhost:8080/api/v1/stats" | jq '{FPS: .Stream.FramesPerSecond, Errors: .Stream.CrcErrors, Total: .Stream.TotalFrames}'
```
