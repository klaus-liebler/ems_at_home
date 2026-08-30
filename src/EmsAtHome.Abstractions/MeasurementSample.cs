namespace EmsAtHome.Abstractions;

/// <summary>
/// One time-stamped numeric measurement (power, current, voltage, SoC, temperature, ...).
/// Shape is deliberately generic (tag + value) rather than device-specific fields, since it must
/// fit an inverter, a meter, a charger and a BMS alike without per-device special-casing.
/// </summary>
public sealed record MeasurementSample(DateTimeOffset Timestamp, string Tag, double Value, string Unit);
