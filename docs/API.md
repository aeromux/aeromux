# REST API

Aeromux includes a read-only REST API that provides access to live aircraft tracking data. The API is designed for web interfaces, map visualizations, and third-party integrations.

The API is HTTP-only (no TLS) — intended for local network use or behind a reverse proxy.

## Configuration

The API is enabled by default on port 8080. Configure it in `aeromux.yaml`:

```yaml
network:
  apiPort: 8080
  apiEnabled: true
```

Or via CLI options:

```bash
aeromux daemon --api-port 8080 --api-enabled true
aeromux daemon --api-enabled false  # disable the API
```

## Endpoints

| Endpoint                              | Description                          |
|---------------------------------------|--------------------------------------|
| `GET /api/v1/aircraft`                | List all tracked aircraft            |
| `GET /api/v1/aircraft/{icao}`         | Aircraft detail (10 sections)        |
| `GET /api/v1/aircraft/{icao}/history` | Position, altitude, velocity history |
| `GET /api/v1/stats`                   | Receiver and session statistics      |
| `GET /api/v1/health`                  | Health check for monitoring          |

## Aircraft List

Returns a compact summary of all tracked aircraft, suitable for table or map rendering.

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

## Aircraft Detail

Returns detailed information for a single aircraft. The ICAO address is case-insensitive.

```bash
# All sections
curl -s "http://localhost:8080/api/v1/aircraft/407F19"

# Specific sections only
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Position,Autopilot"

# Single section
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Identification"
```

### Available Sections

| Section                | Description                                       |
|------------------------|---------------------------------------------------|
| `Identification`       | Identity and transponder state                    |
| `DatabaseRecord`       | Static aircraft metadata from database            |
| `Status`               | Timing, message counts, and signal                |
| `Position`             | Coordinates, altitude, and ground state           |
| `VelocityAndDynamics`  | Speed, direction, vertical rates, dynamics        |
| `Autopilot`            | Autopilot targets and modes                       |
| `Meteorology`          | Wind, temperature, pressure, hazards              |
| `Acas`                 | TCAS system state and resolution advisories       |
| `Capabilities`         | Equipment, features, and operational state        |
| `DataQuality`          | Accuracy, integrity, and antenna from all sources |

When `sections` is omitted, all sections are returned. When specified, only the requested sections appear — unrequested sections are absent entirely. The `Timestamp` field is always present.

Sections that have no data yet (e.g., `Meteorology` for a newly tracked aircraft) are returned as `null`.

### Example: Identification

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
    "LastUpdate": "2026-03-13T14:29:59Z"
  }
}
```

### Example: DatabaseRecord

When database enrichment is enabled and the aircraft is found:

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

When database enrichment is disabled, `DatabaseRecord` is `null`:

```json
{
  "Timestamp": "2026-03-13T14:30:00Z",
  "DatabaseRecord": null
}
```

### Example: Null section (no data yet)

Sections for which no messages have been received are returned as `null`:

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

Returns position, altitude, and velocity history for a single aircraft.

```bash
# All history types
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history"

# Position only, last 50 entries
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&limit=50"

# Multiple types
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position,Altitude"

