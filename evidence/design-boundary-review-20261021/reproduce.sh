#!/bin/sh
set -eu
review_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
probe_dir=$(mktemp -d "${TMPDIR:-/tmp}/uniclaw-boundary-probe.XXXXXX")
trap 'rm -rf "$probe_dir"' EXIT
mkdir -p "$probe_dir/snapshot" "$probe_dir/probe"
tar -xzf "$review_dir/source-snapshot.tar.gz" -C "$probe_dir/snapshot"
cp "$review_dir/Probe.csproj" "$review_dir/Program.cs" "$probe_dir/probe/"
dotnet run --project "$probe_dir/probe/Probe.csproj" --verbosity quiet -p:WarningLevel=0 -p:AuditSnapshotRoot="$probe_dir/snapshot"
