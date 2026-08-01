export const BATTERY_COLORS = {
  good: "#22C55E",
  low: "#F59E0B",
  critical: "#EF4444",
} as const;

/** Battery color by level: green above 35%, amber from 15 to 35%, red below 15%. */
export function batteryColor(percent: number) {
  if (percent < 15) return BATTERY_COLORS.critical;
  if (percent <= 35) return BATTERY_COLORS.low;
  return BATTERY_COLORS.good;
}
