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

using System.Globalization;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Infrastructure.Streaming;
using Serilog;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Writes the body of the session summary that the daemon and the live command log when
/// they stop: the totals, then one block per input source.
/// </summary>
/// <remarks>
/// <para>
/// The figures and their names are those of <c>GET /api/v1/stats</c>, read from the same
/// <see cref="ReceiverStatistics"/>, so the log and the API never disagree.
/// </para>
/// <para>
/// The summary is read by people, in the log file and on the terminal, so it is laid out
/// for reading: one value per line, labels padded so that every number ends in the same
/// column. Each line is still a message template, so the values remain structured
/// properties. String properties use the <c>:l</c> format, which writes them without
/// quotes in every sink.
/// </para>
/// </remarks>
internal static class SessionSummaryLog
{
    /// <summary>The line between two blocks, as wide as the heading's separators.</summary>
    internal const string BlockSeparator = "───────────────────────────────────────────────────────────────";

    /// <summary>
    /// Logs the summary body: the duration and totals, then a block for the SDR devices
    /// together, one per device, one per Beast source, and one for MLAT, each only when
    /// that source exists.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="statistics">The last reading of the receiver, or null when it never started.</param>
    /// <param name="duration">How long the session ran.</param>
    /// <param name="aircraftTracked">Aircraft tracked at the end, logged after the messages; null to leave it out.</param>
    internal static void Write(
        ILogger logger,
        ReceiverStatistics? statistics,
        TimeSpan duration,
        int? aircraftTracked = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        logger.Information("Session duration     {Duration,11:l}", FormatDuration(duration));

        if (statistics == null)
        {
            logger.Information("No statistics: the receiver did not start");
            return;
        }

        double average = duration > TimeSpan.Zero ? statistics.Messages / duration.TotalSeconds : 0;
        logger.Information("Messages             {Messages,11:N0}   {AveragePerSecond:N1} per second on average",
            statistics.Messages, average);

        if (aircraftTracked is { } aircraft)
        {
            logger.Information("Aircraft tracked     {AircraftTracked,11:N0}", aircraft);
        }

        // Each source's messages as a share of all sources' together, so the shares add up
        // to 100%. The aggregator's total above can differ from that sum by the messages in
        // flight at the moment of reading.
        long allSources = (statistics.Sdr?.Devices.Sum(d => d.Messages) ?? 0)
            + statistics.BeastSources.Sum(b => b.Messages)
            + (statistics.Mlat?.Messages ?? 0);

        if (statistics.Sdr is { } sdr)
        {
            WriteSdr(logger, sdr, allSources);
        }

        foreach (BeastSourceStatistics source in statistics.BeastSources)
        {
            logger.Information(BlockSeparator);
            logger.Information("Beast source {Host:l}:{Port}", source.Host, source.Port);
            MessagesLine(logger, source.Messages, allSources);
            logger.Information("  Reconnects         {Reconnects,11:N0}", source.Reconnects);
            MessageTypesTable(logger, source.MessageTypes);
        }

        if (statistics.Mlat is { } mlat)
        {
            logger.Information(BlockSeparator);
            logger.Information("MLAT");
            MessagesLine(logger, mlat.Messages, allSources);
            MessageTypesTable(logger, mlat.MessageTypes);
        }
    }

    private static void WriteSdr(ILogger logger, SdrStatistics sdr, long allSources)
    {
        logger.Information(BlockSeparator);
        logger.Information("SDR, all devices");
        RadioLines(logger,
            sdr.Devices.Sum(d => d.Extracted),
            sdr.Devices.Sum(d => d.Corrected),
            sdr.Devices.Sum(d => d.Duplicates));
        MessagesLine(logger, sdr.Devices.Sum(d => d.Messages), allSources);

        foreach (SdrDeviceStatistics device in sdr.Devices)
        {
            logger.Information(BlockSeparator);
            logger.Information("Device '{DeviceName:l}' (index {DeviceIndex})", device.Name, device.DeviceIndex);
            RadioLines(logger, device.Extracted, device.Corrected, device.Duplicates);
            MessagesLine(logger, device.Messages, allSources);
            MessageTypesTable(logger, device.MessageTypes);
        }
    }

