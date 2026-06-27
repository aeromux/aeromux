// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.
//
// Aircraft-icon resolver. The lookup tables (TYPE_DESIGNATOR,
// TYPE_DESCRIPTION, TYPE_DESCRIPTION_FIRSTCHAR, CATEGORY) and the
// fall-through ordering in resolveShape() are derived from tar1090's
// markers.js (TypeDesignatorIcons, TypeDescriptionIcons,
// CategoryIcons, getBaseMarker). GPLv3 → GPLv3.
//
// Source: tar1090 markers.js (commit 8010bc689b22fc610faf68d37132576051cec12a)
//   https://github.com/wiedehopf/tar1090

import { SHAPES } from './AircraftShapes.js';

// Layer 1: direct ICAO type-designator lookup (e.g. "A320" -> a320).
// Imported verbatim from tar1090's TypeDesignatorIcons; entries
// targeting ground-vehicle shapes are dropped.
export const TYPE_DESIGNATOR = {
    'SHIP': ['blimp', 0.94], // Blimp
    'BALL': ['balloon', 1], // Balloon

    'A318': ['a319', 0.95], // shortened a320 68t
    'A319': ['a319', 1], // shortened a320 75t
    'A19N': ['a319', 1], // shortened a320
    'A320': ['a320', 1], // 78t
    'A20N': ['a320', 1],
    'A321': ['a321', 1], // stretched a320 93t
    'A21N': ['a321', 1], // stretched a320

    'A306': ['heavy_2e', 0.93],
    'A330': ['a332', 0.98],
    'A332': ['a332', 0.99],
    'A333': ['a332', 1.00],
    'A338': ['a332', 1.00], // 800 neo
    'A339': ['a332', 1.01], // 900 neo
    'DC10': ['md11', 0.92],
    'MD11': ['md11', 0.96],

    'A359': ['a359', 1.00],
    'A35K': ['a359', 1.02],

    'A388': ['a380', 1],

    // dubious since these are old-generation 737s
    // but the shape is similar
    'B731': ['b737', 0.90], // len: 29m
    'B732': ['b737', 0.92], // len: 31m

    'B735': ['b737', 0.96], // len: 31m
    'B733': ['b737', 0.98], // len: 33m
    'B734': ['b737', 0.98], // len: 36m

    // next generation
    'B736': ['b737', 0.96], // len: 31m
    'B737': ['b737', 1.00], // len: 33m
    'B738': ['b738', 1.00], // len: 39m
    'B739': ['b739', 1.00], // len: 42m

    // max
    'B37M': ['b737', 1.02], // len: 36m (not yet certified)
    'B38M': ['b738', 1.00], // len: 39m
    'B39M': ['b739', 1.00], // len: 42m
    'B3XM': ['b739', 1.01], // len: 44m (not yet certified)

    'P8': ['p8', 1.00],
    'P8 ?': ['p8', 1.00],

    'E737': ['e737', 1.00],

    'J328': ['airliner', 0.78], // 15t
    'E170': ['airliner', 0.82], // 38t
    'E75S/L': ['airliner', 0.82],
    'E75L': ['airliner', 0.82],
    'E75S': ['airliner', 0.82], // 40t
    'A148': ['airliner', 0.83], // 43t
    'RJ70': ['b707', 0.68], // 38t
    'RJ85': ['b707', 0.68], // 42t
    'RJ1H': ['b707', 0.68], // 44t
    'B461': ['b707', 0.68], // 44t
    'B462': ['b707', 0.68], // 44t
    'B463': ['b707', 0.68], // 44t
    'E190': ['airliner', 0.81], // 52t
    'E195': ['airliner', 0.81], // 52t
    'E290': ['airliner', 0.82], // 56t
    'E295': ['airliner', 0.83], // 62t
    'BCS1': ['airliner', 0.835], // 64t
    'BCS3': ['airliner', 0.85], // 70t

    'B741': ['heavy_4e', 0.96],
    'B742': ['heavy_4e', 0.96],
    'B743': ['heavy_4e', 0.96],
    'B744': ['heavy_4e', 0.96],
    'B74D': ['heavy_4e', 0.96],
    'B74S': ['heavy_4e', 0.96],
    'B74R': ['heavy_4e', 0.96],
    'BLCF': ['heavy_4e', 0.96],
    'BSCA': ['heavy_4e', 0.96], // hah!
    'B748': ['heavy_4e', 0.98],

    'B752': ['heavy_2e', 0.9],
    'B753': ['heavy_2e', 0.9],

    'B772': ['heavy_2e', 1.00], // all pretty similar except for length
    'B773': ['heavy_2e', 1.02],
    'B77L': ['heavy_2e', 1.02],
    'B77W': ['heavy_2e', 1.04],

    'B701': ['b707', 1],
    'B703': ['b707', 1],
    'K35R': ['b707', 1],
    'K35E': ['b707', 1],

    'FA20': ['jet_swept', 0.92], // 13t
    'C680': ['jet_swept', 0.92], // 14t
    'C68A': ['jet_swept', 0.92], // 14t
    'YK40': ['jet_swept', 0.94], // 16t
    'C750': ['jet_swept', 0.94], // 17t
    'F2TH': ['jet_swept', 0.94], // 16t
    'FA50': ['jet_swept', 0.94], // 18t
    'CL30': ['jet_swept', 0.92], // 14t
    'CL35': ['jet_swept', 0.92],
    'F900': ['jet_swept', 0.96], // 21t
    'CL60': ['jet_swept', 0.96], // 22t
    'G200': ['jet_swept', 0.92], // 16t
    'G280': ['jet_swept', 0.92], // 18t
    'HA4T': ['jet_swept', 0.92], // 18t
    'FA7X': ['jet_swept', 0.96], // 29t
    'FA8X': ['jet_swept', 0.96], // 33t
    'FA6X': ['jet_swept', 0.96], // 35t
    'GLF2': ['jet_swept', 0.96], // 29t
    'GLF3': ['jet_swept', 0.96], // 31t
    'GLF4': ['jet_swept', 0.96], // 34t
    'GA5C': ['jet_swept', 0.96], // 34t
    'GL5T': ['jet_swept', 0.98], // 40t
    'GLF5': ['jet_swept', 0.98], // 41t
    'GA6C': ['jet_swept', 0.98], // 41t
    'GLEX': ['jet_swept', 1], // 45t
    'GL6T': ['jet_swept', 1], // 45t
    'GLF6': ['jet_swept', 1], // 48t
    'GA7C': ['jet_swept', 1], // 48t
    'GA8C': ['jet_swept', 1], // 48t (fantasy type but in the database)
    'GL7T': ['jet_swept', 1], // 52t
    'E135': ['jet_swept', 0.92], // 20t
    'E35L': ['jet_swept', 0.92], // 24t
    'E145': ['jet_swept', 0.92], // 22t
    'E45X': ['jet_swept', 0.92], // 24t
    'E390': ['e390', 1],
    'CRJ1': ['jet_swept', 0.92], // 24t
    'CRJ2': ['jet_swept', 0.92], // 24t
    'F28': ['jet_swept', 0.93], // 32t
    'CRJ7': ['jet_swept', 0.94], // 34t
    'CRJ9': ['jet_swept', 0.96], // 38t
    'F70': ['jet_swept', 0.97], // 40
    'CRJX': ['jet_swept', 0.98], // 41t
    'F100': ['jet_swept', 1], // 45t
    'DC91': ['jet_swept', 1],
    'DC92': ['jet_swept', 1],
    'DC93': ['jet_swept', 1],
    'DC94': ['jet_swept', 1],
    'DC95': ['jet_swept', 1],
    'MD80': ['jet_swept', 1.06], // 60t
    'MD81': ['jet_swept', 1.06],
    'MD82': ['jet_swept', 1.06],
    'MD83': ['jet_swept', 1.06],
    'MD87': ['jet_swept', 1.06],
    'MD88': ['jet_swept', 1.06], // 72t
    'MD90': ['jet_swept', 1.06],
    'B712': ['jet_swept', 1.06], // 54t
    'B721': ['jet_swept', 1.10], // 80t
    'B722': ['jet_swept', 1.10], // 80t

    'T154': ['jet_swept', 1.12], // 100t

    'BE40': ['jet_nonswept', 1], // 7.3t
    'FA10': ['jet_nonswept', 1], // 8t
    'C501': ['jet_nonswept', 1],
    'C510': ['jet_nonswept', 1],
    'C25A': ['jet_nonswept', 1],
    'C25B': ['jet_nonswept', 1],
    'C25C': ['jet_nonswept', 1],
    'C525': ['jet_nonswept', 1],
    'C550': ['jet_nonswept', 1],
    'C560': ['jet_nonswept', 1],
    'C56X': ['jet_nonswept', 1], // 9t
    'LJ23': ['jet_nonswept', 1],
    'LJ24': ['jet_nonswept', 1],
    'LJ25': ['jet_nonswept', 1],
    'LJ28': ['jet_nonswept', 1],
    'LJ31': ['jet_nonswept', 1],
    'LJ35': ['jet_nonswept', 1], // 8t
    'LR35': ['jet_nonswept', 1], // wrong but in DB
    'LJ40': ['jet_nonswept', 1],
    'LJ45': ['jet_nonswept', 1],
    'LR45': ['jet_nonswept', 1], // wrong but in DB
    'LJ55': ['jet_nonswept', 1],
    'LJ60': ['jet_nonswept', 1], // 10t
    'LJ70': ['jet_nonswept', 1],
    'LJ75': ['jet_nonswept', 1],
    'LJ85': ['jet_nonswept', 1],

    'C650': ['jet_nonswept', 1.03], // 11t
    'ASTR': ['jet_nonswept', 1.03], // 11t
    'G150': ['jet_nonswept', 1.03], // 11t
    'H25A': ['jet_nonswept', 1.03], // 12t
    'H25B': ['jet_nonswept', 1.03], // 12t
    'H25C': ['jet_nonswept', 1.03], // 12t

    'PRM1': ['jet_nonswept', 0.96],
    'E55P': ['jet_nonswept', 0.96],
    'E50P': ['jet_nonswept', 0.96],
    'EA50': ['jet_nonswept', 0.96],
    'HDJT': ['jet_nonswept', 0.96],
    'SF50': ['jet_nonswept', 0.94],

    'C97': ['super_guppy', 1],
    'SGUP': ['super_guppy', 1],
    'A3ST': ['beluga', 1],
    'A337': ['beluga', 1.06],
    'WB57': ['wb57', 1],

    'A37': ['hi_perf', 1],
    'A700': ['hi_perf', 1],
    'LEOP': ['hi_perf', 1],
    'ME62': ['hi_perf', 1],
    'T2': ['hi_perf', 1],
    'T37': ['hi_perf', 1],
    'T38': ['t38', 1],
    'F104': ['t38', 1],
    'A10': ['a10', 1],
    'A3': ['hi_perf', 1],
    'A6': ['hi_perf', 1],
    'AJET': ['alpha_jet', 1],
    'AT3': ['hi_perf', 1],
    'CKUO': ['hi_perf', 1],
    'EUFI': ['typhoon', 1],
    'SB39': ['sb39', 1],
    'MIR2': ['mirage', 1],
    'KFIR': ['mirage', 1],
    'F1': ['hi_perf', 1],
    'F111': ['hi_perf', 1],
    'F117': ['hi_perf', 1],
    'F14': ['hi_perf', 1],
    'F15': ['md_f15', 1],
    'F16': ['hi_perf', 1],
    'F18': ['f18', 1],
    'F18H': ['f18', 1],
    'F18S': ['f18', 1],
    'F22': ['f35', 1],
    'F22A': ['f35', 1],
    'F35': ['f35', 1],
    'VF35': ['f35', 1],
    'L159': ['l159', 1],
    'L39': ['l159', 1],
    'F4': ['hi_perf', 1],
    'F5': ['f5_tiger', 1],
    'HUNT': ['hunter', 1],
    'LANC': ['lancaster', 1],
    'B17': ['lancaster', 1],
    'B29': ['lancaster', 1],
    'J8A': ['hi_perf', 1],
    'J8B': ['hi_perf', 1],
    'JH7': ['hi_perf', 1],
    'LTNG': ['hi_perf', 1],
    'M346': ['hi_perf', 1],
    'METR': ['hi_perf', 1],
    'MG19': ['hi_perf', 1],
    'MG25': ['hi_perf', 1],
    'MG29': ['hi_perf', 1],
    'MG31': ['hi_perf', 1],
    'MG44': ['hi_perf', 1],
    'MIR4': ['hi_perf', 1],
    'MT2': ['hi_perf', 1],
    'Q5': ['hi_perf', 1],
    'RFAL': ['rafale', 1],
    'S3': ['hi_perf', 1],
    'S37': ['hi_perf', 1],
    'SR71': ['hi_perf', 1],
    'SU15': ['hi_perf', 1],
    'SU24': ['hi_perf', 1],
    'SU25': ['hi_perf', 1],
    'SU27': ['hi_perf', 1],
    'T22M': ['hi_perf', 1],
    'T4': ['hi_perf', 1],
    'TOR': ['tornado', 1],
    'A4': ['md_a4', 1],
    'TU22': ['hi_perf', 1],
    'VAUT': ['hi_perf', 1],
    'Y130': ['hi_perf', 1],
    'YK28': ['hi_perf', 1],
    'BE20': ['twin_large', 0.92],
    'IL62': ['il_62', 1],

    'MRF1': ['miragef1', 0.75],
    'M326': ['m326', 1],
    'M339': ['m326', 1],
    'FOUG': ['m326', 1],
    'T33': ['m326', 1],

    'A225': ['a225', 1],
    'A124': ['b707', 1.18],

    'SLCH': ['strato', 1],
    'WHK2': ['strato', 0.9],

    'C130': ['c130', 1.07],
    'C30J': ['c130', 1.07],

    'P3': ['p3_orion', 1],

    'PARA': ['para', 1],

    'DRON': ['uav', 1],
    'Q1': ['uav', 1],
    'Q4': ['uav', 1],
    'Q9': ['uav', 1],
    'Q25': ['uav', 1],
    'HRON': ['uav', 1],

    'A400': ['a400', 1],

    'V22F': ['v22_fast', 1],
    'V22': ['v22_slow', 1],
    'B609F': ['v22_fast', 0.86],
    'B609': ['v22_slow', 0.86],
    'H64': ['apache', 1],


    // 4 bladed heavy helicopters
    'H60': ['blackhawk', 1], // 11t
    'S92': ['blackhawk', 1], // 12t
    'NH90': ['blackhawk', 1], // 11t

    // Puma, Super Puma, Oryx, Cougar (ICAO'S AS32 & AS3B & PUMA)
    'AS32': ['puma', 1.03], // 9t
    'AS3B': ['puma', 1.03], // 9t
    'PUMA': ['puma', 1.03], // 9t

    'TIGR': ['tiger', 1.00],
    'MI24': ['mil24', 1.00],
    'AS65': ['dauphin', 0.85],
    'S76': ['dauphin', 0.86],
    'GAZL': ['gazelle', 1.00],
    'AS50': ['gazelle', 1.00],
    'AS55': ['gazelle', 1.00],
    'ALO2': ['gazelle', 1.00],
    'ALO3': ['gazelle', 1.00],

    'R22': ['helicopter', 0.92],
    'R44': ['helicopter', 0.94],
    'R66': ['helicopter', 0.98],

    // 5 bladed
    'EC55': ['s61', 0.94], // 5t
    'A169': ['s61', 0.94], // 5t
    'H160': ['s61', 0.95], // 6t
    'A139': ['s61', 0.96], // 7t
    'EC75': ['s61', 0.97], // 8t
    'A189': ['s61', 0.98], // 8.3t
    'A149': ['s61', 0.98], // 8.6t
    'S61': ['s61', 0.98], // 8.6t
    'S61R': ['s61', 1], // 10t
    'EC25': ['s61', 1.01], // 11t
    'EH10': ['s61', 1.04], // 14.5t (AW101)
    'H53': ['s61', 1.1], // 19t
    'H53S': ['s61', 1.1], // 19t

    'U2': ['u2', 1],
    'C2': ['c2', 1],
    'E2': ['c2', 1],
    'H47': ['chinook', 1],
    'H46': ['chinook', 1],
    'HAWK': ['bae_hawk', 1],

    'GYRO': ['gyrocopter', 1],
    'DLTA': ['verhees', 1],

    'B1': ['b1b_lancer', 1.0],
    'B52': ['b52', 1],
    'C17': ['c17', 1.25],
    'C5M': ['c5', 1.18],
    'E3TF': ['e3awacs', 0.88],
    'E3CF': ['e3awacs', 0.88],
    //
    'GLID': ['glider', 1],
    //Stemme
    'S6': ['glider', 1],
    'S10S': ['glider', 1],
    'S12': ['glider', 1],
    'S12S': ['glider', 1],
    //Schempp-Hirth
    'ARCE': ['glider', 1],
    'ARCP': ['glider', 1],
    'DISC': ['glider', 1],
    'DUOD': ['glider', 1],
    'JANU': ['glider', 1],
    'NIMB': ['glider', 1],
    'QINT': ['glider', 1],
    'VENT': ['glider', 1],
    'VNTE': ['glider', 1],
    //Schleicher
    'A20J': ['glider', 1],
    'A32E': ['glider', 1],
    'A32P': ['glider', 1],
    'A33E': ['glider', 1],
    'A33P': ['glider', 1],
    'A34E': ['glider', 1],
    'AS14': ['glider', 1],
    'AS16': ['glider', 1],
    'AS20': ['glider', 1],
    'AS21': ['glider', 1],
    'AS22': ['glider', 1],
    'AS24': ['glider', 1],
    'AS25': ['glider', 1],
    'AS26': ['glider', 1],
    'AS28': ['glider', 1],
    'AS29': ['glider', 1],
    'AS30': ['glider', 1],
    'AS31': ['glider', 1],
    //DG
    'DG80': ['glider', 1],
    'DG1T': ['glider', 1],
    'LS10': ['glider', 1],
    'LS9': ['glider', 1],
    'LS8': ['glider', 1],
    //Jonker
    'TS1J': ['glider', 1],
    //PIK
    'PK20': ['glider', 1],
    //LAK
    'LK17': ['glider', 1],
    'LK19': ['glider', 1],
    'LK20': ['glider', 1],

    'ULAC': ['cessna', 0.92],
    'EV97': ['cessna', 0.92],
    'FDCT': ['cessna', 0.92],
    'WT9': ['cessna', 0.92],
    'PIVI': ['cessna', 0.92],
    'FK9': ['cessna', 0.92],
    'AVID': ['cessna', 0.92],
    'NG5': ['cessna', 0.92],
    'PNR3': ['cessna', 0.92],
    'TL20': ['cessna', 0.92],

    'SR20': ['cirrus_sr22', 1],
    'SR22': ['cirrus_sr22', 1],
    'S22T': ['cirrus_sr22', 1],
    'VEZE': ['rutan_veze', 1],
    'VELO': ['rutan_veze', 1.04],

    'PRTS': ['rutan_veze', 1.3], // approximation for canard configuration

    'PA24': ['pa24', 1],

};

