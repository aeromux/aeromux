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

import { h, Fragment } from 'preact';
import { useState } from 'preact/hooks';
import { DetailSection } from './DetailSection.jsx';
import { DetailField } from './DetailField.jsx';
import { FlightProfileChart } from './FlightProfileChart.jsx';
import { formatAltitude, formatSpeed, haversineDistance, convertDistance } from '../Services/UnitConversion.js';

function fmtBool(v) { return v == null ? null : v ? 'Yes' : 'No'; }
function fmtTime(v) {
    if (!v) return null;
    return new Date(v).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true });
}
function fmtAgo(v) {
    if (!v) return null;
    const seconds = Math.round((Date.now() - new Date(v).getTime()) / 1000);
    if (seconds < 0) return '0s ago';
    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${seconds % 60}s ago`;
    return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m ago`;
}
function fmtDeg(v) { return v != null ? `${v.toFixed(2)}\u00B0` : null; }
function fmtNum(v, suffix) { return v != null ? `${v}${suffix || ''}` : null; }

export function AircraftDetail({ detail, expired, units, receiverLocation, stateHistory, onBack }) {
    const [showMore, setShowMore] = useState({});
    const toggleMore = (key) => setShowMore(prev => ({ ...prev, [key]: !prev[key] }));

    if (!detail) {
        return (
            <div class="detail-scroll">
                <button class="back-button" onClick={onBack}>&larr; Back</button>
                <div class="aircraft-list-empty">Loading...</div>
            </div>
        );
    }

    const id = detail.Identification || {};
    const db = detail.DatabaseRecord;
    const st = detail.Status || {};
    const pos = detail.Position || {};
    const vel = detail.VelocityAndDynamics || {};
    const ap = detail.Autopilot || {};
    const met = detail.Meteorology;
    const acas = detail.Acas || {};
    const cap = detail.Capabilities || {};
    const dq = detail.DataQuality || {};

    // Distance from receiver
    let distStr = null;
    if (pos.Coordinate && receiverLocation) {
        const d = haversineDistance(
            receiverLocation.lat, receiverLocation.lon,
            pos.Coordinate.Latitude, pos.Coordinate.Longitude
        );
        const conv = convertDistance(d, units.distance);
        distStr = `${conv.value} ${conv.label}`;
    } else if (pos.Coordinate && !receiverLocation) {
        distStr = 'N/A (no receiver location)';
    }

    const callsign = id.Callsign || id.ICAO || '';
    const operator = db ? db.OperatorName : null;

    return (
        <div class="detail-scroll">
            <button class="back-button" onClick={onBack}>&larr; Back</button>
            {expired && <div class="expired-banner">[EXPIRED]</div>}

            <div class="detail-header">
                <div class="detail-callsign">{callsign}</div>
                {operator && <div class="detail-operator">{operator}</div>}
            </div>

            {/* 1. Identification */}
            <DetailSection title="Identification" defaultExpanded={true}>
                <div class="section-fields">
                    <DetailField label="ICAO Address" value={id.ICAO} />
                    <DetailField label="Callsign" value={id.Callsign} />
                    <DetailField label="Category" value={id.Category} />
                    <DetailField label="Squawk" value={id.Squawk} />
                    <DetailField label="Emergency" value={id.EmergencyState} />
                    <DetailField label="Flight Status" value={id.FlightStatus} />
                </div>
            </DetailSection>

            {/* 2. Aircraft Database */}
            <DetailSection title="Aircraft Database" defaultExpanded={true}>
                {db === null && detail.Identification ? (
                    <div class="db-disabled-message">Aircraft database not enabled</div>
                ) : db === undefined ? (
                    <div class="db-disabled-message">Aircraft database not enabled</div>
                ) : (
                    <div class="section-fields">
                        <DetailField label="Registration" value={db.Registration} />
                        <DetailField label="Country" value={db.Country} />
                        <DetailField label="Operator" value={db.OperatorName} />
                        <DetailField label="Manufacturer Name" value={db.ManufacturerName} />
                        <DetailField label="Type Description" value={db.TypeDescription} />
                        <DetailField label="Aircraft Model" value={db.Model} />
                        {showMore.db && (
                            <>
                                <DetailField label="Manufacturer ICAO" value={db.ManufacturerIcao} />
                                <DetailField label="Type Class ICAO" value={db.TypeIcaoClass} />
                                <DetailField label="Type Designator" value={db.TypeCode} />
                                <DetailField label="FAA PIA" value={fmtBool(db.Pia)} />
                                <DetailField label="FAA LADD" value={fmtBool(db.Ladd)} />
                                <DetailField label="Military" value={fmtBool(db.Military)} />
                            </>
                        )}
                    </div>
                )}
                {db && <div class="see-more" onClick={() => toggleMore('db')}>{showMore.db ? 'Show less' : 'See more'}</div>}
            </DetailSection>

            {/* 3. Flight Profile */}
            <DetailSection title="Flight Profile" defaultExpanded={true}>
                {stateHistory === null ? (
                    <div class="flight-profile-empty">Loading...</div>
                ) : stateHistory.enabled === false ? (
                    <div class="flight-profile-empty">State history not enabled</div>
                ) : (
                    <FlightProfileChart entries={stateHistory.entries} units={units} />
                )}
            </DetailSection>

            {/* 4. Status */}
            <DetailSection title="Status" defaultExpanded={true}>
                <div class="section-fields">
                    <DetailField label="First Seen" value={fmtTime(st.FirstSeen)} />
                    <DetailField label="Last Seen" value={fmtAgo(st.LastSeen)} />
                    <DetailField label="Total Messages" value={fmtNum(st.TotalMessages)} />
                    <DetailField label="Position Messages" value={fmtNum(st.PositionMessages)} />
                    <DetailField label="Velocity Messages" value={fmtNum(st.VelocityMessages)} />
                    <DetailField label="ID Messages" value={fmtNum(st.IdentificationMessages)} />
                    <DetailField label="Signal Strength" value={st.SignalStrength != null ? `${st.SignalStrength.toFixed(1)} dB` : null} />
                </div>
            </DetailSection>

            {/* 4. Position */}
            <DetailSection title="Position" defaultExpanded={true}>
                <div class="section-fields">
                    <DetailField label="Latitude" value={pos.Coordinate ? fmtDeg(pos.Coordinate.Latitude) : null} />
                    <DetailField label="Longitude" value={pos.Coordinate ? fmtDeg(pos.Coordinate.Longitude) : null} />
                    <DetailField label="Distance" value={distStr} />
                    <DetailField label="Barometric Altitude" value={formatAltitude(pos.BarometricAltitude, units.altitude)} />
                    <DetailField label="Geometric Altitude" value={formatAltitude(pos.GeometricAltitude, units.altitude)} />
                    <DetailField label="GNSS Offset" value={fmtNum(pos.GeometricBarometricDelta, ' ft')} />
                    {showMore.pos && (
                        <>
                            <DetailField label="On Ground" value={fmtBool(pos.IsOnGround)} />
                            <DetailField label="Movement" value={pos.MovementCategory} />
                            <DetailField label="Position Source" value={pos.Source} />
                            <DetailField label="Had MLAT Position" value={fmtBool(pos.HadMlatPosition)} />
                            <DetailField label="Last Update" value={fmtAgo(pos.LastUpdate)} />
                        </>
                    )}
                </div>
                <div class="see-more" onClick={() => toggleMore('pos')}>{showMore.pos ? 'Show less' : 'See more'}</div>
            </DetailSection>

            {/* 5. Velocity & Dynamics */}
            <DetailSection title="Velocity & Dynamics" defaultExpanded={true}>
                <div class="section-fields">
                    <DetailField label="Speed" value={formatSpeed(vel.Speed, units.speed)} />
                    <DetailField label="Track" value={fmtDeg(vel.Track)} />
                    <DetailField label="Heading" value={fmtDeg(vel.Heading)} />
                    <DetailField label="Vertical Rate" value={fmtNum(vel.VerticalRate, ' ft/min')} />
                    {showMore.vel && (
                        <>
                            <DetailField label="IAS (BDS 6,0)" value={formatSpeed(vel.IndicatedAirspeed, units.speed)} />
                            <DetailField label="TAS (BDS 5,0)" value={formatSpeed(vel.TrueAirspeed, units.speed)} />
                            <DetailField label="Ground Speed" value={formatSpeed(vel.GroundSpeed, units.speed)} />
                            <DetailField label="Mach" value={vel.MachNumber != null ? vel.MachNumber.toFixed(3) : null} />
                            <DetailField label="Track (BDS 5,0)" value={fmtDeg(vel.TrackAngle)} />
                            <DetailField label="Magnetic Heading" value={fmtDeg(vel.MagneticHeading)} />
                            <DetailField label="True Heading" value={fmtDeg(vel.TrueHeading)} />
                            <DetailField label="Magnetic Declination" value={fmtDeg(vel.MagneticDeclination)} />
                            <DetailField label="Heading Type" value={vel.HeadingType} />
                            <DetailField label="Horizontal Ref" value={vel.HorizontalReference} />
                            <DetailField label="Baro Vertical Rate" value={fmtNum(vel.BarometricVerticalRate, ' ft/min')} />
                            <DetailField label="Inertial Vertical Rate" value={fmtNum(vel.InertialVerticalRate, ' ft/min')} />
                            <DetailField label="Roll Angle" value={fmtDeg(vel.RollAngle)} />
                            <DetailField label="Track Rate" value={vel.TrackRate != null ? `${vel.TrackRate.toFixed(2)}\u00B0/s` : null} />
                            <DetailField label="Speed On Ground" value={formatSpeed(vel.SpeedOnGround, units.speed)} />
                            <DetailField label="Track On Ground" value={fmtDeg(vel.TrackOnGround)} />
                            <DetailField label="Last Update" value={fmtAgo(vel.LastUpdate)} />
                        </>
                    )}
                </div>
                <div class="see-more" onClick={() => toggleMore('vel')}>{showMore.vel ? 'Show less' : 'See more'}</div>
            </DetailSection>

            {/* 6. Autopilot (collapsed) */}
            <DetailSection title="Autopilot" defaultExpanded={false}>
                <div class="section-fields">
                    <DetailField label="Autopilot Engaged" value={fmtBool(ap.AutopilotEngaged)} />
                    <DetailField label="Selected Altitude" value={formatAltitude(ap.SelectedAltitude, units.altitude)} />
                    <DetailField label="Altitude Source" value={ap.AltitudeSource} />
                    <DetailField label="Selected Heading" value={fmtDeg(ap.SelectedHeading)} />
                    <DetailField label="Baro Pressure" value={ap.BarometricPressureSetting != null ? `${ap.BarometricPressureSetting.toFixed(1)} mb` : null} />
                    <DetailField label="Vertical Mode" value={ap.VerticalMode} />
                    <DetailField label="Horizontal Mode" value={ap.HorizontalMode} />
                    <DetailField label="VNAV" value={fmtBool(ap.VNAVMode)} />
                    <DetailField label="LNAV" value={fmtBool(ap.LNAVMode)} />
                    <DetailField label="Altitude Hold" value={fmtBool(ap.AltitudeHoldMode)} />
                    <DetailField label="Approach Mode" value={fmtBool(ap.ApproachMode)} />
                    <DetailField label="Last Update" value={fmtAgo(ap.LastUpdate)} />
                </div>
            </DetailSection>

            {/* 7. Meteorology (collapsed) */}
            <DetailSection title="Meteorology" defaultExpanded={false}>
                {met === null ? (
                    <div class="section-fields">
                        <DetailField label="Wind Speed" value={null} />
                        <DetailField label="Wind Direction" value={null} />
                        <DetailField label="Temperature" value={null} />
                        <DetailField label="Pressure" value={null} />
                    </div>
                ) : (
                    <div class="section-fields">
                        <DetailField label="Wind Speed" value={fmtNum(met.WindSpeed, ' kts')} />
                        <DetailField label="Wind Direction" value={fmtDeg(met.WindDirection)} />
                        <DetailField label="Total Air Temp" value={met.TotalAirTemperature != null ? `${met.TotalAirTemperature.toFixed(1)} \u00B0C` : null} />
                        <DetailField label="Static Air Temp" value={met.StaticAirTemperature != null ? `${met.StaticAirTemperature.toFixed(1)} \u00B0C` : null} />
                        <DetailField label="Pressure" value={met.Pressure != null ? `${met.Pressure.toFixed(1)} mb` : null} />
                        <DetailField label="Radio Height" value={fmtNum(met.RadioHeight, ' ft')} />
                        <DetailField label="Turbulence" value={met.Turbulence} />
                        <DetailField label="Wind Shear" value={met.WindShear} />
                        <DetailField label="Microburst" value={met.Microburst} />
                        <DetailField label="Icing" value={met.Icing} />
                        <DetailField label="Wake Vortex" value={met.WakeVortex} />
                        <DetailField label="Humidity" value={met.Humidity != null ? `${met.Humidity.toFixed(1)}%` : null} />
                        <DetailField label="Figure of Merit" value={fmtNum(met.FigureOfMerit)} />
                        <DetailField label="Last Update" value={fmtAgo(met.LastUpdate)} />
                    </div>
                )}
            </DetailSection>

            {/* 8. ACAS/TCAS (collapsed) */}
            <DetailSection title="ACAS/TCAS" defaultExpanded={false}>
                <div class="section-fields">
                    <DetailField label="TCAS Operational" value={fmtBool(acas.TCASOperational)} />
                    <DetailField label="Sensitivity Level" value={fmtNum(acas.SensitivityLevel)} />
                    <DetailField label="Cross-Link" value={fmtBool(acas.CrossLinkCapability)} />
                    <DetailField label="Reply Info" value={acas.ReplyInformation} />
                    <DetailField label="RA Active (BDS 3,0)" value={fmtBool(acas.TCASRAActive_BDS30)} />
                    <DetailField label="RA Active (TC 31)" value={fmtBool(acas.TCASRAActive_TC31)} />
                    <DetailField label="RA Terminated" value={fmtBool(acas.ResolutionAdvisoryTerminated)} />
                    <DetailField label="Multiple Threats" value={fmtBool(acas.MultipleThreatEncounter)} />
                    <DetailField label="RAC: Not Below" value={fmtBool(acas.RACNotBelow)} />
                    <DetailField label="RAC: Not Above" value={fmtBool(acas.RACNotAbove)} />
                    <DetailField label="RAC: Not Left" value={fmtBool(acas.RACNotLeft)} />
                    <DetailField label="RAC: Not Right" value={fmtBool(acas.RACNotRight)} />
                    <DetailField label="Last Update" value={fmtAgo(acas.LastUpdate)} />
                </div>
            </DetailSection>

            {/* 9. Capabilities (collapsed) */}
            <DetailSection title="Capabilities" defaultExpanded={false}>
                <div class="section-fields">
                    <DetailField label="ADS-B Version" value={cap.AdsbVersion} />
                    <DetailField label="Transponder Level" value={cap.TransponderLevel} />
                    <DetailField label="ADS-B 1090ES" value={fmtBool(cap.ADSB1090ES)} />
                    <DetailField label="UAT 978" value={fmtBool(cap.UAT978Support)} />
                    {showMore.cap && (
                        <>
                            <DetailField label="Low Power 1090ES" value={fmtBool(cap.LowPower1090ES)} />
                            <DetailField label="TCAS Capability" value={fmtBool(cap.TCASCapability)} />
                            <DetailField label="CDTI" value={fmtBool(cap.CockpitDisplayTraffic)} />
                            <DetailField label="Air Ref Velocity" value={fmtBool(cap.AirReferencedVelocity)} />
                            <DetailField label="Target State" value={fmtBool(cap.TargetStateReporting)} />
                            <DetailField label="Trajectory Change" value={cap.TrajectoryChangeLevel} />
                            <DetailField label="Position Offset" value={fmtBool(cap.PositionOffsetApplied)} />
                            <DetailField label="IDENT Active" value={fmtBool(cap.IdentSwitchActive)} />
                            <DetailField label="ATC Services" value={fmtBool(cap.ReceivingATCServices)} />
                            <DetailField label="Dimensions" value={cap.Dimensions} />
                            <DetailField label="GPS Lateral" value={cap.GPSLateralOffset} />
                            <DetailField label="GPS Longitudinal" value={cap.GPSLongitudinalOffset} />
                            <DetailField label="Downlink Request" value={fmtNum(cap.DownlinkRequest)} />
                            <DetailField label="Utility Message" value={cap.UtilityMessage} />
                            <DetailField label="Data Link Capability" value={cap.DataLinkCapability} />
                            <DetailField label="Supported BDS Registers" value={cap.SupportedBDSRegisters} />
                            <DetailField label="Last Update" value={fmtAgo(cap.LastUpdate)} />
                        </>
                    )}
                </div>
                <div class="see-more" onClick={() => toggleMore('cap')}>{showMore.cap ? 'Show less' : 'See more'}</div>
            </DetailSection>

            {/* 10. Data Quality (collapsed) */}
            <DetailSection title="Data Quality" defaultExpanded={false}>
                <div class="section-fields">
                    <DetailField label="Antenna (TC 9-18)" value={dq.Antenna_TC918} />
                    <DetailField label="Antenna (TC 31)" value={dq.Antenna_TC31} />
                    <DetailField label="NACp (TC 9-18)" value={dq.NACp_TC918} />
                    <DetailField label="NACp (TC 29)" value={dq.NACp_TC29} />
                    {showMore.dq && (
                        <>
                            <DetailField label="NACv (TC 19)" value={dq.NACv_TC19} />
                            <DetailField label="NACv (TC 31)" value={dq.NACv_TC31} />
                            <DetailField label="NICbaro (TC 9-18)" value={dq.NICbaro_TC918} />
                            <DetailField label="NICbaro (TC 29)" value={dq.NICbaro_TC29} />
                            <DetailField label="NIC Supplement A" value={fmtBool(dq.NICSupplementA)} />
                            <DetailField label="NIC Supplement C" value={fmtBool(dq.NICSupplementC)} />
                            <DetailField label="SIL (TC 9-18)" value={dq.SIL_TC918} />
                            <DetailField label="SIL (TC 29)" value={dq.SIL_TC29} />
                            <DetailField label="SIL Supplement" value={dq.SILSupplement} />
                            <DetailField label="GVA" value={dq.GeometricVerticalAccuracy} />
                            <DetailField label="SDA" value={dq.SystemDesignAssurance} />
                            <DetailField label="Last Update" value={fmtAgo(dq.LastUpdate)} />
                        </>
                    )}
                </div>
                <div class="see-more" onClick={() => toggleMore('dq')}>{showMore.dq ? 'Show less' : 'See more'}</div>
            </DetailSection>
        </div>
    );
}
