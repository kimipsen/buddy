# shellcheck shell=bash
# Sourced by ../pre-commit. Each check gets only the staged files it applies to, and checks their
# working-tree content (a file staged in part is checked as it is on disk).

FRONTEND_DIR="src/frontend/buddy"
I18N_DIR="$FRONTEND_DIR/src/app/core/i18n"
LOAD_TESTS_DIR="src/load-tests"

pre_commit_run() {
  local root
  root="$(git rev-parse --show-toplevel)"
  cd "$root"

  local -a staged frontend backend markdown load_tests
  mapfile -t staged < <(git diff --cached --name-only --diff-filter=ACMR)
  frontend=()
  backend=()
  markdown=()
  load_tests=()
  local i18n_changed=0 file

  for file in "${staged[@]}"; do
    case "$file" in
      *.md) markdown+=("$file") ;;
      "$FRONTEND_DIR"/*) frontend+=("${file#"$FRONTEND_DIR"/}") ;;
      src/backend/*.cs) backend+=("$file") ;;
      "$LOAD_TESTS_DIR"/*.ts | "$LOAD_TESTS_DIR"/*.mjs | "$LOAD_TESTS_DIR"/*.json | "$LOAD_TESTS_DIR"/*.yml)
        load_tests+=("${file#"$LOAD_TESTS_DIR"/}") ;;
    esac
    case "$file" in
      "$I18N_DIR"/*) i18n_changed=1 ;;
    esac
  done

  local failed=0
  ((${#frontend[@]} == 0)) || check_frontend "${frontend[@]}" || failed=1
  ((i18n_changed == 0)) || check_i18n || failed=1
  ((${#backend[@]} == 0)) || check_backend "${backend[@]}" || failed=1
  ((${#markdown[@]} == 0)) || check_markdown "${markdown[@]}" || failed=1
  ((${#load_tests[@]} == 0)) || check_load_tests "${load_tests[@]}" || failed=1

  if ((failed)); then
    echo >&2
    echo "pre-commit: fix the problems above, or commit with --no-verify to skip these checks." >&2
    return 1
  fi
}

check_frontend() {
  if [[ ! -x "$FRONTEND_DIR/node_modules/.bin/prettier" ]]; then
    echo "pre-commit: $FRONTEND_DIR/node_modules is missing (run npm ci there) -- skipping frontend checks." >&2
    return 0
  fi

  local -a lintable=()
  local file
  for file in "$@"; do
    case "$file" in
      *.ts | *.html) lintable+=("$file") ;;
    esac
  done

  local failed=0
  echo "pre-commit: prettier (${#} file(s))"
  if ! (cd "$FRONTEND_DIR" && node_modules/.bin/prettier --check --ignore-unknown --log-level warn "$@"); then
    echo "  fix: (cd $FRONTEND_DIR && npm run format)" >&2
    failed=1
  fi

  if ((${#lintable[@]} > 0)); then
    echo "pre-commit: eslint (${#lintable[@]} file(s))"
    # Same flat config as `ng lint` in CI; warnings don't fail it there either.
    if ! (cd "$FRONTEND_DIR" && node_modules/.bin/eslint --no-warn-ignored "${lintable[@]}"); then
      failed=1
    fi
  fi

  return "$failed"
}

# Every markdown file in the repo, run from the root: Prettier picks up the .prettierrc next to each
# file, markdownlint the root .markdownlint-cli2.jsonc (which also lists the ignored files).
check_markdown() {
  local bin="$FRONTEND_DIR/node_modules/.bin"
  if [[ ! -x "$bin/markdownlint-cli2" ]]; then
    echo "pre-commit: $FRONTEND_DIR/node_modules is missing (run npm ci there) -- skipping markdown checks." >&2
    return 0
  fi

  local failed=0
  echo "pre-commit: markdown (${#} file(s))"
  if ! "$bin/prettier" --check --log-level warn "$@"; then
    echo "  fix: $bin/prettier --write $*" >&2
    failed=1
  fi
  # The banner and summary go to stdout; the findings themselves to stderr.
  if ! "$bin/markdownlint-cli2" "$@" >/dev/null; then
    echo "  fix: task lint:markdown FIX=1 fixes some rules; fix the rest by hand (rules in .markdownlint-cli2.jsonc)." >&2
    failed=1
  fi

  return "$failed"
}

# The k6 scripts (docs/backend/analysis/load-testing-k6.md): Prettier on the staged files, and the
# whole project's type check, since a change to one module can break another's types.
check_load_tests() {
  if [[ ! -x "$LOAD_TESTS_DIR/node_modules/.bin/tsc" ]]; then
    echo "pre-commit: $LOAD_TESTS_DIR/node_modules is missing (run npm ci there) -- skipping load-test checks." >&2
    return 0
  fi

  local failed=0
  echo "pre-commit: load tests: prettier (${#} file(s)) and tsc"
  if ! (cd "$LOAD_TESTS_DIR" && node_modules/.bin/prettier --check --log-level warn "$@"); then
    echo "  fix: (cd $LOAD_TESTS_DIR && npm run format)" >&2
    failed=1
  fi
  if ! (cd "$LOAD_TESTS_DIR" && node_modules/.bin/tsc --noEmit -p tsconfig.json); then
    failed=1
  fi

  return "$failed"
}

check_i18n() {
  echo "pre-commit: en/da translation parity"
  node .claude/skills/i18n/check-parity.mjs
}

check_backend() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "pre-commit: dotnet not found -- skipping the C# formatting check." >&2
    return 0
  fi

  # Whitespace only (no build), on the solution rather than --folder: folder mode parses with the
  # default C# version and misreads `union`. Code style and analyzers that need the compiler run
  # in the build. --include paths must be relative to the current directory (the repo root here):
  # absolute paths silently match nothing.
  echo "pre-commit: dotnet format whitespace (${#} file(s))"
  if ! dotnet format whitespace src/backend/backend.slnx --verify-no-changes --include "$@"; then
    echo "  fix: dotnet format whitespace src/backend/backend.slnx --include $*" >&2
    return 1
  fi
}
