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

using Aeromux.Infrastructure.Database;

namespace Aeromux.Infrastructure.Tests.Database;

/// <summary>
/// Tests for <see cref="DatabaseDownloader.DeleteSupersededDatabases"/> — the daemon's post-update
/// cleanup that keeps only the just-installed database file.
/// </summary>
public sealed class DatabaseDownloaderTests : IDisposable
{
    private readonly string _dir;

    public DatabaseDownloaderTests()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aeromux-prune-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
        GC.SuppressFinalize(this);
    }

    private string Touch(string name)
    {
        string path = System.IO.Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void DeletesOtherDatabases_KeepsInstalledAndUnrelatedFiles()
    {
        Touch("aeromux-db_2026.2.w20_r2.sqlite");
        Touch("aeromux-db_2026.1.w08_r1.sqlite");
        string keep = Touch("aeromux-db_2026.3.w29_r1.sqlite");
        string unrelated = Touch("notes.txt");

        int removed = DatabaseDownloader.DeleteSupersededDatabases(_dir, keep);

        removed.Should().Be(2, "the two older aeromux-db files should be deleted");
        Directory.GetFiles(_dir, "aeromux-db_*.sqlite").Should().ContainSingle()
            .Which.Should().Be(keep);
        File.Exists(unrelated).Should().BeTrue("non-matching files must be left untouched");
    }

    [Fact]
    public void KeepFileMatchedByName_NotFullPath()
    {
        string keep = Touch("aeromux-db_2026.3.w29_r1.sqlite");
        Touch("aeromux-db_2026.2.w20_r2.sqlite");

        // Pass a keep path with a different directory prefix but the same file name.
        string keepByName = System.IO.Path.Combine("/some/other/place", "aeromux-db_2026.3.w29_r1.sqlite");
        DatabaseDownloader.DeleteSupersededDatabases(_dir, keepByName);

        File.Exists(keep).Should().BeTrue();
        Directory.GetFiles(_dir, "aeromux-db_*.sqlite").Should().ContainSingle();
    }

    [Fact]
    public void OnlyKeptFilePresent_DeletesNothing()
    {
        string keep = Touch("aeromux-db_2026.3.w29_r1.sqlite");

        DatabaseDownloader.DeleteSupersededDatabases(_dir, keep);

        File.Exists(keep).Should().BeTrue();
    }

    [Fact]
    public void MissingDirectory_DoesNotThrow()
    {
        string missing = System.IO.Path.Combine(_dir, "does-not-exist");

        int removed = 0;
        Action act = () => removed = DatabaseDownloader.DeleteSupersededDatabases(
            missing, System.IO.Path.Combine(missing, "aeromux-db_x.sqlite"));

        act.Should().NotThrow();
        removed.Should().Be(0);
    }
}
