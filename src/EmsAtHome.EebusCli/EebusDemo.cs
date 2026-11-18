using EEBUS;
using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.SPINE.Commands;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EmsAtHome.EebusCli;

/// <summary>
/// Small interactive console demo built on top of upstream EEBUS.Net's own ConsoleDemo (see
/// src/vendor/EEBUS.Net/ORIGIN.md), extended with commands that exercise SPINE at the application
/// level: connecting prints everything discovery found (use cases, features, functions, LoadControl
/// limits), plus reading a numeric measurement and writing a power limit. Every SHIP frame and
/// decoded SPINE datagram is visible via the "EEBUS.Ship" / "EEBUS.Spine" loggers configured in
/// appsettings.json.
/// </summary>
public sealed class EebusDemo
{
    private readonly ILoggerFactory _loggerFactory;
    private EEBUSManager? _manager;

    public EebusDemo(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public async Task RunAsync(Settings settings, IReadOnlyList<string> trustedSkis)
    {
        _manager = new EEBUSManager(
            settings,
            logger: _loggerFactory.CreateLogger("EEBUS.Ship"),
            spineLogger: _loggerFactory.CreateLogger("EEBUS.Spine"));

        _manager.Start();

        Console.WriteLine("[EEBus CLI Demo]");
        Console.WriteLine($"Local SKI: {ExtractSki(_manager.GetLocal())}\n");

        // The trust list (AddTrustedSki) only lives in this process's memory, not persisted across
        // restarts - unlike pairing on the remote device's own side, which does persist. Trusting
        // known SKIs from config at startup avoids re-typing "ski <ski>" every single run.
        foreach (string ski in trustedSkis)
        {
            _manager.AddTrustedSki(ski);
            Console.WriteLine($"Trusted SKI {ski} (from config).");
        }
        if (trustedSkis.Count > 0) Console.WriteLine();

        PrintHelp();

        while (true)
        {
            Console.Write(":> ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens[0] == "exit") break;

            try
            {
                switch (tokens[0])
                {
                    case "help": PrintHelp(); break;
                    case "remotes": PrintRemotes(); break;
                    case "ski":
                        string trustedSki = tokens.ElementAtOrDefault(1) ?? string.Empty;
                        _manager.AddTrustedSki(trustedSki);
                        Console.WriteLine($"Trusted SKI {trustedSki}.");
                        break;
                    case "connect": await ConnectAsync(tokens); break;
                    case "read": await ReadValueAsync(tokens); break;
                    case "write": await WriteLimitAsync(tokens); break;
                    default: Console.WriteLine("Unknown command. Type 'help' for a list."); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        _manager.Dispose();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  remotes             list devices found via mDNS, with an index for the commands below");
        Console.WriteLine("  ski <ski>           trust a remote SKI so it may pair/connect (SKIs from Eebus:TrustedSkis are trusted automatically at startup)");
        Console.WriteLine("  connect <index>     connect to remotes[index] and print everything SPINE discovery found (query/browsing demo)");
        Console.WriteLine("  read <index>        read energy consumed + the current power limit (read-a-value demo)");
        Console.WriteLine("  write <index> <W>   write a new max charging power limit in Watts (write-a-value demo)");
        Console.WriteLine("  exit                quit");
    }

    private static string ExtractSki(JsonObject local)
    {
        if (local.TryGetPropertyValue("ski", out JsonNode? node) && node != null)
            return Regex.Replace(node.ToString(), @"\s+", string.Empty);
        return "unknown";
    }

    private void PrintRemotes()
    {
        var remotes = _manager!.GetDiscoveredDevices().ToList();
        if (remotes.Count == 0)
        {
            Console.WriteLine("No devices found yet - mDNS discovery runs in the background, try again shortly.");
            return;
        }

        for (int i = 0; i < remotes.Count; i++)
        {
            var status = _manager.GetConnectionStatus(remotes[i].Ski);
            Console.WriteLine($"[{i}] {remotes[i].Name} ski={remotes[i].Ski} status={status}");
        }
    }

    private async Task ConnectAsync(string[] tokens)
    {
        var remote = ResolveRemote(tokens);
        if (remote == null) return;

        bool connected = await _manager!.TryConnectAsync(remote.Ski);
        if (connected)
        {
            Console.WriteLine("Connected.");
        }
        else if (_manager.GetConnectionStatus(remote.Ski) != DeviceConnectionStatus.Unknown)
        {
            // TryConnectAsync returns false whenever a connection already exists (see
            // EEBUSManager.cs) - including one the remote itself just opened to us inbound, which
            // can win this race since the remote may dial in as soon as it sees our mDNS
            // advertisement. Already connected either way, so proceed instead of reporting failure.
            Console.WriteLine("Already connected (remote connected to us first).");
        }
        else
        {
            Console.WriteLine("Failed to connect.");
            return;
        }

        // NodeDiscoveryCompleted -> UseCaseDiscoveryCompleted, then the automatic per-feature
        // binding/reads, all happen asynchronously in the background after TryConnectAsync returns -
        // wait for discovery to finish (or a 3s timeout) before printing what was found, instead of
        // printing an empty/partial tree.
        for (int i = 0; i < 30; i++)
        {
            if (_manager.GetConnectionStatus(remote.Ski) == DeviceConnectionStatus.UseCaseDiscoveryCompleted)
                break;
            await Task.Delay(100);
        }

        // UseCaseDiscoveryCompleted only means the use-case/feature tree is known - the per-feature
        // reads Connection.ReadAndSubscribe() then fires (LoadControl, ElectricalConnection
        // characteristics, DeviceConfiguration key values, Bill, Measurement) are fire-and-forget
        // (PushDataMessage, not awaited), so their replies can still be in flight at this point.
        // Without this, PrintDiscoveryDetails's LoadControl/ElectricalConnection/DeviceConfiguration
        // sections print empty even though the same live RemoteDevice object shows the data moments
        // later (e.g. if 'read' is run right after 'connect'). No completion signal is exposed for
        // these, so a short fixed extra wait is the pragmatic fix for this CLI.
        await Task.Delay(1500);

        Console.WriteLine();
        PrintDiscoveryDetails(remote.Ski);
    }

    /// <summary>
    /// Prints everything SPINE discovery already found on the remote after connecting: every
    /// entity's use cases, and for every feature the concrete functions available on it (read/write),
    /// plus the actual LoadControl limit entries once known. All of this was already fetched during
    /// connect (NodeManagementDetailedDiscoveryData + the automatic per-feature reads/subscribes) -
    /// this only formats what's already in memory, no extra network round-trip.
    /// </summary>
    private void PrintDiscoveryDetails(string ski)
    {
        var remote = _manager!.Devices.GetRemotes().FirstOrDefault(r => r.SKI.ToString() == ski);
        if (remote == null)
        {
            Console.WriteLine("No discovery data yet - run 'connect <index>' first.");
            return;
        }

        Console.WriteLine($"{remote.Name} ({remote.SKI})");
        foreach (Entity entity in remote.Entities)
        {
            Console.WriteLine($"  Entity [{string.Join('.', entity.Index)}] {entity.Type}");

            foreach (var info in entity.UseCaseInformations)
            {
                foreach (var useCase in info.useCaseSupport)
                {
                    string scenarios = string.Join(',', useCase.scenarioSupport ?? []);
                    Console.WriteLine(
                        $"    Use case: {useCase.useCaseName} v{useCase.useCaseVersion} " +
                        $"(actor={info.actor}, available={useCase.useCaseAvailable}, scenarios=[{scenarios}])");
                }
            }

            foreach (Feature feature in entity.Features)
            {
                Console.WriteLine($"    Feature: {feature.Type} ({feature.Role})");
                foreach (Function function in feature.Functions)
                {
                    var supported = function.SupportedFunction;
                    var operations = new List<string>();
                    if (supported.possibleOperations?.read != null) operations.Add("read");
                    if (supported.possibleOperations?.write != null) operations.Add("write");
                    Console.WriteLine($"      - {supported.function} [{string.Join(", ", operations)}]");
                }

                // Function names alone only say a list *exists* - for Measurement, the actual
                // per-channel data (already fetched automatically during connect, see
                // MeasurementListData/MeasurementDescriptionListData.EvaluateAsync) says what's
                // really behind it: which physical quantity, unit, and current value.
                //
                // Only channels with Confirmed == true are shown: EEBUS.Net's
                // MonitoringOfPowerConsumption use case constructors (EEBUS.UseCases.
                // MonitoringAppliance/MonitoredUnit) unconditionally seed 10 hardcoded placeholder
                // channels - guessed type/unit/scope/phase - into this exact list before any wire
                // data arrives. A remote that doesn't actually expose all of them (this wallbox only
                // ever confirmed 1 of the 10 seeded IDs across an entire session, live-verified via
                // its trace log - never id 4 "acEnergyConsumed" despite the placeholder claiming it
                // exists) would otherwise have 9 fully fabricated channels shown as if real, with no
                // way to tell them apart. Phase (electricalConnectionParameterDescriptionData) is
                // additionally left out even for confirmed channels - that field is updated by a
                // separate function/code path this session didn't verify as trustworthy to the same
                // degree.
                if (feature is MeasurementServerFeature measurementFeature)
                    PrintMeasurementChannels(measurementFeature, "        ");

                if (feature is BillServerFeature billFeature)
                    PrintBillEntries(billFeature, "        ");
            }
        }

        Console.WriteLine("  LoadControl limits:");
        PrintLoadControlLimits(remote, "    ");
        Console.WriteLine("  ElectricalConnection characteristics (contractual limits):");
        PrintElectricalConnectionCharacteristics(remote, "    ");
        Console.WriteLine("  DeviceConfiguration key values:");
        PrintDeviceConfigurationKeyValues(remote, "    ");
    }

    /// <summary>
    /// Function names alone only say a list *exists* - the actual per-channel data (already fetched
    /// automatically during connect, see MeasurementListData/MeasurementDescriptionListData.
    /// EvaluateAsync) says what's really behind it: which physical quantity, unit, and current value.
    ///
    /// Only channels with Confirmed == true are shown: EEBUS.Net's MonitoringOfPowerConsumption use
    /// case constructors (EEBUS.UseCases.MonitoringAppliance/MonitoredUnit) unconditionally seed 10
    /// hardcoded placeholder channels - guessed type/unit/scope/phase - into this exact list before
    /// any wire data arrives. A remote that doesn't actually expose all of them (this wallbox only
    /// ever confirmed 1 of the 10 seeded IDs across an entire session, live-verified via its trace
    /// log - never id 4 "acEnergyConsumed" despite the placeholder claiming it exists) would
    /// otherwise have 9 fully fabricated channels shown as if real, with no way to tell them apart.
    /// </summary>
    private static void PrintMeasurementChannels(MeasurementServerFeature measurementFeature, string indent)
    {
        if (measurementFeature.measurementData.Count == 0) return;

        var confirmed = measurementFeature.measurementData.Where(c => c.Confirmed).ToList();
        foreach (var channel in confirmed)
        {
            var description = channel.measurementDescriptionDataType;
            string value = channel.MeasuredValue is { } v ? v.ToString("G6") : "n/a";
            Console.WriteLine(
                $"{indent}channel {channel.measurementId}: {description?.measurementType ?? "?"} " +
                $"({description?.scopeType ?? "?"}), unit={description?.unit ?? "?"}, value={value}");
        }
        int unconfirmed = measurementFeature.measurementData.Count - confirmed.Count;
        if (unconfirmed > 0)
            Console.WriteLine($"{indent}({unconfirmed} more channel(s) declared by this feature type but never confirmed by this remote's own replies - not shown)");
    }

    /// <summary>
    /// "EV Charging Summary" (evChargingSummary) use case: per its spec the Bill feature's data is
    /// nominally written by an external "Energy Broker", not computed by the EVSE itself. Loop over
    /// descriptions (not bills) so a bill entry that exists but carries no data yet
    /// (updateRequired=true - the server explicitly signalling "I have nothing, an Energy Broker
    /// needs to write this") is shown instead of silently skipped. Never fabricated locally -
    /// everything printed here came from this remote's own reply.
    /// </summary>
    private static void PrintBillEntries(BillServerFeature billFeature, string indent)
    {
        if (billFeature.descriptions.Count == 0) return;

        foreach (var description in billFeature.descriptions)
        {
            var bill = billFeature.bills.FirstOrDefault(b => b.billId == description.billId);
            string supported = description.supportedBillType is { Length: > 0 } types
                ? string.Join(",", types) : "?";

            var totalValue = bill?.total?.value?.FirstOrDefault();
            if (totalValue?.value == null)
            {
                string reason = description.updateRequired == true
                    ? "server has no data - it requests an external Energy Broker write it (updateRequired=true)"
                    : "no data yet";
                Console.WriteLine($"{indent}bill {description.billId} ({supported}): n/a - {reason}");
                continue;
            }

            var totalCost = bill!.total?.cost?.FirstOrDefault();
            string energy = $"{ScaledToDouble(totalValue.value) / 1000.0:F3} kWh";
            string cost = totalCost?.cost is { } c ? $"{ScaledToDouble(c):F2} {totalCost.currency ?? ""}".TrimEnd() : "n/a";
            string period = bill.total?.timePeriod is { } tp ? $"{tp.startTime ?? "?"} .. {tp.endTime ?? "?"}" : "n/a";
            Console.WriteLine($"{indent}bill {description.billId} ({supported}): total {energy}, cost={cost}, period={period}");

            foreach (var position in bill.position ?? [])
            {
                var value = position.value?.FirstOrDefault();
                var posCost = position.cost?.FirstOrDefault();
                string pct = value?.valuePercentage is { } vp ? $"{ScaledToDouble(vp):F1}%" : "n/a";
                string costPct = posCost?.costPercentage is { } cp ? $"{ScaledToDouble(cp):F1}%" : "n/a";
                Console.WriteLine($"{indent}  position {position.positionId}: {position.positionType ?? "?"} energy%={pct} cost%={costPct}");
            }
        }
    }

    private static void PrintLoadControlLimits(RemoteDevice remote, string indent)
    {
        foreach (var limit in remote.GetDataStructures<LoadControlLimitDataStructure>())
        {
            Console.WriteLine(
                $"{indent}limitId={limit.Id} direction={limit.LimitDirection} active={limit.LimitActive} " +
                $"value={limit.Number} changeable={limit.LimitChangable}");
        }
    }

    private static void PrintElectricalConnectionCharacteristics(RemoteDevice remote, string indent)
    {
        foreach (var c in remote.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>())
        {
            double value = c.Number * Math.Pow(10, c.Scale);
            Console.WriteLine($"{indent}{c.CharacteristicType}: {value:F0}");
        }
    }

    private static void PrintDeviceConfigurationKeyValues(RemoteDevice remote, string indent)
    {
        foreach (var kv in remote.KeyValues)
        {
            string value = kv.Data.value?.scaledNumber is { } sn
                ? ScaledToDouble(sn).ToString("F2")
                : kv.Data.value?.duration ?? "n/a";
            Console.WriteLine($"{indent}{kv.KeyName}: {value}");
        }
    }

    /// <summary>
    /// Dumps every single value this client can currently read from the remote, one line each: a
    /// fresh on-demand read of Measurement/LPC/manufacturer/diagnosis-state/permitted-value-ranges,
    /// plus everything already known from connect's auto-subscribed data (Measurement channels, Bill
    /// entries, LoadControl limits, ElectricalConnection characteristics, DeviceConfiguration key
    /// values). Nothing here is fabricated - an "n/a" line means the remote itself didn't answer or
    /// doesn't support that Function; see PrintMeasurementChannels/PrintBillEntries for why some
    /// entries are filtered rather than just relayed verbatim.
    /// </summary>
    private async Task ReadValueAsync(string[] tokens)
    {
        var remoteRef = ResolveRemote(tokens);
        if (remoteRef == null) return;

        var data = new DeviceData
        {
            Measurements = new MeasurementsData(),
            Lpc = new LpcLppData(),
            Manufacturer = new ManufacturerData(),
            DiagnosisState = new DiagnosisStateData(),
            PermittedValueRanges = new List<PermittedValueRangeData>(),
            ElectricalConnectionInfo = new ElectricalConnectionInfoData(),
        };
        await _manager!.ReadDataAsync(data, remoteRef.Ski);

        Console.WriteLine("Measurement:");
        // This wallbox never sends acEnergyConsumed via SPINE at all, neither in a read reply nor via
        // notify (confirmed by exhaustive log analysis: zero occurrences across a full charging
        // session). It only ever reports a single channel, acPowerTotal. This is a wallbox/firmware
        // limitation, not something this client can work around - "n/a" here is the correct answer.
        Console.WriteLine(data.Measurements?.AcEnergyConsumed is { } wh
            ? $"  Energy consumed so far: {wh / 1000.0:F3} kWh"
            : "  Energy consumed so far: n/a (this wallbox never reports this channel - not a bug here)");
        Console.WriteLine(data.Measurements?.AcPowerTotal is { } watts
            ? $"  Total AC power now:     {watts:F0} W"
            : "  Total AC power now:     n/a");

        Console.WriteLine("LoadControl:");
        Console.WriteLine(data.Lpc?.Limit is { } limit
            ? $"  Current LPC limit:      {limit} W (active={data.Lpc.LimitActive})"
            : "  Current LPC limit:      n/a (device may not answer LoadControl reads while idle)");

        Console.WriteLine("DeviceClassification (manufacturer info):");
        if (data.Manufacturer is { } m && (m.DeviceName ?? m.VendorName ?? m.BrandName ?? m.SerialNumber) != null)
        {
            Console.WriteLine($"  Device name:        {m.DeviceName ?? "n/a"}");
            Console.WriteLine($"  Vendor:             {m.VendorName ?? "n/a"}");
            Console.WriteLine($"  Brand:              {m.BrandName ?? "n/a"}");
            Console.WriteLine($"  Serial number:      {m.SerialNumber ?? "n/a"}");
            Console.WriteLine($"  Software revision:  {m.SoftwareRevision ?? "n/a"}");
            Console.WriteLine($"  Hardware revision:  {m.HardwareRevision ?? "n/a"}");
            Console.WriteLine($"  Power source:       {m.PowerSource ?? "n/a"}");
        }
        else
        {
            Console.WriteLine("  n/a (no reply)");
        }

        Console.WriteLine("DeviceDiagnosis (state):");
        if (data.DiagnosisState is { } ds && (ds.OperatingState ?? ds.PowerSupplyCondition) != null)
        {
            Console.WriteLine($"  Operating state:       {ds.OperatingState ?? "n/a"}");
            Console.WriteLine($"  Last error code:       {ds.LastErrorCode ?? "n/a"}");
            Console.WriteLine($"  Power supply condition: {ds.PowerSupplyCondition ?? "n/a"}");
            Console.WriteLine($"  Up time:               {ds.UpTime ?? "n/a"}");
        }
        else
        {
            Console.WriteLine("  n/a (no reply)");
        }

        Console.WriteLine("ElectricalConnection (description):");
        if (data.ElectricalConnectionInfo?.PowerSupplyType != null)
        {
            var eci = data.ElectricalConnectionInfo;
            Console.WriteLine($"  Power supply type:         {eci.PowerSupplyType}");
            Console.WriteLine($"  AC connected phases:       {eci.AcConnectedPhases?.ToString() ?? "n/a"}");
            Console.WriteLine($"  Positive energy direction: {eci.PositiveEnergyDirection ?? "n/a"}");
        }
        else
        {
            Console.WriteLine("  n/a (no reply)");
        }

        Console.WriteLine("ElectricalConnection (permitted value ranges):");
        if (data.PermittedValueRanges is { Count: > 0 } ranges)
        {
            foreach (var r in ranges)
            {
                string values = r.Values.Count > 0 ? string.Join(", ", r.Values.Select(v => v.ToString("F1"))) : "-";
                string rangeStr = r.Ranges.Count > 0 ? string.Join(", ", r.Ranges.Select(x => $"{x.Min:F1}..{x.Max:F1}")) : "-";
                Console.WriteLine($"  electricalConnectionId={r.ElectricalConnectionId} parameterId={r.ParameterId}: values=[{values}] ranges=[{rangeStr}]");
            }
        }
        else
        {
            Console.WriteLine("  n/a (no reply)");
        }

        var remote = _manager!.Devices.GetRemotes().FirstOrDefault(r => r.SKI.ToString() == remoteRef.Ski);
        if (remote != null)
        {
            Console.WriteLine("Measurement channels / Bill entries (already known from connect):");
            foreach (var entity in remote.Entities)
            {
                foreach (var feature in entity.Features)
                {
                    if (feature is MeasurementServerFeature mf) PrintMeasurementChannels(mf, "  ");
                    if (feature is BillServerFeature bf) PrintBillEntries(bf, "  ");
                }
            }

            Console.WriteLine("LoadControl limits (already known from connect):");
            PrintLoadControlLimits(remote, "  ");
            Console.WriteLine("ElectricalConnection characteristics (contractual limits, already known from connect):");
            PrintElectricalConnectionCharacteristics(remote, "  ");
            Console.WriteLine("DeviceConfiguration key values (already known from connect):");
            PrintDeviceConfigurationKeyValues(remote, "  ");
        }
    }

    /// <summary>Writes a new max charging power (LimitationOfPowerConsumption "consume" limit).</summary>
    private async Task WriteLimitAsync(string[] tokens)
    {
        var remote = ResolveRemote(tokens);
        if (remote == null) return;

        if (!long.TryParse(tokens.ElementAtOrDefault(2), out long watts))
        {
            Console.WriteLine("Usage: write <index> <watts>");
            return;
        }

        var data = new DeviceData { Lpc = new LpcLppData { LimitActive = true, Limit = watts } };
        await _manager!.WriteDataAsync(data, remote.Ski);
        Console.WriteLine($"Wrote LPC limit {watts} W to {remote.Name} ({remote.Ski}).");
    }

    private static double ScaledToDouble(ScaledNumberType scaled)
    {
        double scale = scaled.scale ?? 0;
        return scaled.number * Math.Pow(10, scale);
    }

    private EEBUS.Net.EEBUS.Models.Data.RemoteDeviceData? ResolveRemote(string[] tokens)
    {
        if (!int.TryParse(tokens.ElementAtOrDefault(1), out int index))
        {
            Console.WriteLine("Usage: <command> <index> - see 'remotes' for the index.");
            return null;
        }

        var remote = _manager!.GetDiscoveredDevices().ElementAtOrDefault(index);
        if (remote == null)
            Console.WriteLine("No such remote - see 'remotes' for valid indices.");
        return remote;
    }
}
