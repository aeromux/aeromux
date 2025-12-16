// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Registry mapping Mode S message types to their corresponding tracking handlers.
/// Provides fast O(1) lookup for message processing using the Strategy pattern.
/// </summary>
/// <remarks>
/// This registry is initialized once during AircraftStateTracker construction.
/// All handlers are registered in the constructor for centralized management.
/// Adding a new message type handler requires only:
/// 1. Create the handler class implementing ITrackingHandler
/// 2. Register it in this constructor
/// No changes needed to AircraftStateTracker itself (Open/Closed Principle).
/// </remarks>
public sealed class TrackingHandlerRegistry
{
    private readonly Dictionary<Type, ITrackingHandler> _handlers = new();

    /// <summary>
    /// Initializes the registry and registers all tracking handlers.
    /// Handlers are registered by their MessageType for fast lookup.
    /// </summary>
    public TrackingHandlerRegistry()
    {
        // Identification and status handlers
        Register(new AircraftIdentificationHandler());
        Register(new AircraftStatusHandler());

        // Position handlers
        Register(new AirbornePositionHandler());
        Register(new SurfacePositionHandler());

        // Velocity handler
        Register(new AirborneVelocityHandler());

        // Operational status handler (TC 31)
        Register(new OperationalStatusHandler());

        // Target state and status handler (TC 29)
        Register(new TargetStateAndStatusHandler());

        // Comm-B handlers (DF 20, DF 21)
        Register(new CommBAltitudeReplyHandler());
        Register(new CommBIdentityReplyHandler());

        // Surveillance reply handlers
        Register(new SurveillanceAltitudeReplyHandler());
        Register(new SurveillanceIdentityReplyHandler());
        Register(new ShortAirAirSurveillanceHandler());
    }

    /// <summary>
    /// Registers a handler for its declared message type.
    /// </summary>
    private void Register(ITrackingHandler handler) => _handlers[handler.MessageType] = handler;

    /// <summary>
    /// Gets the handler for a specific message type.
    /// </summary>
    /// <param name="messageType">The Mode S message type to look up</param>
    /// <returns>The handler for this message type, or null if no handler is registered</returns>
    /// <remarks>
    /// Returns null for message types that don't affect tracking state
    /// (e.g., AllCallReply, CommB messages without relevant data).
    /// </remarks>
    public ITrackingHandler? GetHandler(Type messageType)
    {
        _handlers.TryGetValue(messageType, out ITrackingHandler? handler);
        return handler;
    }

    /// <summary>
    /// Gets the number of registered handlers.
    /// </summary>
    public int Count => _handlers.Count;
}
