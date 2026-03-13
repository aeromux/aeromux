# Terminal User Interface (TUI)

Aeromux includes an interactive terminal interface for real-time aircraft tracking. The TUI is available in **live mode** and provides two views: the aircraft list, which serves as the main screen and shows all tracked aircraft in a sortable, searchable table, and the aircraft detail view, which displays comprehensive information for a single selected aircraft.

## Starting the TUI

The TUI is launched through the `live` command, which supports two operating modes depending on your setup:

```bash
# Standalone mode — reads directly from your RTL-SDR device(s) and displays the TUI
aeromux live --standalone --config aeromux.yaml

# Connect mode — connects to an existing Beast data source over the network and displays the TUI
aeromux live --connect host:port --config aeromux.yaml
```

In standalone mode, Aeromux manages the SDR devices directly and performs all demodulation and decoding locally. In connect mode, it receives pre-demodulated Beast binary data from another instance of Aeromux, dump1090, readsb, or any other Beast-compatible source.

## Aircraft List

The main screen displays all currently tracked aircraft in a table format. Each row represents one aircraft, and the columns show the most important tracking data at a glance:

| Column    | Description                                                                                                                                                                    |
|-----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ICAO      | The aircraft's 24-bit ICAO address, displayed as a 6-character hexadecimal string                                                                                              |
| Callsign  | The flight callsign (e.g., `DLH1234`), or `N/A` if no identification message has been received yet                                                                             |
| Altitude  | Barometric or geometric altitude, displayed in the currently selected unit (feet or meters)                                                                                    |
| Vertical  | Vertical rate (climb or descent speed) in feet per minute or meters per second                                                                                                 |
| Distance  | Distance from the receiver to the aircraft, calculated from the configured receiver location. Requires the receiver latitude and longitude to be set in the configuration file |
| Speed     | Ground speed or airspeed, displayed in the currently selected unit (knots, km/h, or mph)                                                                                       |
| Messages  | Total number of Mode S messages received from this aircraft since it was first detected                                                                                        |
| Signal    | Signal strength in dBFS (decibels relative to full scale), indicating reception quality                                                                                        |
| Last seen | Elapsed time since the most recent message was received from this aircraft                                                                                                     |

The rightmost column displays a scrollbar that indicates the current viewport position within the list, which is useful when the number of tracked aircraft exceeds the terminal height.

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

The footer occupies the bottom three rows of the terminal and provides contextual information about the current state of the interface:

1. **Sort/Search row** — Displays the available sort columns (`F1` through `F6`) with a direction arrow next to the currently active sort column. When search mode is active, this row shows the search prompt instead.
2. **Status row** — Shows the total number of tracked aircraft, the currently selected row number, the visible viewport range, and the active display units for distance, altitude, and speed.
3. **Navigation row** — Provides a quick reference of the most commonly used keyboard shortcuts for the current view.

## Aircraft Detail View

Pressing `Enter` on a selected aircraft in the list opens the detail view, which displays all available information about that aircraft organized into clearly labeled sections. This view provides a comprehensive overview of everything Aeromux knows about the aircraft, including data that is not shown in the compact list view.

The detail view is organized into the following sections:

