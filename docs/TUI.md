# Terminal User Interface (TUI)

Aeromux includes an interactive terminal interface for real-time aircraft tracking. The TUI is available in **live mode** and provides two views: the aircraft list (main screen) and the aircraft detail view.

## Starting the TUI

```bash
# Standalone mode — reads directly from your SDR device(s)
aeromux live --standalone --config aeromux.yaml

# Connect mode — connects to an existing Beast data source
aeromux live --connect host:port --config aeromux.yaml
```

## Aircraft List

The main screen displays all tracked aircraft in a table with the following columns:

| Column    | Description                                                    |
|-----------|----------------------------------------------------------------|
| ICAO      | 24-bit ICAO address (hex)                                      |
| Callsign  | Flight callsign or `N/A` if not yet received                   |
| Altitude  | Barometric or geometric altitude                               |
| Vertical  | Vertical rate in ft/min                                        |
| Distance  | Distance from receiver (requires receiver location in config)  |
| Speed     | Ground speed or airspeed                                       |
| Messages  | Total messages received from this aircraft                     |
| Signal    | Signal strength in dBFS                                        |
| Last seen | Time since last message                                        |

The rightmost column is a scrollbar indicating the viewport position when the list exceeds the terminal height.

```
                                        AIRCRAFT LIST - Aeromux
┌────────┬──────────┬──────────┬────────────┬───────────┬──────────┬──────────┬────────┬───────────┬───┐
│ ICAO   │ Callsign │ Altitude │   Vertical │  Distance │    Speed │ Messages │ Signal │ Last seen │   │
├────────┼──────────┼──────────┼────────────┼───────────┼──────────┼──────────┼────────┼───────────┼───┤
│ 06A13C │ QTR3293  │ 37000 ft │     0 ft/m │  193.2 mi │  472 kts │      191 │  -25.9 │  0.1s ago │ ░ │
│ 392AED │ N/A      │ 37000 ft │   -64 ft/m │  203.2 mi │  450 kts │       40 │  -27.3 │  0.1s ago │ ░ │
│ 3965AF │ AFR274   │ 31000 ft │     0 ft/m │   55.3 mi │  477 kts │      322 │   -4.3 │  0.0s ago │ ░ │
│ 3C6593 │ DLH8KC   │ 39025 ft │     0 ft/m │    8.8 mi │  461 kts │      300 │   -5.7 │  0.0s ago │ ░ │
│ 4007EE │ BAW2231  │ 33000 ft │     0 ft/m │   79.2 mi │  470 kts │      340 │   -9.7 │  0.1s ago │ ░ │
│ ...    │ ...      │ ...      │        ... │       ... │      ... │      ... │    ... │       ... │ ░ │
└────────┴──────────┴──────────┴────────────┴───────────┴──────────┴──────────┴────────┴───────────┴───┘
  F1: ICAO ▲  F2: Callsign  F3: Altitude  F4: Vertical  F5: Distance  F6: Speed             F12: Reset
  Aircraft: 34 | Selected: 1/34 | Viewport: 1-34                         Dist: mi | Alt: ft | Spd: kts
  ↑/↓: Row, ←/→: Page, Home/End                       ENTER: Details, D/A/S: Units, /: Search, Q: Quit
```

### Footer

The footer has three rows:

1. **Sort/Search** — Shows the current sort column with a direction arrow, or the search prompt when searching.
2. **Status** — Aircraft count, selected row, viewport range, and current display units.
3. **Navigation** — Keyboard shortcuts reference.

## Aircraft Detail View

Press `Enter` on a selected aircraft to open its detail view. This shows all available information organized into sections:

- **Identification** — ICAO address, callsign, category, squawk, emergency state
- **Aircraft Database** — Registration, operator, manufacturer, type (from [aeromux-db](https://github.com/nandortoth/aeromux-db))
- **Status** — First/last seen timestamps, message counts, signal strength
- **Position** — Latitude, longitude, distance, barometric and geometric altitude
- **Velocity & Dynamics** — Speed, heading, track, vertical rate, roll angle, Mach number, surface movement
- **Autopilot** — Selected altitude/heading, barometric pressure, autopilot modes
- **Meteorology** — Wind speed/direction, temperature, turbulence, icing
- **ACAS/TCAS** — TCAS operational status, sensitivity, resolution advisories
- **Capabilities** — Transponder level, ADS-B version, data link features, operational state
- **Data Quality** — Antenna, NACp, NACv, NICbaro, SIL accuracy and integrity parameters

```
                                   AIRCRAFT DETAIL (471DB6) - Aeromux
┌──────────────────────────────────────────┬───────────────────────────────────────────────────────┬───┐
│ Field                                    │ Value                                                 │   │
├──────────────────────────────────────────┼───────────────────────────────────────────────────────┼───┤
│ === IDENTIFICATION ===================== │ ===================================================== │ █ │
│ --- Identity --------------------------- │ ----------------------------------------------------- │ █ │
│ ICAO Address                             │ 471DB6                                                │ █ │
│ Callsign                                 │ WZZ268                                                │ █ │
│ Category                                 │ Large                                                 │ █ │
│ --- Transponder ------------------------ │ ----------------------------------------------------- │ █ │
│ Squawk                                   │ 5301                                                  │ █ │
│ Emergency                                │ NoEmergency                                           │ █ │
│ Flight Status                            │ AirborneNormal                                        │ █ │
│                                          │                                                       │ █ │
│ === AIRCRAFT DATABASE ================== │ ===================================================== │ █ │
│ --- Registration ----------------------- │ ----------------------------------------------------- │ █ │
│ Registration                             │ HA-LGO                                                │ █ │
│ Registration Country                     │ Hungary                                               │ █ │
│ Operator Name                            │ Wizz Air Hungary                                      │ █ │
│ --- Aircraft Type ---------------------- │ ----------------------------------------------------- │ █ │
│ Manufacturer ICAO                        │ N/A                                                   │ █ │
│ Manufacturer Name                        │ AIRBUS                                                │ █ │
│ Type Class ICAO                          │ L2J                                                   │ ░ │
│ Type Designator                          │ A21N                                                  │ ░ │
│ Type Description                         │ AIRBUS A-321neo                                       │ ░ │
│ Aircraft Model                           │ A321neo                                               │ ░ │
│ --- Flags ------------------------------ │ ----------------------------------------------------- │ ░ │
│ FAA PIA (Privacy)                        │ No                                                    │ ░ │
│ FAA LADD (Limiting)                      │ No                                                    │ ░ │
│ Military                                 │ No                                                    │ ░ │
│ ...                                      │ ...                                                   │ ░ │
└──────────────────────────────────────────┴───────────────────────────────────────────────────────┴───┘
  Row: 3/187                                                             Dist: mi | Alt: ft | Spd: kts
  ↑/↓: Row, ←/→: Page, Home/End                            D/A/S: Units, /: Search, ESC: Back, Q: Quit
```

Fields that have not yet been received display `N/A (no data yet)`. The Aircraft Database section requires the [aeromux-db](https://github.com/nandortoth/aeromux-db) database to be configured; without it, a message indicates that no database is available.

Display units (`D`/`A`/`S`) can be toggled directly in the detail view — see [Display Units](#display-units). Press `/` to search field names — see [Detail View Search](#detail-view-search).

## Keyboard Reference

### Aircraft List

| Key            | Action                                     |
|----------------|--------------------------------------------|
| `↑` / `↓`      | Move selection up/down by one row          |
| `←` / `→`      | Move selection up/down by one page         |
| `Page Up/Down` | Same as `←` / `→`                          |
| `Home`         | Jump to the first aircraft                 |
| `End`          | Jump to the last aircraft                  |
| `Enter`        | Open detail view for the selected aircraft |
| `D`            | Toggle distance unit (miles / kilometers)  |
| `A`            | Toggle altitude unit (feet / meters)       |
| `S`            | Cycle speed unit (knots / km/h / mph)      |
| `F1`–`F6`      | Sort by column (see [Sorting](#sorting))   |
| `F12`          | Reset sort, units, and search to defaults  |
| `/`            | Enter search mode (see [Search](#search))  |
| `Q` / `Esc`    | Quit                                       |

### Detail View

| Key            | Action                                                            |
|----------------|-------------------------------------------------------------------|
| `↑` / `↓`      | Move selection up/down (skips section headers)                    |
| `←` / `→`      | Move up/down by one page                                          |
| `Page Up/Down` | Same as `←` / `→`                                                 |
| `Home`         | Jump to the first field                                           |
| `End`          | Jump to the last field                                            |
| `D`            | Toggle distance unit (miles / kilometers)                         |
| `A`            | Toggle altitude unit (feet / meters)                              |
| `S`            | Cycle speed unit (knots / km/h / mph)                             |
| `/`            | Enter search mode (see [Detail View Search](#detail-view-search)) |
| `Esc`          | Return to the aircraft list                                       |
| `Q`            | Quit                                                              |

### Detail View Search Mode

| Key                                    | Action                                                  |
|----------------------------------------|---------------------------------------------------------|
| Letters, digits, `()`, `-`, `,`, space | Append to search input (max 15 chars, shown uppercase)  |
| `Backspace`                            | Remove last character from search input                 |
| `↑` / `↓` / `Tab`                      | Cycle through matching fields (wraps around)            |
| `←` / `→`                              | Move up/down by one page                                |
| `Home` / `End`                         | Jump to first/last field                                |
| `Enter`                                | Confirm search and keep current position                |
| `Esc`                                  | Cancel search and restore previous position             |

### Aircraft List Search Mode

| Key              | Action                                            |
|------------------|---------------------------------------------------|
| `A`–`Z`, `0`–`9` | Append character to search input (max 8 chars)    |
| `Backspace`      | Remove last character from search input           |
| `↑` / `↓`        | Navigate the filtered list                        |
| `Home` / `End`   | Jump to first/last match                          |
| `Enter`          | Confirm search and open detail view               |
| `Esc`            | Cancel search and restore previous selection      |
| `F12`            | Reset all settings to defaults                    |

## Sorting

Press `F1`–`F6` to sort the aircraft list by a column:

| Key  | Column   |
|------|----------|
| `F1` | ICAO     |
| `F2` | Callsign |
| `F3` | Altitude |
| `F4` | Vertical |
| `F5` | Distance |
| `F6` | Speed    |

Pressing the same key cycles through: **ascending** (▲) → **descending** (▼) → **default** (ICAO ascending).

The active sort column is highlighted in the footer. The direction arrow appears next to the active column name.

Sorting behavior:
- Aircraft with no data for the sort column (N/A) always appear at the bottom, regardless of sort direction.
- When values are equal, aircraft are sorted by ICAO address (ascending) as a tiebreaker.

## Search

Both the aircraft list and the detail view support search mode, activated by pressing `/`.

### Aircraft List Search

Type to filter the aircraft list (up to 8 characters) — the search matches against both ICAO address and callsign as a case-insensitive substring match. For example, typing `WZZ` matches all Wizz Air flights by callsign and any ICAO address containing `WZZ`.

Matching substrings are highlighted in the ICAO and callsign columns. The footer shows the search prompt with a match count. Press `Enter` to confirm and open the selected aircraft's detail view, or `Esc` to cancel and restore the previous selection.

```
                                        AIRCRAFT LIST - Aeromux
┌────────┬──────────┬──────────┬────────────┬───────────┬──────────┬──────────┬────────┬───────────┬───┐
│ ICAO   │ Callsign │ Altitude │   Vertical │  Distance │    Speed │ Messages │ Signal │ Last seen │   │
├────────┼──────────┼──────────┼────────────┼───────────┼──────────┼──────────┼────────┼───────────┼───┤
│ 471DB9 │ WZZ6LG   │ 35025 ft │   -64 ft/m │  157.2 mi │  443 kts │      200 │  -29.0 │  0.1s ago │ ░ │
│ 471DBD │ WZZ5070  │  1400 ft │  -896 ft/m │   36.1 mi │  152 kts │      419 │  -27.3 │  0.2s ago │ ░ │
│ 471FA2 │ WZZ88DJ  │ 35000 ft │     0 ft/m │  128.0 mi │  426 kts │      209 │  -28.1 │  1.0s ago │ ░ │
│        │          │          │            │           │          │          │        │           │ ░ │
│ ...    │          │          │            │           │          │          │        │           │ ░ │
└────────┴──────────┴──────────┴────────────┴───────────┴──────────┴──────────┴────────┴───────────┴───┘
  Search: WZZ_ (3 matches)                                                                 ESC: Cancel
  Aircraft: 3 | Selected: 2/3 | Viewport: 1-3                            Dist: mi | Alt: ft | Spd: kts
  ↑/↓: Row, ←/→: Page, Home/End                       ENTER: Details, D/A/S: Units, /: Search, Q: Quit
```

### Detail View Search

Type to search field names in the detail view (up to 15 characters). Unlike the aircraft list search which filters the list, the detail view search uses a **jump-and-highlight** approach — all rows remain visible, the selection jumps to the first matching field, and matching characters are highlighted in red. This preserves section context around the matched field.

Accepted characters: letters (displayed uppercase), digits, space, parentheses, hyphen, and comma. The search is case-insensitive. Use `↑`/`↓`/`Tab` to cycle through matches (wraps around). Press `Enter` to confirm (keeps current position) or `Esc` to cancel (restores previous position).

## Display Units

Three display units can be toggled in both the aircraft list and the detail view:

| Key | Unit     | Options                      | Default |
|-----|----------|------------------------------|---------|
| `D` | Distance | Miles (mi) / Kilometers (km) | Miles   |
| `A` | Altitude | Feet (ft) / Meters (m)       | Feet    |
| `S` | Speed    | Knots (kts) / km/h / mph     | Knots   |

The current units are shown in the status footer row. Press `F12` to reset all units, sort, and search to defaults. The current selection is preserved.

## Terminal Resize

The TUI adapts to the terminal size. The viewport adjusts automatically when the terminal is resized. The display uses a workaround for a known Spectre.Console resize issue — the screen briefly clears and redraws on resize, which is expected behavior.