// Layer 2: ICAO type description ("L2J") with optional wake-turbulence
// category suffix ("L2J-H"). Composite keys are tried before bare keys
// when both class and wtc are present, mirroring tar1090's
// getBaseMarker ordering. Includes two synthesised bare entries for
// classes that tar1090 only ships as WTC-suffixed (L2J, L3J), so an
// aircraft with a known class but missing WTC still resolves to a
// sensible shape rather than dropping to category/unknown.
export const TYPE_DESCRIPTION = {
    'H':     ['helicopter',   1],
    'G':     ['gyrocopter',   1],

    'L1P':   ['cessna',       1],
    'A1P':   ['cessna',       1],
    'L1T':   ['single_turbo', 1],
    'L1J':   ['hi_perf',      0.92],

    'L2P':   ['twin_small',   1],
    'A2P':   ['twin_large',   0.96],
    'A2P-M': ['twin_large',   1.12],
    'L2T':   ['twin_large',   0.96],
    'L2T-M': ['twin_large',   1.12],
    'A2T':   ['twin_large',   0.96],
    'A2T-M': ['twin_large',   1.06],

    'L1J-L': ['jet_nonswept', 1],     // < 7t
    'L2J-L': ['jet_nonswept', 1],     // < 7t
    'L2J-M': ['airliner',     1],     // < 136t
    'L2J-H': ['heavy_2e',     0.96],  // > 136t

    'L3J-H': ['md11',         1],     // > 136t

    'L4T':   ['c130',         0.96],
    'L4T-M': ['c130',         1],
    'L4T-H': ['c130',         1.07],

    'L4J':   ['b707',         0.8],
    'L4J-M': ['b707',         0.8],
    'L4J-H': ['b707',         1],

    // Synthesised bare entries: tar1090 ships only WTC-suffixed
    // entries for L2J and L3J. When WTC is unknown but the bare
    // class is, fall back to the most-common WTC variant rather
    // than dropping through to category/unknown.
    'L2J':   ['airliner',     1],     // mirrors L2J-M (medium twinjet)
    'L3J':   ['md11',         1],     // mirrors L3J-H (only L3J variant tar1090 ships)
};

