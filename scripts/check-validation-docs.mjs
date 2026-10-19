#!/usr/bin/env node
// Checks the `## Validation` table of every docs/backend/<domain>/flow.md against the OpenAPI
// contract (docs/backend/openapi/buddy.json) and the FluentValidation validators:
//   - every row names an operationId of that domain's tag, a status the operation answers with,
//     and an ErrorEnvelope code the operation lists under that status (x-error-codes);
//   - every operation that can answer `400 validation_error` has at least one row;
//   - every endpoint-specific error code (not one the shared middleware adds) has a row;
//   - every *Validator in src/backend/buddy/Features whose command is an operation has a row.
// Usage: node scripts/check-validation-docs.mjs   (exit code 1 on any problem)

import { readFileSync, readdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const openapi = JSON.parse(readFileSync(join(root, "docs/backend/openapi/buddy.json"), "utf8"));

// Codes every operation can get from shared middleware; documented once in
// docs/backend/http-status-codes.md, not per domain.
const middlewareCodes = new Set([
  "invalid_idempotency_key",
  "idempotency_key_in_progress",
  "idempotency_key_reused",
  "idempotency_response_unavailable",
  "concurrency_conflict",
  "user_not_provisioned",
  "rate_limited",
  "internal_error",
  "dependency_unavailable",
]);

const kebab = (tag) => tag.replace(/([a-z])([A-Z])/g, "$1-$2").toLowerCase();

const operations = new Map();
for (const [path, verbs] of Object.entries(openapi.paths)) {
  for (const [verb, op] of Object.entries(verbs)) {
    const codes = new Map();
    for (const [status, response] of Object.entries(op.responses ?? {})) {
      codes.set(status, new Set(response["x-error-codes"] ?? []));
    }
    operations.set(op.operationId, { route: `${verb.toUpperCase()} ${path}`, domain: kebab(op.tags?.[0] ?? ""), codes });
  }
}

const problems = [];
const documented = new Map(); // operationId -> Set of "status code"

const docsDir = join(root, "docs/backend");
for (const domain of readdirSync(docsDir)) {
  const file = join(docsDir, domain, "flow.md");
  if (!existsSync(file)) continue;
  const lines = readFileSync(file, "utf8").split("\n");
  const start = lines.findIndex((l) => /^## Validation\s*$/.test(l));
  if (start < 0) {
    problems.push(`${domain}/flow.md: no "## Validation" section`);
    continue;
  }
  let header = null;
  for (let i = start + 1; i < lines.length && !/^## /.test(lines[i]); i++) {
    const line = lines[i];
    if (!line.startsWith("|")) continue;
    const cells = line.split(/(?<!\\)\|/).slice(1, -1).map((c) => c.trim());
    if (!header) {
      header = cells.map((c) => c.toLowerCase());
      continue;
    }
    if (cells.every((c) => /^:?-+:?$/.test(c))) continue;
    const at = (name) => cells[header.indexOf(name)] ?? "";
    const where = `${domain}/flow.md:${i + 1}`;
    const code = at("code").replace(/`/g, "");
    const status = at("status").replace(/`/g, "");
    if (!header.includes("endpoint") || !header.includes("code") || !header.includes("status")) {
      problems.push(`${where}: table needs Endpoint, Field, Rule, Code and Status columns`);
      break;
    }
    // One rule shared by several operations lists them all: `AddMeal`, `UpdateMeal`.
    for (const operationId of at("endpoint").replace(/`|<br>/g, ",").split(",").map((s) => s.trim()).filter(Boolean)) {
      const op = operations.get(operationId);
      if (!op) {
        problems.push(`${where}: unknown operationId "${operationId}"`);
        continue;
      }
      if (op.domain !== domain) problems.push(`${where}: ${operationId} belongs in ${op.domain}/flow.md`);
      if (!op.codes.has(status)) problems.push(`${where}: ${operationId} (${op.route}) has no ${status} response`);
      else if (!op.codes.get(status).has(code))
        problems.push(`${where}: ${operationId} ${status} does not list "${code}" (has: ${[...op.codes.get(status)].join(", ") || "none"})`);
      if (!documented.has(operationId)) documented.set(operationId, new Set());
      documented.get(operationId).add(`${status} ${code}`);
    }
  }
}

for (const [operationId, op] of operations) {
  for (const [status, codes] of op.codes) {
    for (const code of codes) {
      if (middlewareCodes.has(code)) continue;
      if (!documented.get(operationId)?.has(`${status} ${code}`))
        problems.push(`${op.domain}/flow.md: missing ${operationId} (${op.route}) ${status} ${code}`);
    }
  }
}

const walk = (dir) =>
  readdirSync(dir, { withFileTypes: true }).flatMap((e) => (e.isDirectory() ? walk(join(dir, e.name)) : [join(dir, e.name)]));
for (const file of walk(join(root, "src/backend/buddy/Features"))) {
  if (!file.endsWith(".cs")) continue;
  for (const match of readFileSync(file, "utf8").matchAll(/class (\w+)Validator\s*:\s*AbstractValidator</g)) {
    const operationId = match[1];
    if (operations.has(operationId) && !documented.has(operationId))
      problems.push(`${operations.get(operationId).domain}/flow.md: ${match[1]}Validator has no rows for ${operationId}`);
  }
}

if (problems.length) {
  console.error(problems.join("\n"));
  console.error(`\n${problems.length} problem(s).`);
  process.exit(1);
}
console.log(`Validation docs match the OpenAPI contract (${documented.size} operations documented).`);
