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

using Aeromux.CLI.Commands;
using Aeromux.Core.Configuration;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands;

public class ConnectionStringParserTests
{
    // ─── Parse() Happy Path ───

    [Fact]
    public void Parse_HostAndPort_ReturnsParsed()
    {
        (string host, int port) = ConnectionStringParser.Parse("192.168.1.10:30005");
        host.Should().Be("192.168.1.10");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_NumericOnly_ReturnsLocalhostWithPort()
    {
        (string host, int port) = ConnectionStringParser.Parse("30005");
        host.Should().Be("localhost");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_HostnameOnly_ReturnsHostWithDefaultPort()
    {
        (string host, int port) = ConnectionStringParser.Parse("piaware");
        host.Should().Be("piaware");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_Null_ReturnsDefaults()
    {
        (string host, int port) = ConnectionStringParser.Parse(null);
        host.Should().Be("localhost");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsDefaults()
    {
        (string host, int port) = ConnectionStringParser.Parse("");
        host.Should().Be("localhost");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsDefaults()
    {
        (string host, int port) = ConnectionStringParser.Parse("   ");
        host.Should().Be("localhost");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_DnsHostnameWithPort_ReturnsParsed()
    {
        (string host, int port) = ConnectionStringParser.Parse("piaware.local:30005");
        host.Should().Be("piaware.local");
        port.Should().Be(30005);
    }

    [Fact]
    public void Parse_IpWithCustomPort_ReturnsParsed()
    {
        (string host, int port) = ConnectionStringParser.Parse("192.168.1.10:12345");
        host.Should().Be("192.168.1.10");
        port.Should().Be(12345);
    }

    // ─── Parse() Edge Cases ───

    [Fact]
    public void Parse_PortOne_ReturnsLocalhostWithPortOne()
    {
        (string host, int port) = ConnectionStringParser.Parse("1");
        host.Should().Be("localhost");
        port.Should().Be(1);
    }

    [Fact]
    public void Parse_MaxPort_ReturnsLocalhostWithMaxPort()
    {
        (string host, int port) = ConnectionStringParser.Parse("65535");
        host.Should().Be("localhost");
        port.Should().Be(65535);
    }

    [Fact]
    public void Parse_ColonPrefix_ThrowsForEmptyHost()
    {
        // ":30005" splits to ["", "30005"] — empty host fails validation
        Action act = () => ConnectionStringParser.Parse(":30005");
        act.Should().Throw<ArgumentException>();
    }

    // ─── Parse() Invalid Inputs ───

    [Fact]
    public void Parse_PortZero_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.Parse("host:0");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_PortExceedsMax_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.Parse("host:65536");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_NonNumericPort_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.Parse("host:abc");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_TooManyColons_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.Parse("host:port:extra");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_NegativePort_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.Parse("host:-1");
        act.Should().Throw<ArgumentException>();
    }

    // ─── ParseMultiple() ───

    [Fact]
    public void ParseMultiple_TwoValidSources_ReturnsTwoConfigs()
    {
        List<BeastSourceConfig> result = ConnectionStringParser.ParseMultiple(["192.168.1.10:30005", "piaware:30005"]);
        result.Should().HaveCount(2);
        result[0].Host.Should().Be("192.168.1.10");
        result[0].Port.Should().Be(30005);
        result[1].Host.Should().Be("piaware");
        result[1].Port.Should().Be(30005);
    }

    [Fact]
    public void ParseMultiple_SingleSource_ReturnsOneConfig()
    {
        List<BeastSourceConfig> result = ConnectionStringParser.ParseMultiple(["192.168.1.10:30005"]);
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseMultiple_Null_ReturnsEmptyList()
    {
        List<BeastSourceConfig> result = ConnectionStringParser.ParseMultiple(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseMultiple_EmptyArray_ReturnsEmptyList()
    {
        List<BeastSourceConfig> result = ConnectionStringParser.ParseMultiple([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseMultiple_DuplicateSources_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.ParseMultiple(["host:30005", "host:30005"]);
        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public void ParseMultiple_DuplicatesCaseInsensitive_ThrowsArgumentException()
    {
        Action act = () => ConnectionStringParser.ParseMultiple(["Host:30005", "host:30005"]);
        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public void ParseMultiple_DifferentHostsSamePort_ReturnsTwoConfigs()
    {
        List<BeastSourceConfig> result = ConnectionStringParser.ParseMultiple(["host1:30005", "host2:30005"]);
        result.Should().HaveCount(2);
    }
}
