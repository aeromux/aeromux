#!/bin/bash

# Aeromux Docker Entrypoint
# Handles automatic database download on first daemon start,
# then execs the actual command as PID 1.
#
# Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program. If not, see <https://www.gnu.org/licenses/>.

set -e

# Only auto-download the database when running the daemon
if [ "$1" = "aeromux" ] && [ "$2" = "daemon" ]; then
    # Check if database directory has any files (first-run detection)
    if [ -z "$(ls -A /var/lib/aeromux/ 2>/dev/null)" ]; then
        echo "First run detected — downloading aircraft database..."
        aeromux database update --config /etc/aeromux/aeromux.yaml
        echo "Aircraft database downloaded successfully."
    fi
fi

# Replace shell with the actual command (PID 1 for signal handling)
exec "$@"
