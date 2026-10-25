# REST API

Aeromux includes a read-only REST API that provides programmatic access to live aircraft tracking data. The API is designed for building web interfaces, rendering map visualizations, and integrating with third-party tools that need structured, on-demand access to the current tracking state.

All API responses are served over plain HTTP without TLS encryption. This is intentional: the API is intended for use on local networks or behind a reverse proxy that handles TLS termination. If you need encrypted access from external clients, place a reverse proxy such as nginx or Caddy in front of Aeromux.

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

When the API is disabled, the HTTP server is not started and the port is not opened. The same server also hosts the Web Map and its live-update connection, so disabling the API disables the Web Map as well.

### Browser access from other origins (CORS)

Browsers stop a web page from reading responses from another origin (scheme, host and port) unless the server allows it. The Web Map is served by the daemon itself and is not affected, and neither are clients that are not browsers, such as curl, scripts, Home Assistant or Node-RED. It matters only for a dashboard served from somewhere else that calls the API from the browser.

To allow such a dashboard, list its origin in `apiCorsOrigins`:

```yaml
network:
  apiCorsOrigins:
    - http://dashboard.local:3000
    - https://grafana.example.net
```

```bash
aeromux daemon --api-cors-origin http://dashboard.local:3000 --api-cors-origin https://grafana.example.net
```

`--api-cors-origin` can be repeated and, like `--beast-source`, replaces the list from the YAML file as a whole. The list is empty by default, which allows no other origin and leaves every response unchanged.

- Each entry is an `http` or `https` origin with a host and an optional port, and nothing else: no path, query or fragment. A trailing `/` is removed. Anything else stops the daemon at startup with `Invalid CORS origin 'X': expected http(s)://host[:port] or *`.
- `*` allows every origin. The daemon starts with a warning in the log, because any web page opened in a browser that can reach the daemon could then read its API, including the configuration at `/api/v1/config`.
- CORS applies to paths under `/api` only, for `GET` and its preflight `OPTIONS` request, which is answered with `204`. No credentials are allowed. An allowed origin receives `Access-Control-Allow-Origin`; any other origin receives the same response without it, and the browser blocks the read.
- The live-update connection at `/maphub` has no CORS policy: it serves the bundled Web Map only.

## Endpoints

The API exposes seven endpoints, all under the `/api/v1/` prefix. All endpoints accept only `GET` requests:

| Endpoint                              | Description                                                                         |
|---------------------------------------|-------------------------------------------------------------------------------------|
| `GET /api/v1/aircraft`                | Returns a compact list of all currently tracked aircraft                            |
| `GET /api/v1/aircraft/{icao}`         | Returns detailed information for a single aircraft, organized into 10 sections      |
| `GET /api/v1/aircraft/{icao}/history` | Returns position, altitude, velocity, and state history for a single aircraft       |
| `GET /api/v1/aircraft/{icao}/photo`   | Returns photo metadata sourced from Planespotters.net (thumbnail URL + attribution) |
| `GET /api/v1/config`                  | Returns the configuration the daemon runs with, optionally with each value's source |
| `GET /api/v1/stats`                   | Returns measurements per input and output: frame counts, rates and connection state |
| `GET /api/v1/health`                  | Reports whether the inputs are delivering, for Docker, monitoring tools and probes  |

Each query parameter may appear at most once in a request. Repeating one, as in `?type=Position&type=State`, returns HTTP 400 rather than silently using the first value.

## Aircraft List

