const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ["year", 365 * 24 * 3600],
  ["month", 30 * 24 * 3600],
  ["week", 7 * 24 * 3600],
  ["day", 24 * 3600],
  ["hour", 3600],
  ["minute", 60],
];

/**
 * "3 days ago" / "il y a 3 jours" / "قبل 3 أيام"… in the given Intl locale.
 * The API sends UTC; a timestamp without a zone is read as UTC.
 */
export function relativeTime(iso: string, locale: string, now: number = Date.now()): string {
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  const hasZone = /(Z|[+-]\d{2}:?\d{2})$/.test(iso);
  const seconds = (new Date(hasZone ? iso : `${iso}Z`).getTime() - now) / 1000;

  for (const [unit, size] of UNITS) {
    if (Math.abs(seconds) >= size) return formatter.format(Math.round(seconds / size), unit);
  }
  return formatter.format(0, "second");
}
