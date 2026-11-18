using EEBUS.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.Events
{
    public class DeviceConnectionRequestEventArgs
    {
        public SKI Ski { get; init; }

        /// <summary>
        /// Set by an <see cref="EEBUSManager"/> subscriber to refuse this inbound connection request
        /// before the WebSocket upgrade is accepted - used for SHIP 12.2.3's ("Prevent Double
        /// Connections with SKI Comparison", 12.2.2 in SHIP TS v1.0.1) requirement that when both an
        /// inbound and outbound connection exist for the same peer, only the higher-SKI side's
        /// connection may survive.
        /// </summary>
        public bool Reject { get; set; }

        public DeviceConnectionRequestEventArgs(SKI ski)
        {
            Ski = ski;
        }
    }
}