The aircraft list endpoint returns a compact summary of all currently tracked aircraft in a single response. Each aircraft entry includes the most commonly needed fields (identification, position, speed, altitude, and signal quality), making it suitable for rendering aircraft tables or map markers without requiring individual detail queries.

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
      "BarometricAltitude": { "Type": "Barometric", "Feet": 36000, "Meters": 10973, "FlightLevel": 360 },
      "GeometricAltitude": null,
      "IsOnGround": false,
      "Speed": { "Type": "GroundSpeed", "Knots": 450, "KilometersPerHour": 833, "MilesPerHour": 518, "MetersPerSecond": 231.4998 },
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
      "TypeIcaoClass": "L2J",
      "TypeWtc": "M",
      "OperatorName": "Lufthansa",
      "Military": false,
      "Ladd": false,
      "Pia": false
    }
  ]
}
```

The response includes a `Count` field for convenience, a `Timestamp` indicating when the response was generated, and an `Aircraft` array containing one entry per tracked aircraft. Aircraft that have timed out (no messages received within the configured timeout period) are automatically removed from the list.

### Query Parameters

The aircraft list endpoint supports optional query parameters for filtering results:

| Parameter | Type   | Default | Description                                                                   |
|-----------|--------|---------|-------------------------------------------------------------------------------|
| `bounds`  | string | (none)  | Geographic bounding box: `<south>,<west>,<north>,<east>`                      |
| `search`  | string | (none)  | Case-insensitive substring search across callsign, ICAO, squawk, registration |

The `bounds` and `search` parameters are mutually exclusive. Providing both returns HTTP 400.

**Bounds filtering** returns only aircraft with a known position within the specified bounding box, edges included. Aircraft without a decoded position are excluded. All four values must be finite numbers, latitudes between -90 and 90, longitudes between -180 and 180, and south must be less than north.

A `west` value greater than `east` describes a box that crosses the 180° meridian: it runs east from `west` to 180° and continues from -180° to `east`. Map views over the Pacific produce such boxes, and the bundled Web Map sends them whenever its view crosses the meridian.

```bash
# Aircraft within a geographic bounding box
curl -s "http://localhost:8080/api/v1/aircraft?bounds=46.5,18.0,48.0,20.5"

# A box across the 180° meridian, from 170° E to 170° W
curl -s "http://localhost:8080/api/v1/aircraft?bounds=-50,170,-30,-170"
```

**Search filtering** performs a case-insensitive substring match against ICAO address, callsign, squawk code, and registration (when the aircraft database is enabled). Search queries all tracked aircraft regardless of position, so aircraft without a known position are included in search results. A value that is empty or only spaces matches every aircraft, the same as omitting the parameter.

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

# Longitude outside -180..180
curl -s "http://localhost:8080/api/v1/aircraft?bounds=46.5,18.0,48.0,190"
# → 400: {"Error": "Invalid bounds: longitude must be between -180 and 180"}

# A value that is not a finite number
curl -s "http://localhost:8080/api/v1/aircraft?bounds=NaN,18.0,48.0,20.5"
# → 400: {"Error": "Invalid bounds values: all values must be finite numbers"}

# Both parameters provided
curl -s "http://localhost:8080/api/v1/aircraft?bounds=46.5,18.0,48.0,20.5&search=BAW"
# → 400: {"Error": "Parameters 'bounds' and 'search' are mutually exclusive"}
```

## Aircraft Detail

The aircraft detail endpoint returns comprehensive information for a single aircraft, organized into logical sections. The ICAO address in the URL is case-insensitive: `407f19` and `407F19` resolve to the same aircraft.

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

