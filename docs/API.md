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

## Endpoints

The API exposes six endpoints, all under the `/api/v1/` prefix. All endpoints accept only `GET` requests:

| Endpoint                              | Description                                                                         |
|---------------------------------------|-------------------------------------------------------------------------------------|
| `GET /api/v1/aircraft`                | Returns a compact list of all currently tracked aircraft                            |
| `GET /api/v1/aircraft/{icao}`         | Returns detailed information for a single aircraft, organized into 10 sections      |
| `GET /api/v1/aircraft/{icao}/history` | Returns position, altitude, velocity, and state history for a single aircraft       |
| `GET /api/v1/aircraft/{icao}/photo`   | Returns photo metadata sourced from Planespotters.net (thumbnail URL + attribution) |
| `GET /api/v1/stats`                   | Returns receiver and session statistics including uptime and frame rates            |
| `GET /api/v1/health`                  | Lightweight health check for Docker, monitoring tools, and readiness probes         |

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

| Parameter | Type   | Default | Description                                                                  |
|-----------|--------|---------|------------------------------------------------------------------------------|
| `bounds`  | string | (none)  | Geographic bounding box: `<south>,<west>,<north>,<east>`                     |
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

## Statistics

The statistics endpoint returns information about the receiver's operational state, including how long the daemon has been running, how many aircraft are currently tracked, and detailed frame processing metrics. The frame statistics are useful for monitoring receiver health and diagnosing reception issues.

```bash
curl -s "http://localhost:8080/api/v1/stats"
```

```json
{
  "Version": "0.8.4",
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
  },
  "HeatmapCollectionEnabled": true
}
```

The `Uptime` field is the number of seconds since the daemon started, the same span the session summary reports when the daemon stops. The `Stream` section shows cumulative frame counts since the receiver started. `FramesPerSecond` is the average rate of valid frames over that whole period, not an instantaneous rate, so it changes slowly once the daemon has been running for a while.

When the daemon has no SDR device, for example when it only consumes Beast input, there are no frame statistics: every `Stream` field is `0` and `Devices` is `0`.

The `Receiver` section includes the station's geographic location as configured in the YAML file, which is useful for clients that need to calculate distances or render the receiver on a map. The `HeatmapCollectionEnabled` field reflects the `heatmap.collect` configuration option: whether the daemon is aggregating the Web Map traffic-density heatmap grid.

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

The HTTP server starts only after the aircraft state tracker is running, so any response from this endpoint reports `OK`. A daemon that is still starting does not answer at all: the connection is refused, which orchestration tools treat as not ready.

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
curl -s "http://localhost:8080/api/v1/health" | jq .Status
```

### Monitor the frame processing rate and error ratio

```bash
curl -s "http://localhost:8080/api/v1/stats" | jq '{FPS: .Stream.FramesPerSecond, Errors: .Stream.CrcErrors, Total: .Stream.TotalFrames}'
```