    /// <summary>
    /// Logs how an SDR device fares on the radio side. No shares: these are not parts of
    /// one whole (corrected frames are extracted frames, and so are duplicates).
    /// </summary>
    private static void RadioLines(ILogger logger, long extracted, long corrected, long duplicates)
    {
        logger.Information("  Extracted          {Extracted,11:N0}", extracted);
        logger.Information("  Corrected          {Corrected,11:N0}", corrected);
        logger.Information("  Duplicates         {Duplicates,11:N0}", duplicates);
    }

    /// <summary>
    /// Logs a source's messages with their share of all sources' messages, or without one
    /// when no source delivered any and a share would mean nothing.
    /// </summary>
    private static void MessagesLine(ILogger logger, long messages, long allSources)
    {
        if (allSources > 0)
        {
            logger.Information("  Messages           {Messages,11:N0}  {MessagesShare,5:F1}%",
                messages, messages * 100.0 / allSources);
        }
        else
        {
            logger.Information("  Messages           {Messages,11:N0}", messages);
        }
    }

    // Column widths of the message type table: the code (DF and TC numbers, BDS
    // registers up to "Unknown"), its count, and its share of its column
    private const int DfWidth = 6;
    private const int TcWidth = 6;
    private const int BdsWidth = 10;
    private const int CountWidth = 11;
    private const int ShareWidth = 7;

    /// <summary>
    /// Logs a source's messages by kind as one table, after a blank line: DF, TC and BDS
    /// side by side, each column ordered by count with each code's share of its column.
    /// </summary>
    /// <remarks>
    /// Side by side, a source takes as many rows as its longest column, about fifteen; one
    /// row per code would take about forty. The rows are text built with fixed widths and
    /// the invariant culture, which formats numbers as the rest of the log does (US format,
    /// "1,234.5"), and written with the <c>:l</c> format so no sink quotes them. They are not structured properties: the
    /// structured form is <c>GET /api/v1/stats</c>, and a row holds three unrelated codes.
    /// </remarks>
    private static void MessageTypesTable(ILogger logger, MessageTypeCounts types)
    {
        List<string>[] columns =
        [
            Column(types.DownlinkFormats, code => code.ToString(CultureInfo.InvariantCulture), DfWidth),
            Column(types.TypeCodes, code => code.ToString(CultureInfo.InvariantCulture), TcWidth),
            Column(types.BdsCodes, code => code.ToRegisterName(), BdsWidth)
        ];
        int rows = columns.Max(c => c.Count);
        if (rows == 0)
        {
            return;
        }

        int[] widths = [DfWidth + CountWidth + ShareWidth, TcWidth + CountWidth + ShareWidth, BdsWidth + CountWidth + ShareWidth];

        logger.Information("");
        logger.Information("{Row:l}", "  " + string.Join("   ",
            Cell("DF", DfWidth, "Count", "Share"),
            Cell("TC", TcWidth, "Count", "Share"),
            Cell("BDS", BdsWidth, "Count", "Share")));

        for (int row = 0; row < rows; row++)
        {
            string line = "  " + string.Join("   ",
                Enumerable.Range(0, 3).Select(c => row < columns[c].Count ? columns[c][row] : new string(' ', widths[c])));
            logger.Information("{Row:l}", line.TrimEnd());
        }
    }

    /// <summary>
    /// One column of the table: the cells of a kind, highest count first, ties by code value.
    /// </summary>
    private static List<string> Column<TCode>(
        IReadOnlyDictionary<TCode, long> counts, Func<TCode, string> name, int codeWidth)
        where TCode : notnull
    {
        long total = counts.Values.Sum();
        // The rows are text by the time a sink renders them, so they name their culture
        // rather than depend on the thread that writes them
        CultureInfo culture = CultureInfo.InvariantCulture;

        return
        [
            .. counts
                .OrderByDescending(c => c.Value)
                .ThenBy(c => c.Key)
                .Select(c => Cell(
                    name(c.Key),
                    codeWidth,
                    c.Value.ToString("N0", culture),
                    (c.Value * 100.0 / total).ToString("F1", culture) + "%"))
        ];
    }

    private static string Cell(string code, int codeWidth, string count, string share) =>
        code.PadRight(codeWidth) + count.PadLeft(CountWidth) + share.PadLeft(ShareWidth);

    /// <summary>
    /// Formats a duration as hours, minutes and seconds, with the hours counted in full:
    /// a session of 25 hours reads <c>25:00:00</c>, not <c>01:00:00</c>.
    /// </summary>
    internal static string FormatDuration(TimeSpan duration) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}");
}
