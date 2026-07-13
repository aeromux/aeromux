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

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Helpers for validating the database storage directory.
/// </summary>
public static class DatabaseDirectory
{
    /// <summary>
    /// Validates that the given path is a directory that can be created (if missing) and written to.
    /// Creates the directory when it does not exist and probes writability with a temp file.
    /// </summary>
    /// <param name="path">The database directory path.</param>
    /// <returns>An error message, or <c>null</c> if the directory exists (or was created) and is writable.</returns>
    public static string? ValidateWritable(string path)
    {
        // Check if the path points to an existing file (not a directory)
        if (File.Exists(path))
        {
            return $"The database path {path} is a file, not a directory.";
        }

        // Create directory if it doesn't exist
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            return $"Cannot create database directory {path}: {ex.Message}";
        }

        // Verify writability by testing a temp file
        try
        {
            string testFile = Path.Combine(path, $".aeromux-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
        }
        catch
        {
            return $"The database directory {path} is not writable.";
        }

        return null;
    }
}
