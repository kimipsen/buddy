import { copyFileSync, existsSync, rmSync } from 'node:fs';
import { join } from 'node:path';

const RUNTIME_CONFIG_PATH = join(__dirname, '../../public/config/runtime-config.json');
const BACKUP_PATH = `${RUNTIME_CONFIG_PATH}.e2e-backup`;

// Restores the checked-in runtime-config.json that global-setup.ts temporarily overwrote.
export default function globalTeardown(): void {
  if (existsSync(BACKUP_PATH)) {
    copyFileSync(BACKUP_PATH, RUNTIME_CONFIG_PATH);
    rmSync(BACKUP_PATH);
  }
}