// Layer 3: single-character type-description fall-back. Only "H" and
// "G" are unambiguous enough to map; "L" and "A" cover everything
// from a Cessna to a 747 and deliberately drop through to layer 4.
export const TYPE_DESCRIPTION_FIRSTCHAR = {
    'H': ['helicopter', 1],
    'G': ['gyrocopter', 1],
};

// Layer 4: ADS-B emitter category. Keys are the Aeromux C# enum
// names (Light, Small, ...) emitted by JsonStringEnumConverter, not
// the ADS-B Ax letter codes. Set "B" categories (glider, balloon,
// ultralight, UAV) are intentionally omitted — they aren't surfaced
// from the wire format today.
export const CATEGORY = {
    'Light':           ['cessna',     1],     // A1
    'Small':           ['jet_swept',  0.94],  // A2
    'Large':           ['airliner',   0.96],  // A3
    'HighVortexLarge': ['airliner',   1],     // A4
    'Heavy':           ['heavy_2e',   0.92],  // A5
    'HighPerformance': ['hi_perf',    0.94],  // A6
    'Rotorcraft':      ['helicopter', 1],     // A7
};

/**
 * Resolve an aircraft to a shape name and per-type scale, walking the
 * five resolver layers top-to-bottom. The `resolvedVia` field on the
 * return value identifies which layer fired; used for debug logging
 * and tooltips.
 *
 * @param {string|null} typeDesignator
 *        ICAO type designator (e.g. "A320"). Case-insensitive —
 *        upper-cased before lookup.
 * @param {string|null} typeIcaoClass
 *        ICAO type description (e.g. "L2J"). Must be the bare 3-char
 *        (or 1-char) form; pre-suffixed strings like "L2J-M" are
 *        rejected by the layer-2 length guard and fall through.
 * @param {string|null} typeWtc
 *        Single-character wake-turbulence code ("L" / "M" / "H" / "J").
 * @param {string|null} emitterCategory
 *        Aeromux C# enum name (e.g. "Heavy"), not the ADS-B Ax letter.
 * @returns {{shapeName: string, scale: number, resolvedVia: string}}
 */
