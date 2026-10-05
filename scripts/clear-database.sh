#!/usr/bin/env bash
# Clear every table in every user schema of a Buddy Postgres database (destructive).
#
# Schemas are discovered at runtime instead of read from a fixed list, so this works against a
# database from any app version: schemas the current code no longer has (or doesn't have yet)
# are handled, and missing ones are simply not there to clear. System schemas (pg_*,
# information_schema) are never touched. Keycloak lives in its own database and is unaffected.
#
# Connection uses the standard libpq variables; unset ones default to the devcontainer database:
#   PGHOST=db PGPORT=5432 PGUSER=postgres PGPASSWORD=postgres PGDATABASE=postgres
#
# Usage: scripts/clear-database.sh [--drop] [--dry-run] [--yes]
#   (default)  TRUNCATE all tables and restart all sequences; schemas and tables stay.
#   --drop     DROP every user schema except public (CASCADE) and truncate public's tables.
#              Marten recreates its schemas on startup / first use.
#   --dry-run  List what would be cleared and exit.
#   --yes      Don't ask for confirmation.
set -euo pipefail

mode=truncate
dry_run=false
assume_yes=false
for arg in "$@"; do
  case "$arg" in
    --drop) mode=drop ;;
    --dry-run) dry_run=true ;;
    --yes | -y) assume_yes=true ;;
    -h | --help) sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown argument: $arg (see --help)" >&2; exit 2 ;;
  esac
done

export PGHOST="${PGHOST:-db}" PGPORT="${PGPORT:-5432}" PGUSER="${PGUSER:-postgres}"
export PGPASSWORD="${PGPASSWORD:-postgres}" PGDATABASE="${PGDATABASE:-postgres}"

psql_cmd=(psql -X -v ON_ERROR_STOP=1 --no-psqlrc)

# User schemas: everything that isn't a system schema.
user_schemas="n.nspname not like 'pg\_%' and n.nspname <> 'information_schema'"

echo "Database: ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE}  (mode: ${mode})"
"${psql_cmd[@]}" -c "
  select n.nspname as schema, count(c.oid) filter (where c.relkind in ('r','p')) as tables
  from pg_namespace n
  left join pg_class c on c.relnamespace = n.oid and not c.relispartition
  where ${user_schemas}
  group by n.nspname
  order by n.nspname;"

if $dry_run; then
  exit 0
fi

if ! $assume_yes; then
  read -r -p "This permanently deletes all data above. Type the database name to continue: " answer
  if [[ "$answer" != "$PGDATABASE" ]]; then
    echo "Aborted." >&2
    exit 1
  fi
fi

if [[ "$mode" == drop ]]; then
  drop_schemas="
    for s in select n.nspname from pg_namespace n
             where ${user_schemas} and n.nspname <> 'public'
               -- schemas that belong to an extension go when the extension does
               and not exists (select 1 from pg_depend d
                               where d.classid = 'pg_namespace'::regclass and d.objid = n.oid
                                 and d.deptype = 'e')
             order by 1
    loop
      execute format('drop schema %I cascade', s);
      raise notice 'dropped schema %', s;
    end loop;"
  table_scope="n.nspname = 'public'"
else
  drop_schemas=""
  table_scope="${user_schemas}"
fi

# One TRUNCATE over every table at once, so foreign keys between them don't get in the way.
# RESTART IDENTITY only resets sequences owned by a column, so reset the rest (e.g. Marten's
# mt_events_sequence) explicitly.
"${psql_cmd[@]}" --single-transaction <<SQL
do \$\$
declare
  s text;
  tbls text;
  cnt int;
begin
  ${drop_schemas}

  select string_agg(format('%I.%I', n.nspname, c.relname), ', '), count(*)
  into tbls, cnt
  from pg_class c
  join pg_namespace n on n.oid = c.relnamespace
  where c.relkind in ('r', 'p') and not c.relispartition and ${table_scope};

  if tbls is not null then
    execute 'truncate table ' || tbls || ' restart identity cascade';
  end if;
  raise notice 'truncated % table(s)', coalesce(cnt, 0);

  for s in select format('%I.%I', n.nspname, c.relname)
           from pg_class c
           join pg_namespace n on n.oid = c.relnamespace
           where c.relkind = 'S' and ${table_scope}
  loop
    execute format('alter sequence %s restart', s);
  end loop;
end \$\$;
SQL

echo "Done."
