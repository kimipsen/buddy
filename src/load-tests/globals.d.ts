// Globals k6 provides at runtime that @types/k6 doesn't declare (and lib "dom" would over-declare).
declare const crypto: { randomUUID(): string };
declare const console: {
  log(...data: unknown[]): void;
  warn(...data: unknown[]): void;
};