# Altitude history only
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Altitude"
```

Each history type includes buffer metadata:

```json
{
  "ICAO": "407F19",
  "Timestamp": "2026-03-13T14:30:00Z",
  "Position": {
    "Enabled": true,
    "Capacity": 1000,
    "Count": 247,
    "Entries": [
      {
        "Timestamp": "2026-03-13T14:29:50Z",
        "Position": { "Latitude": 47.3955, "Longitude": 18.5217 },
        "NACp": "< 3 m"
      }
    ]
  }
}
```

When a history type is disabled: `{ "Enabled": false }` (no `Capacity`, `Count`, or `Entries`).

### Example: Velocity history with limit

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
    "Entries": [
      {
        "Timestamp": "2026-03-13T14:29:55Z",
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93 },
        "Heading": null,
        "Track": 294.59,
        "SpeedOnGround": null,
        "TrackOnGround": null,
        "VerticalRate": 0
      },
      {
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

## Statistics

Returns receiver and session statistics.

```bash
curl -s "http://localhost:8080/api/v1/stats"
```

```json
{
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

## Health Check

Lightweight endpoint for Docker health checks, monitoring tools, and readiness probes.

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

Returns HTTP 503 with `{ "Status": "Starting" }` if the tracker is not yet initialized.

## Response Format

- **Content-Type:** `application/json`
- **Field names:** PascalCase
- **Timestamps:** ISO 8601 UTC (e.g., `"2026-03-13T14:30:00Z"`)
- **Null handling:** Within returned sections, all fields are always present. Fields with no data are `null` (never omitted).
- **Rich value objects:** Altitude and speed are returned with multiple units:
  - Altitude: `{ "Type": "Barometric", "Feet": 38000, "Meters": 11582, "FlightLevel": 380 }`
  - Speed: `{ "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 888, "MilesPerHour": 552, "MetersPerSecond": 246.93 }`
- **Enums:** Serialized as human-readable strings (e.g., `"No Emergency"`, `"DO-260B"`, `"Heavy"`)

## Error Responses

All errors use the format `{ "Error": "<message>" }` with the appropriate HTTP status code.

| Code | Meaning                                                |
|------|--------------------------------------------------------|
| 200  | Success                                                |
| 400  | Bad request (invalid ICAO, section name, or parameter) |
| 404  | Aircraft not found or unknown route                    |
| 405  | Method not allowed (only GET is supported)             |
| 429  | Too many requests (rate limit exceeded)                |
| 503  | API not ready (tracker not yet initialized)            |

### Error Examples

```bash
# Invalid ICAO address
curl -s "http://localhost:8080/api/v1/aircraft/ZZZZZZ"
# {"Error":"Invalid ICAO address: ZZZZZZ"}  (HTTP 400)

# Aircraft not found
curl -s "http://localhost:8080/api/v1/aircraft/AAAAAA"
# {"Error":"Aircraft not found: AAAAAA"}  (HTTP 404)

# Invalid section name
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=InvalidName"
# {"Error":"Unknown section: InvalidName"}  (HTTP 400)

# Invalid history type
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=InvalidType"
# {"Error":"Unknown history type: InvalidType"}  (HTTP 400)

# Invalid limit
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?limit=-1"
# {"Error":"Invalid limit: -1. Must be a positive integer."}  (HTTP 400)
```

## Rate Limiting

Aircraft endpoints are rate-limited to 1 request per 500ms per client IP to prevent aggressive polling:

- **Aircraft list** (`/api/v1/aircraft`): 1 request per 500ms per client IP.
- **Aircraft detail and history** (`/api/v1/aircraft/{icao}`, `/api/v1/aircraft/{icao}/history`): 1 request per 500ms per client IP per ICAO. Different aircraft have independent limits.
- **Stats and health** endpoints are not rate-limited.

When rate-limited, the server returns HTTP 429 with a `Retry-After` header.

## Common Usage Patterns

### Poll aircraft list for a map

```bash
# Poll every 1 second for map markers
while true; do
  curl -s "http://localhost:8080/api/v1/aircraft" | jq '.Aircraft[] | {ICAO, Coordinate, BarometricAltitude}'
  sleep 1
done
```

### Get detail for a specific aircraft

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19" | jq .
```

### Fetch only position and speed

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19?sections=Position,VelocityAndDynamics" | jq .
```

### Fetch position trail for a map polyline

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position" | jq '.Position.Entries[] | [.Position.Longitude, .Position.Latitude]'
```

### Get last 10 altitude readings

```bash
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Altitude&limit=10" | jq '.Altitude.Entries[] | {Timestamp, Feet: .Altitude.Feet}'
```

### Count tracked aircraft

```bash
curl -s "http://localhost:8080/api/v1/aircraft" | jq .Count
```

### Monitor receiver health

```bash
curl -s "http://localhost:8080/api/v1/health" | jq .Status
```

### Check frame rate and error ratio

```bash
curl -s "http://localhost:8080/api/v1/stats" | jq '{FPS: .Stream.FramesPerSecond, Errors: .Stream.CrcErrors, Total: .Stream.TotalFrames}'
```