- **Identification** — The aircraft's ICAO address, callsign, wake turbulence category, squawk code, and emergency state.
- **Aircraft Database** — Static metadata from the [aeromux-db](https://github.com/nandortoth/aeromux-db) database, including registration, operator, manufacturer, aircraft type, and regulatory flags such as FAA PIA and LADD.
- **Status** — Timestamps for when the aircraft was first and last seen, message counts broken down by type (position, velocity, identification), and the current signal strength.
- **Position** — Geographic coordinates, distance from the receiver, barometric and geometric altitudes with their delta, ground state, and position source.
- **Velocity & Dynamics** — Ground speed, airspeed, heading, track angle, vertical rate, roll angle, Mach number, turn rate, and surface movement data.
- **Autopilot** — Selected altitude and heading, barometric pressure setting, and autopilot mode flags (VNAV, LNAV, altitude hold, approach).
- **Meteorology** — Wind speed and direction, static and total air temperatures, atmospheric pressure, radio height, and hazard severity levels for turbulence, wind shear, microburst, icing, and wake vortex.
- **ACAS/TCAS** — TCAS operational status, sensitivity level, cross-link capability, resolution advisory state and complement, and threat encounter details.
- **Capabilities** — Transponder level, ADS-B version, data link feature support (1090ES, UAT, CDTI), operational flags, aircraft dimensions, and GPS antenna offsets.
- **Data Quality** — Navigation accuracy (NACp, NACv), navigation integrity (NICbaro, NIC supplements), surveillance integrity (SIL), geometric vertical accuracy, antenna configuration, and system design assurance level.

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

Fields that have not yet been received from the aircraft display `N/A (no data yet)` as their value. The Aircraft Database section requires the [aeromux-db](https://github.com/nandortoth/aeromux-db) database to be configured and enabled; without it, a message is displayed indicating that no database is available.

Display units for distance, altitude, and speed can be toggled directly within the detail view using the `D`, `A`, and `S` keys — see [Display Units](#display-units) for details. The detail view also supports field name search, activated by pressing `/` — see [Detail View Search](#detail-view-search) for more information.

## Keyboard Reference

### Aircraft List

The following keyboard shortcuts are available in the aircraft list view:

| Key            | Action                                                                              |
|----------------|-------------------------------------------------------------------------------------|
| `↑` / `↓`      | Move the selection highlight up or down by one row                                  |
| `←` / `→`      | Move the selection up or down by one full page (the number of visible rows)         |
| `Page Up/Down` | Same as `←` / `→` — move the selection by one page                                  |
| `Home`         | Jump the selection to the first aircraft in the list                                |
| `End`          | Jump the selection to the last aircraft in the list                                 |
| `Enter`        | Open the detail view for the currently selected aircraft                            |
| `D`            | Toggle the distance display unit between miles and kilometers                       |
| `A`            | Toggle the altitude display unit between feet and meters                            |
| `S`            | Cycle the speed display unit through knots, kilometers per hour, and miles per hour |
| `F1`–`F6`      | Sort the aircraft list by a column — see [Sorting](#sorting) for details            |
| `F12`          | Reset all settings (sort column, display units, and search) back to their defaults  |
| `/`            | Enter search mode to filter the aircraft list — see [Search](#search)               |
| `Q` / `Esc`    | Quit the TUI and return to the terminal                                             |

### Detail View

The following keyboard shortcuts are available in the aircraft detail view:

| Key            | Action                                                                                        |
|----------------|-----------------------------------------------------------------------------------------------|
| `↑` / `↓`      | Move the selection up or down by one row, automatically skipping section headers and dividers |
| `←` / `→`      | Move the selection up or down by one full page                                                |
| `Page Up/Down` | Same as `←` / `→` — move the selection by one page                                            |
| `Home`         | Jump the selection to the first data field in the detail view                                 |
| `End`          | Jump the selection to the last data field in the detail view                                  |
| `D`            | Toggle the distance display unit between miles and kilometers                                 |
| `A`            | Toggle the altitude display unit between feet and meters                                      |
| `S`            | Cycle the speed display unit through knots, kilometers per hour, and miles per hour           |
| `/`            | Enter search mode to search field names — see [Detail View Search](#detail-view-search)       |
| `Esc`          | Close the detail view and return to the aircraft list                                         |
| `Q`            | Quit the TUI entirely and return to the terminal                                              |

### Detail View Search Mode

When search mode is active in the detail view, the following keys are available:

| Key                                    | Action                                                                                   |
|----------------------------------------|------------------------------------------------------------------------------------------|
| Letters, digits, `()`, `-`, `,`, space | Append the character to the search input (up to 15 characters, displayed in uppercase)   |
| `Backspace`                            | Remove the last character from the search input                                          |
| `↑` / `↓` / `Tab`                      | Cycle through the matching fields, wrapping around when reaching the end or beginning    |
| `←` / `→`                              | Move the selection up or down by one full page while keeping the search active           |
| `Home` / `End`                         | Jump to the first or last field in the detail view while keeping the search active       |
| `Enter`                                | Confirm the search and keep the selection at the current matching field                  |
| `Esc`                                  | Cancel the search and restore the selection to the position it was at before searching   |

### Aircraft List Search Mode

When search mode is active in the aircraft list, the following keys are available:

| Key              | Action                                                                                        |
|------------------|-----------------------------------------------------------------------------------------------|
| `A`–`Z`, `0`–`9` | Append the character to the search input (up to 8 characters)                                 |
| `Backspace`      | Remove the last character from the search input                                               |
| `↑` / `↓`        | Navigate up and down within the filtered list of matching aircraft                            |
| `Home` / `End`   | Jump to the first or last aircraft in the filtered list                                       |
| `Enter`          | Confirm the search and open the detail view for the currently selected matching aircraft      |
| `Esc`            | Cancel the search, restore the original unfiltered list, and return to the previous selection |
| `F12`            | Reset all settings (sort, units, and search) back to their defaults                           |

## Sorting

The aircraft list can be sorted by any of six columns using the function keys `F1` through `F6`. Each key corresponds to a specific column:

| Key  | Column   |
|------|----------|
| `F1` | ICAO     |
| `F2` | Callsign |
| `F3` | Altitude |
| `F4` | Vertical |
| `F5` | Distance |
| `F6` | Speed    |

Pressing the same function key repeatedly cycles through three states: **ascending** (indicated by a ▲ arrow), **descending** (indicated by a ▼ arrow), and **default** (which resets the sort to ICAO ascending). The currently active sort column is highlighted in the footer row, with the direction arrow displayed next to the column name.

When sorting by a column, aircraft that have no data for that column (displayed as `N/A`) are always placed at the bottom of the list, regardless of whether the sort direction is ascending or descending. When two aircraft have identical values for the sort column, the ICAO address is used as a tiebreaker, with ascending alphabetical order applied.

## Search

Both the aircraft list and the detail view support a search mode, activated by pressing the `/` key. The two search modes behave differently to suit their respective contexts.

### Aircraft List Search

In the aircraft list, search mode filters the list to show only aircraft whose ICAO address or callsign contains the search term as a case-insensitive substring. For example, typing `WZZ` would match all Wizz Air flights by callsign (e.g., `WZZ268`, `WZZ5070`) as well as any aircraft whose ICAO address happens to contain those characters.

The search input accepts up to 8 characters. As you type, the list is immediately filtered and matching substrings are highlighted in both the ICAO and Callsign columns. The footer updates to show the search prompt along with the number of matching aircraft. Press `Enter` to confirm the search and open the detail view for the selected matching aircraft, or press `Esc` to cancel the search and restore the original unfiltered list with the previous selection.

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

In the detail view, search mode uses a **jump-and-highlight** approach rather than filtering. All rows remain visible at all times, but the selection cursor jumps to the first field whose name matches the search term, and the matching characters are highlighted in red. This design preserves the surrounding section context so you can see related fields near the match.

The search input accepts up to 15 characters, including letters (displayed in uppercase), digits, spaces, parentheses, hyphens, and commas. The matching is case-insensitive. When there are multiple matches, use `↑`, `↓`, or `Tab` to cycle through them — the navigation wraps around from the last match to the first and vice versa. Press `Enter` to confirm the search and keep the selection at the current match, or press `Esc` to cancel and restore the selection to its position before the search was started.

## Display Units

Three measurement units can be toggled independently in both the aircraft list and the detail view, allowing you to switch between unit systems without leaving the current view:

| Key | Unit     | Options                              | Default |
|-----|----------|--------------------------------------| --------|
| `D` | Distance | Miles (mi) / Kilometers (km)         | Miles   |
| `A` | Altitude | Feet (ft) / Meters (m)               | Feet    |
| `S` | Speed    | Knots (kts) / km/h / mph             | Knots   |

The currently selected units are displayed in the status footer row on the right side. Pressing `F12` resets all display units, the sort column, and any active search back to their default values. The current row selection is preserved when resetting.

## Terminal Resize

The TUI automatically adapts to changes in the terminal size. When the terminal is resized, the viewport adjusts to accommodate the new dimensions, and the display redraws accordingly. Due to a known limitation in the Spectre.Console library, the screen briefly clears and redraws during a resize operation — this is expected behavior and does not affect the tracking state.