When the `sections` parameter is omitted, all 10 sections are included in the response. When specified, only the requested sections appear; unrequested sections are absent entirely from the response rather than being set to `null`. An empty value, or a list with no names in it such as `sections=,`, is treated as if the parameter were omitted. The `Timestamp` field is always present regardless of which sections are requested.

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
    "GeometricAltitude": { "Type": "Geometric", "Feet": 37725, "Meters": 11499, "FlightLevel": 377 },
    "GeometricBarometricDelta": -275,
    "IsOnGround": false,
    "MovementCategory": null,
    "Source": "SDR",
    "HadMlatPosition": false,
    "LastUpdate": "2026-03-13T14:29:59Z"
  },
  "VelocityAndDynamics": {
    "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93312 },
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
    "VerticalRateSource": "Barometric",
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

In the Position section, `LastUpdate` is when a position was last received and accepted, whether or not the aircraft moved. A position message that decoded no coordinate, or whose coordinate was rejected as implausible, leaves it unchanged, and so does an altitude update.

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
    "TypeWtc": "H",
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

Each entry in a history buffer is assigned a **sequence ID**: a monotonically increasing `long` starting at 1, scoped per aircraft per buffer type. Sequence IDs enable efficient incremental polling via the `after` query parameter, so clients only receive entries they have not yet seen.

### Query Parameters

| Parameter | Type   | Default | Description                                                         |
|-----------|--------|---------|---------------------------------------------------------------------|
| `type`    | string | (all)   | Single history type: `Position`, `Altitude`, `Velocity`, or `State` |
| `limit`   | int    | (all)   | Return only the N most recent entries (at most 10,000)              |
| `after`   | long   | (none)  | Return only entries with sequence ID greater than this value        |

- `type` accepts a **single value only**. When omitted, all four types are returned.
- `after` **requires** `type`; using `after` without `type` returns HTTP 400.
- A `limit` above 10,000 is treated as 10,000.
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

Each history type in the response includes buffer metadata alongside the entries. Every entry contains a `SequenceId` field, and the buffer metadata includes `MinSequenceId` and `MaxSequenceId` for the current buffer range. The metadata and the entries are read from the buffer at the same instant, so they always describe the same contents:

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

When history tracking is disabled for a particular type, `Enabled` is `false` and every other field is `null`:

```json
{
  "Enabled": false,
  "Capacity": null,
  "Count": null,
  "MinSequenceId": null,
  "MaxSequenceId": null,
  "Entries": null
}
```

When a buffer is enabled but empty, `MinSequenceId` and `MaxSequenceId` are `null`.

### Incremental Polling with `after`

The `after` parameter enables efficient incremental polling by returning only entries with a sequence ID greater than the specified value. The server-side filtering logic works as follows:

- **`after` ≥ `MaxSequenceId`**: no new data. Returns an empty `Entries` array.
- **`after` < `MinSequenceId`**: the client missed entries (the buffer wrapped). Returns all entries currently in the buffer.
- **`MinSequenceId` ≤ `after` < `MaxSequenceId`**: returns only entries with `SequenceId` > `after`.

In all cases, `Count` reflects the total number of entries in the buffer, not the number of returned entries.

Use the `SequenceId` of the last entry received as the next `after` value, and keep the previous value when a response has no entries.

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
        "Timestamp": "2026-03-13T14:29:50Z",
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93312 },
        "Heading": null,
        "Track": 294.59,
        "SpeedOnGround": null,
        "TrackOnGround": null,
        "VerticalRate": 0
      },
      {
        "SequenceId": 185,
        "Timestamp": "2026-03-13T14:29:55Z",
        "Speed": { "Type": "GroundSpeed", "Knots": 478, "KilometersPerHour": 885, "MilesPerHour": 550, "MetersPerSecond": 245.904232 },
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
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93312 },
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
        "Speed": { "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93312 },
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

## Aircraft Photo

The aircraft photo endpoint returns metadata for a representative photo of the airframe sourced from [Planespotters.net](https://www.planespotters.net/) via their public `/pub/` API. Aeromux acts as a metadata cache only: it queries Planespotters on cache miss, caches the result in memory, and returns the metadata to the client. The browser then fetches the thumbnail JPEG directly from Planespotters' CDN. Aeromux is not in the byte path.

Photos are looked up only for aircraft the daemon is currently tracking. Any other ICAO address returns `404 {"Error":"Aircraft not found: XXXXXX"}` without contacting Planespotters.

```bash
curl -s "http://localhost:8080/api/v1/aircraft/4CA87C/photo"
```

```json
{
  "HasPhoto": true,
  "ThumbnailUrl": "https://t.plnspttrs.net/03699/1234567_abc_280.jpg",
  "Photographer": "Jane Doe",
  "Link": "https://www.planespotters.net/photo/1234567/foo?utm_source=api"
}
```

When no photo is available (Planespotters returned an empty result, or a 404 or 410), `HasPhoto` is `false` and the other fields are `null`:

```json
{
  "HasPhoto": false,
  "ThumbnailUrl": null,
  "Photographer": null,
  "Link": null
}
```

### Lookup behavior

The endpoint first looks up the photo by 24-bit ICAO hex address. If that returns no photo and the local aircraft database is enabled and has a registration for the ICAO, a second lookup is attempted by registration. Both terminal results (positive and "no photo") are cached in memory until the aircraft expires from the tracker or 12 hours pass, whichever comes first, with a 1000-entry LRU safety cap.

Transient upstream failures (HTTP 429, 5xx, network error, timeout) are **not** cached. The endpoint returns `502 Bad Gateway` and the next request retries from scratch.

### Image bytes

The browser is responsible for fetching the thumbnail JPEG. Set the `<img>` tag's `src` attribute to the returned `ThumbnailUrl` and let the browser/CDN handle the bytes and HTTP cache:

```html
<img src="https://t.plnspttrs.net/03699/1234567_abc_280.jpg" alt="Aircraft photo by Jane Doe">
```

### Planespotters usage rules

Photo metadata returned by this endpoint originates from Planespotters' public photo API and is governed by the [Planespotters Photo API Terms of Use](https://www.planespotters.net/photo/api). Aeromux is designed to satisfy those rules out of the box; clients consuming this endpoint MUST preserve that compliance:

- **Attribution is mandatory.** Render the `Photographer` value as visible text next to the image, and the `Link` value as a clickable anchor pointing to the photo's page on planespotters.net. The link must be a plain anchor without `rel="nofollow"` or equivalent. Aeromux's bundled web map uses `rel="noopener"` (security only) and `target="_blank"`.
- **Use URLs unchanged.** `ThumbnailUrl` and `Link` must be passed through verbatim. Do not proxy, rewrite, or hot-link-protection-bypass them. The image must be loaded by the end user's browser directly from `ThumbnailUrl`.
- **Do not download or re-host the image bytes.** Aeromux only caches the JSON metadata; the binary JPEG never touches the daemon. Clients must do the same.
- **Metadata caching ≤ 24 h.** Aeromux never serves a cached entry more than 12 hours old, and drops entries sooner when the aircraft expires from the tracker. Downstream clients that add their own cache must respect the same upper bound.
- **No re-exposure as a separate dataset.** This endpoint exists to render photos in the bundled Aeromux web map for the end user operating the daemon. It is not a redistribution feed: building a public proxy, bulk export, or third-party API on top of it is not permitted by Planespotters' terms.
- **No ML/AI training.** The returned metadata and the images it points to must not be used to train, fine-tune, evaluate, or build datasets for machine-learning models.

Upstream, the daemon identifies itself to Planespotters with a descriptive `User-Agent` (`Aeromux/{version} (+https://github.com/aeromux/aeromux)`) as required for server-side clients. No API key is needed.

## Effective Configuration

The configuration endpoint returns the configuration the daemon is running with, after the built-in defaults, the YAML file and the command-line options are combined and validated. It answers the most common support question, "why is my setting not applied?", without reading log files.

```bash
curl -s "http://localhost:8080/api/v1/config"
```

```json
{
  "Version": "0.9.0",
  "Timestamp": "2026-10-25T14:30:00Z",
  "ConfigFile": "/etc/aeromux/aeromux.yaml",
  "Logging": {
    "Level": "Information",
    "Console": { "Enabled": false, "Colored": true },
    "File": {
      "Enabled": true,
      "Path": "logs/aeromux-.log",
      "RollingInterval": "Day",
      "RetainedFileCount": 7,
      "FileSizeLimitMb": 100
    }
  },
  "SdrSources": [
    {
      "Name": "default",
      "DeviceIndex": 0,
      "GainMode": "Manual",
      "TunerGain": 49.6,
      "PpmCorrection": 0,
      "Enabled": true,
      "PreambleThreshold": 1.8125,
      "DeduplicationWindow": 50,
      "MaxTrackedFrames": 1000
    }
  ],
  "BeastSources": [],
  "Network": {
    "BeastOutputPort": 30005,
    "BeastOutputEnabled": true,
    "JsonOutputPort": 30006,
    "JsonOutputEnabled": false,
    "SbsOutputPort": 30003,
    "SbsOutputEnabled": false,
    "ApiPort": 8080,
    "ApiEnabled": true,
    "ApiCorsOrigins": [],
    "BindAddress": "0.0.0.0"
  },
  "Tracking": {
    "ConfidenceLevel": "Medium",
    "IcaoTimeoutSeconds": 30,
    "AircraftTimeoutSeconds": 60,
    "EnablePositionHistory": true,
    "EnableAltitudeHistory": true,
    "EnableVelocityHistory": true,
    "EnableStateHistory": true,
    "MaxHistorySize": 1000
  },
  "Receiver": {
    "Latitude": 46.907982,
    "Longitude": 19.693172,
    "Altitude": 120,
    "Name": "Kecskemét",
    "ReceiverUuid": "(redacted)"
  },
  "Database": {
    "Enabled": true,
    "Path": "/var/lib/aeromux/db",
    "AutoUpdate": {
      "Enabled": true,
      "CheckOnStartup": true,
      "CheckIntervalHours": 24,
      "PruneOldDatabases": true
    }
  },
  "Mlat": { "Enabled": true, "InputPort": 30104 },
  "Heatmap": { "Collect": true }
}
```

- The structure follows `aeromux.yaml`, with the YAML keys in PascalCase, so each field maps back to the line that sets it. Every section and every field is always present: a receiver that is not configured is a `Receiver` section of `null` values.
- `ConfigFile` is the absolute path of the YAML file that was loaded, or `null` when none was (no `--config`, and no `aeromux.yaml` in the working directory).
- The values are the ones in effect. A command-line option shows its value, not the one in the file, and `Database.AutoUpdate` shows the settings after their defaults and limits are applied. `SdrSources` lists every configured device, enabled or not; which devices are actually running is reported by the statistics endpoint.
- `Receiver.ReceiverUuid` reads `"(redacted)"` when a UUID is set and `null` when not. MLAT aggregators identify a feeder by this UUID, so publishing it would let anyone who can reach the API feed timing data under the operator's identity. Nothing else is withheld.

### Where each value came from

With `source=true`, every configuration value becomes an object that names the layer it came from. `Version`, `Timestamp` and `ConfigFile` stay as they are.

```bash
curl -s "http://localhost:8080/api/v1/config?source=true" | jq .Network
```

```json
{
  "BeastOutputPort":    { "Value": 30005,       "Source": "Default" },
  "BeastOutputEnabled": { "Value": true,        "Source": "Default" },
  "JsonOutputPort":     { "Value": 30006,       "Source": "Default" },
  "JsonOutputEnabled":  { "Value": false,       "Source": "Default" },
  "SbsOutputPort":      { "Value": 30003,       "Source": "Default" },
  "SbsOutputEnabled":   { "Value": false,       "Source": "Default" },
  "ApiPort":            { "Value": 8081,        "Source": "Yaml" },
  "ApiEnabled":         { "Value": true,        "Source": "Default" },
  "ApiCorsOrigins":     { "Value": [],          "Source": "Default" },
  "BindAddress":        { "Value": "127.0.0.1", "Source": "Cli" }
}
```

| `Source`  | Meaning                                                                                                                                   |
|-----------|-------------------------------------------------------------------------------------------------------------------------------------------|
| `Cli`     | A command-line option set this value, or replaced the list it belongs to (`--beast-source` replaces all of `BeastSources`)                |
| `Yaml`    | The key is written in the loaded YAML file, even if its value is the same as the default                                                  |
| `Default` | Neither: the built-in default                                                                                                             |

`Value` keeps its JSON type: a port is still a number and a flag still a boolean. In lists such as `SdrSources`, each field is judged on its own, so a device that sets only `name` and `tunerGain` reports `Yaml` for those two and `Default` for the rest. How the layers combine is described under Configuration Priority in [CLI.md](CLI.md).

`source` accepts `true` and `false` in any letter case. `false`, an empty value and leaving the parameter out all return the plain form. Any other value returns HTTP 400: `{"Error":"Invalid source: yes. Must be true or false."}`.

## Statistics

The statistics endpoint reports measurements only: how many frames each input delivers, how they fare at each stage of processing, which sources are connected, and how many clients each output serves. The configuration they run under is reported by the configuration endpoint.

```bash
curl -s "http://localhost:8080/api/v1/stats"
```

```json
{
  "Version": "0.9.0",
  "Timestamp": "2026-10-25T14:30:00Z",
  "Uptime": 3600,
  "AircraftCount": 42,
  "Frames": {
    "Total": 412400,
    "PerSecond": 112.4,
    "AveragePerSecond": 114.6
  },
  "Sdr": {
    "Extracted": 1250000,
    "CrcErrors": 790000,
    "Corrected": 12000,
    "Unconfirmed": 61000,
    "Accepted": 399000,
    "AcceptedPerSecond": 109.8,
    "Devices": [
      {
        "Name": "default",
        "DeviceIndex": 0,
        "SamplesPerSecond": 2400000,
        "Extracted": 1250000,
        "CrcErrors": 790000,
        "Corrected": 12000,
        "Duplicates": 300,
        "Messages": 398700,
        "ExtractedPerSecond": 346.1
      }
    ]
  },
  "BeastSources": [
    {
      "Host": "192.168.1.20",
      "Port": 30005,
      "Connected": true,
      "Since": "2026-10-25T13:30:04Z",
      "Reconnects": 0,
      "Received": 12000,
      "Unconfirmed": 900,
      "Accepted": 11100,
      "AcceptedPerSecond": 3.1
    }
  ],
  "Mlat": {
    "Clients": 1,
    "Received": 2600,
    "ReceivedPerSecond": 0.7
  },
  "Outputs": {
    "Beast": { "Clients": 2 },
    "Json": null,
    "Sbs": null
  },
  "Database": { "Version": "2026.10.20" }
}
```

| Field                                     | Meaning                                                                                                                                    |
|-------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| `Uptime`                                  | Seconds since the daemon started, the same span the session summary reports when it stops                                                  |
| `Frames.Total`                            | Frames handed to the aircraft tracker from every source (SDR, Beast and MLAT) since start                                                  |
| `Frames.PerSecond`                        | The same over the last 60 seconds                                                                                                          |
| `Frames.AveragePerSecond`                 | `Total` divided by `Uptime`                                                                                                                |
| `Sdr`                                     | `null` when no SDR device runs                                                                                                             |
| `Sdr.Extracted`                           | Frames the preamble detector found, all devices                                                                                            |
| `Sdr.CrcErrors`                           | Extracted frames that failed the CRC check and could not be corrected                                                                      |
| `Sdr.Corrected`                           | Frames that passed the CRC check only after a single-bit correction                                                                        |
| `Sdr.Unconfirmed`                         | Frames that passed the CRC check but came from an aircraft address not yet confirmed (noise, or an aircraft heard for the first time)      |
| `Sdr.Accepted`                            | Frames passed on for decoding, including frames without an ICAO aircraft address (TIS-B and ADS-R track numbers, DF 19)                    |
| `Sdr.AcceptedPerSecond`                   | `Accepted` over the last 60 seconds                                                                                                        |
| `Sdr.Devices[]`                           | One entry per running device, in configuration order                                                                                       |
| `…SamplesPerSecond`                       | IQ samples received over the last 60 seconds: close to the sample rate when the device is healthy, `0` when it has stopped delivering      |
| `…Extracted`, `…CrcErrors`, `…Corrected`  | As above, for this device                                                                                                                  |
| `…Duplicates`                             | Frames this device's duplicate filter dropped                                                                                              |
| `…Messages`                               | Messages this device decoded                                                                                                               |
| `…ExtractedPerSecond`                     | `Extracted` over the last 60 seconds                                                                                                       |
| `BeastSources[]`                          | One entry per Beast input, `[]` when there are none                                                                                        |
| `…Connected`, `…Since`                    | Whether the source is connected, and since when; `Since` is `null` while the first connection attempt is still under way                   |
| `…Reconnects`                             | Successful connections after the first                                                                                                     |
| `…Received`, `…Unconfirmed`, `…Accepted`  | Frames decoded from the source; those dropped because their address is not yet confirmed; those passed on                                  |
| `…AcceptedPerSecond`                      | `Accepted` over the last 60 seconds                                                                                                        |
| `Mlat`                                    | `null` when MLAT input is disabled                                                                                                         |
| `Mlat.Clients`                            | Connected mlat-client instances                                                                                                            |
| `Mlat.Received`, `Mlat.ReceivedPerSecond` | MLAT frames received, in total and over the last 60 seconds                                                                                |
| `Outputs.Beast`, `.Json`, `.Sbs`          | `{ "Clients": n }`, or `null` when that output is disabled                                                                                 |
| `Database`                                | `null` when database enrichment is off; `Version` is the loaded database, which changes when the auto-updater installs a new one, and is `null` until one is loaded |

Counters are cumulative since their source started. For one SDR frame, the stages are: extracted by the preamble detector, CRC check (with single-bit correction), confirmation of the aircraft address, duplicate filter, decoder. Every extracted frame either fails the CRC check or passes it, and every frame that passes is either unconfirmed or accepted, so `Extracted = CrcErrors + Unconfirmed + Accepted`, and for a Beast source `Received = Unconfirmed + Accepted`, up to the few frames being processed at the moment of reading. The device totals add up to the `Sdr` totals.

`Unconfirmed` and `Accepted` exist only for the SDR devices as a whole, because all devices and the MLAT input share one record of confirmed addresses: an aircraft confirmed through one is confirmed for all.

**Rates.** Every `PerSecond` field covers the last 60 seconds, sampled once a second, so a monitoring tool polling every 30 to 60 seconds sees the period it polled across. During the first minute the rates cover the time since the daemon started. `Frames.AveragePerSecond` is the average over the whole session.

**Judging reception.** The share of `CrcErrors` in `Extracted` says how much of what the receiver picks up is too damaged to use, which is what to watch when adjusting the antenna or the gain. Most of these are noise and weak or overlapping signals, so a high share is normal; a change in it after an adjustment is what matters.

### Migrating from 0.8

The statistics endpoint changed shape in 0.9.0. The fields it no longer has moved as follows:

| Old field                                   | New location                                                                                                     |
|---------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| `Receiver.Latitude`, `.Longitude`, `.Name`  | `/api/v1/config` `Receiver.*`                                                                                    |
| `Receiver.AltitudeMeters`                   | `/api/v1/config` `Receiver.Altitude`                                                                             |
| `HeatmapCollectionEnabled`                  | `/api/v1/config` `Heatmap.Collect`                                                                               |
| `Devices`                                   | The length of `Sdr.Devices`, `0` when `Sdr` is `null`                                                            |
| `Stream.TotalFrames`                        | `Sdr.Extracted`                                                                                                  |
| `Stream.ValidFrames`                        | `Sdr.Accepted`                                                                                                   |
| `Stream.CrcErrors`                          | `Sdr.CrcErrors`, now only frames that failed the CRC check; with `Sdr.Unconfirmed` it gives about the old figure |
| `Stream.FramesPerSecond`                    | `Sdr.AcceptedPerSecond`, now over the last 60 seconds instead of the whole session                               |

A daemon without an SDR device, receiving only Beast input, used to report zeros. It now reports `Sdr` as `null`, its Beast sources and MLAT input with their own figures, and `Frames.Total` from every source.

## Health Check

The health check endpoint reports whether the daemon's inputs are delivering. It is meant for Docker health checks, monitoring tools, load balancers and readiness probes.

```bash
curl -s "http://localhost:8080/api/v1/health"
```

```json
{
  "Status": "Degraded",
  "Uptime": 3600,
  "AircraftCount": 42,
  "Timestamp": "2026-10-25T14:30:00Z",
  "Problems": [
    "Beast source 192.168.1.20:30005 has been disconnected for 45 s"
  ]
}
```

An input source is **failing** when, for at least 10 seconds:

| Source       | Failing when                     |
|--------------|----------------------------------|
| SDR device   | It has delivered no IQ samples   |
| Beast source | It has not been connected        |

Samples, not frames, decide for an SDR device: a receiver with no aircraft in range, at night or in a quiet area, still delivers samples and is working. A Beast source that is connected but silent is treated the same way. MLAT input is not judged, because mlat-client is optional and reconnects on its own.

| `Status`    | When                                              | HTTP |
|-------------|---------------------------------------------------|------|
| `OK`        | No source is failing                              | 200  |
| `Degraded`  | Some sources are failing, at least one is not     | 200  |
| `Unhealthy` | Every input source is failing                     | 503  |

`Degraded` is answered with 200 so that an orchestrator does not restart a daemon that is still receiving through its other inputs; `Problems` says what is wrong. `Unhealthy` is answered with 503, so `curl --fail`, Docker `HEALTHCHECK` and uptime monitors react without reading the body. `Problems` lists one message per failing source, such as `SDR device 'default' (index 0) has delivered no samples for 15 s`, and is `[]` when the status is `OK`.

The 10 seconds cover the first Beast connection attempt and a brief USB interruption, and are short enough that a monitor polling every 30 seconds sees a failure on its next poll. A daemon that has been running for less than 10 seconds therefore reports `OK`. One that is still starting does not answer at all: the connection is refused, which orchestration tools treat as not ready.

## Response Format

All API responses follow consistent serialization conventions:

- **Content-Type:** All responses are served as `application/json`.
- **Caching:** Every API response, errors included, carries `Cache-Control: no-store`, so no browser or proxy serves live data from a cache.
- **Compression:** Responses are compressed when the client asks for it, with Brotli (`br`) preferred over `gzip` when both are offered, and every compressed response carries `Vary: Accept-Encoding`. Browsers offer Brotli only over HTTPS, so a browser on the local network typically receives gzip. With `curl`, add `--compressed`. The Web Map's static files are compressed the same way; the SignalR connection at `/maphub` is not.
- **Field names** use PascalCase throughout, matching the C# property names in the underlying data model.
- **Timestamps** are formatted as ISO 8601 strings in UTC (e.g., `"2026-03-13T14:30:00Z"`).
- **Null handling** follows an explicit inclusion policy: within any returned section, all fields are always present. Fields that have no data are set to `null` rather than being omitted, which allows clients to discover the full schema from any single response.
- **Altitude** is represented as a rich value object containing the altitude in multiple units simultaneously: `{ "Type": "Barometric", "Feet": 38000, "Meters": 11582, "FlightLevel": 380 }`. `Meters` is rounded to the nearest meter. `FlightLevel` is the altitude in feet divided by 100 and rounded down, and is present for every altitude type; a flight level strictly refers to barometric altitude, so treat it with care on a geometric altitude.
- **Speed** is similarly represented with multiple units: `{ "Type": "GroundSpeed", "Knots": 480, "KilometersPerHour": 889, "MilesPerHour": 552, "MetersPerSecond": 246.93312 }`. `KilometersPerHour` and `MilesPerHour` are rounded to whole numbers; `MetersPerSecond` is not rounded.
- **Enums** are serialized as human-readable strings rather than numeric values (e.g., `"No Emergency"`, `"DO-260B"`, `"Heavy"`), making the API output more readable without requiring clients to maintain enum lookup tables.

## Error Responses

All error responses use a consistent format with a single `Error` field containing a human-readable message, accompanied by the appropriate HTTP status code:

| Code | Meaning                                                                                                   |
|------|-----------------------------------------------------------------------------------------------------------|
| 200  | The request was successful and the response contains the requested data                                   |
| 400  | The request was malformed: invalid ICAO address, unknown section name, invalid or repeated parameter      |
| 404  | The specified aircraft is not being tracked, or the requested route does not exist                        |
| 405  | The request used a method other than GET, which is the only supported HTTP method                         |
| 500  | An unexpected server error                                                                                |
| 502  | The photo endpoint could not get an answer from Planespotters; the next request retries                   |
| 503  | The health check found every input source failing; the body is the usual health response, not an error    |

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

# Only one history type per request
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position,Altitude"
# {"Error":"Invalid type: Position,Altitude"}  (HTTP 400)

# Repeating a query parameter
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&type=State"
# {"Error":"Parameter 'type' must not be repeated"}  (HTTP 400)

# Invalid after parameter (negative or non-numeric)
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=-1"
# {"Error":"Invalid after: -1"}  (HTTP 400)

curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=abc"
# {"Error":"Invalid after: abc"}  (HTTP 400)

# A source value other than true or false
curl -s "http://localhost:8080/api/v1/config?source=yes"
# {"Error":"Invalid source: yes. Must be true or false."}  (HTTP 400)

# Using after without specifying a type
curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?after=42"
# {"Error":"Parameter 'after' requires a type"}  (HTTP 400)

# Photo for an aircraft that is not being tracked
curl -s "http://localhost:8080/api/v1/aircraft/AAAAAA/photo"
# {"Error":"Aircraft not found: AAAAAA"}  (HTTP 404)

# A route that does not exist
curl -s "http://localhost:8080/api/v1/nope"
# {"Error":"Not found: /api/v1/nope"}  (HTTP 404)

# A method other than GET
curl -s -X POST "http://localhost:8080/api/v1/aircraft"
# {"Error":"Method not allowed: POST"}  (HTTP 405)
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
# First request: get all entries and remember the sequence ID of the newest one
LAST_SEQ=$(curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position" | jq '.Position.Entries[-1].SequenceId // 0')

# Subsequent requests: fetch only entries newer than the last one received
while true; do
  RESPONSE=$(curl -s "http://localhost:8080/api/v1/aircraft/407F19/history?type=Position&after=$LAST_SEQ")
  echo "$RESPONSE" | jq '.Position.Entries[]'
  LAST_SEQ=$(echo "$RESPONSE" | jq ".Position.Entries[-1].SequenceId // $LAST_SEQ")
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
# Exits with an error when every input is failing (HTTP 503)
curl -s --fail "http://localhost:8080/api/v1/health" | jq '{Status, Problems}'
```

### Monitor the frame rate and the CRC error ratio

```bash
curl -s "http://localhost:8080/api/v1/stats" | jq '{PerSecond: .Frames.PerSecond,
  SdrAcceptedPerSecond: .Sdr.AcceptedPerSecond,
  CrcErrorRatio: (if (.Sdr.Extracted // 0) > 0 then .Sdr.CrcErrors / .Sdr.Extracted else null end)}'
```

`SdrAcceptedPerSecond` and `CrcErrorRatio` are `null` on a daemon without an SDR device, and `CrcErrorRatio` also before the first frame is extracted.

### Check the state of every Beast source

```bash
curl -s "http://localhost:8080/api/v1/stats" | jq '.BeastSources[] | {Host, Port, Connected, Since, Reconnects}'
```

### Find out why a setting is not applied

```bash
# Which file was loaded, and where the network settings came from
curl -s "http://localhost:8080/api/v1/config?source=true" | jq '{ConfigFile, Network}'
```
