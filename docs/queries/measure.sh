#!/usr/bin/env bash
# Measures the reporting queries with SET STATISTICS IO, TIME ON against the
# docker-compose SQL Server. Reproduces the numbers in docs/sql-performance.md.
#
# Usage:
#   docs/queries/measure.sh                      # run all queries
#   docs/queries/measure.sh <file> [<file>...]   # run specific queries
#
# Prerequisites: the docker-compose stack is running (`docker compose up -d`)
# and the seed data is loaded (`docker compose exec api dotnet InventorySync.Api.dll seed`).
set -euo pipefail

cd "$(dirname "$0")/../.."

PASSWORD="${MSSQL_SA_PASSWORD:-InventorySync!Passw0rd}"

if [[ $# -gt 0 ]]; then
  FILES=("$@")
else
  mapfile -t FILES < <(ls docs/queries/*.sql)
fi

for file in "${FILES[@]}"; do
  echo "================================================================"
  echo "== $file"
  echo "================================================================"
  cat "$file" | docker compose exec -T sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PASSWORD" -C -d InventorySync -y 24 -Y 24
  echo
done
