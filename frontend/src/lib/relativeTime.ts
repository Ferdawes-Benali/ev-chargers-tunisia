const formatter = new Intl.RelativeTimeFormat("en", { numeric: "auto" });

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 365 * 24 * 3600],
  ["month", 30 * 24 * 3600],
  ["week", 7 * 24 * 3600],
  ["day", 24 * 3600],
  ["hour", 3600],
  ["minute", 60],
];

/** "3 days ago", "yesterday", "now"… The API sends UTC; a timestamp without a zone is read as UTC. */
export function relativeTime(iso: string, now: number = Date.now()): string {
  const hasZone = /(Z|[+-]\d{2}:?\d{2})$/.test(iso);
  const seconds = (new Date(hasZone ? iso : `${iso}Z`).getTime() - now) / 1000;

  for (const [unit, size] of UNITS) {
    if (Math.abs(seconds) >= size) return formatter.format(Math.round(seconds / size), unit);
  }
  return formatter.format(0, "second");
}
