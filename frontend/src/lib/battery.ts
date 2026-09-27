export const BATTERY_COLORS = {
  good: "#0E9F6E",
  low: "#E8A33D",
  critical: "#D1495B",
} as const;

/** Battery color by level: green above 35%, amber from 15 to 35%, red below 15%. */
export function batteryColor(percent: number) {
  if (percent < 15) return BATTERY_COLORS.critical;
  if (percent <= 35) return BATTERY_COLORS.low;
  return BATTERY_COLORS.good;
}
