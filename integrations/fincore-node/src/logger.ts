/**
 * Logs estructurados en JSON (una línea por evento), como la API y el Worker.
 * Nunca se registran payloads, firmas, tokens ni números de cuenta completos.
 */
type Level = 'debug' | 'info' | 'warn' | 'error';

export function log(level: Level, message: string, fields: Record<string, unknown> = {}): void {
  if (process.env.LOG_SILENT === '1') return;
  const line = JSON.stringify({ '@t': new Date().toISOString(), '@l': level, '@m': message, Application: 'fincore-node', ...fields });
  (level === 'error' || level === 'warn' ? process.stderr : process.stdout).write(line + '\n');
}

/** "123-456789-01" → "***9-01": suficiente para soporte, inútil para un atacante. */
export function maskAccount(account: string | undefined | null): string {
  if (!account) return '';
  return account.length <= 4 ? account : `***${account.slice(-4)}`;
}