export function resolveShape(typeDesignator, typeIcaoClass, typeWtc, emitterCategory) {
    // Memoize on the input tuple. updateMarkers re-resolves every aircraft on
    // every marker rebuild (which can fire several times within a one-second
    // push burst), but the inputs for a given airframe essentially never change.
    // Real ICAO type designators/descriptions are alphanumeric, so '|' is a safe
    // key separator. Callers treat the result as immutable (destructure only), so
    // returning the shared cached object is fine. The cache is naturally bounded
    // by the count of distinct real type tuples.
    const cacheKey = `${typeDesignator}|${typeIcaoClass}|${typeWtc}|${emitterCategory}`;
    const cached = resolveShapeCache.get(cacheKey);
    if (cached) return cached;

    const result = resolveShapeUncached(typeDesignator, typeIcaoClass, typeWtc, emitterCategory);
    resolveShapeCache.set(cacheKey, result);
    return result;
}

const resolveShapeCache = new Map();

// Uncached core; see resolveShape's JSDoc for the layer semantics and contract.
function resolveShapeUncached(typeDesignator, typeIcaoClass, typeWtc, emitterCategory) {
    if (typeDesignator) {
        const key = typeDesignator.toUpperCase();
        const v = TYPE_DESIGNATOR[key];
        if (v) return { shapeName: v[0], scale: v[1], resolvedVia: 'designator' };
    }
    if (typeIcaoClass && typeIcaoClass.length === 3) {
        if (typeWtc && typeWtc.length === 1) {
            const composite = `${typeIcaoClass}-${typeWtc}`;
            const v = TYPE_DESCRIPTION[composite];
            if (v) return { shapeName: v[0], scale: v[1], resolvedVia: 'description-3-wtc' };
        }
        const v = TYPE_DESCRIPTION[typeIcaoClass];
        if (v) return { shapeName: v[0], scale: v[1], resolvedVia: 'description-3' };
        const f = TYPE_DESCRIPTION_FIRSTCHAR[typeIcaoClass.charAt(0)];
        if (f) return { shapeName: f[0], scale: f[1], resolvedVia: 'description-1' };
    } else if (typeIcaoClass) {
        // Single-char "H" or "G" arriving directly in TypeIcaoClass.
        const f = TYPE_DESCRIPTION_FIRSTCHAR[typeIcaoClass.charAt(0)];
        if (f) return { shapeName: f[0], scale: f[1], resolvedVia: 'description-1' };
    }
    if (emitterCategory) {
        const v = CATEGORY[emitterCategory];
        if (v) return { shapeName: v[0], scale: v[1], resolvedVia: 'category' };
    }
    return { shapeName: 'unknown', scale: 1, resolvedVia: 'fallback' };
}

// Module-load integrity assertion: every shape name referenced from
// any lookup table must exist in SHAPES. Catches drift if a shape
// is removed from AircraftShapes.js without removing or
// redirecting the lookup entries that point at it.
for (const [tableName, table] of [
    ['TYPE_DESIGNATOR',           TYPE_DESIGNATOR],
    ['TYPE_DESCRIPTION',          TYPE_DESCRIPTION],
    ['TYPE_DESCRIPTION_FIRSTCHAR', TYPE_DESCRIPTION_FIRSTCHAR],
    ['CATEGORY',                  CATEGORY],
]) {
    for (const [key, value] of Object.entries(table)) {
        const shapeName = value[0];
        if (!(shapeName in SHAPES)) {
            throw new Error(
                `[AircraftIconResolver] ${tableName}['${key}'] references missing shape '${shapeName}'`
            );
        }
    }
}
if (!('unknown' in SHAPES)) {
    throw new Error("[AircraftIconResolver] SHAPES.unknown is required as the universal fallback");
}
