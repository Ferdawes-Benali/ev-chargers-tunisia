import {
  Banknote,
  Coffee,
  MoonStar,
  Pill,
  ShoppingBag,
  Toilet,
  Trees,
  UtensilsCrossed,
  type LucideIcon,
} from "lucide-react";
import type { CompanionPlace, PlaceCategory, PlaceGroup, WalkBand } from "@/types/companion";

export const CATEGORIES: Record<PlaceCategory, { icon: LucideIcon; color: string }> = {
  cafe: { icon: Coffee, color: "#b45309" },
  restaurant: { icon: UtensilsCrossed, color: "#dc2626" },
  mosque: { icon: MoonStar, color: "#059669" },
  park: { icon: Trees, color: "#16a34a" },
  shopping: { icon: ShoppingBag, color: "#7c3aed" },
  pharmacy: { icon: Pill, color: "#0891b2" },
  toilets: { icon: Toilet, color: "#64748b" },
  atm: { icon: Banknote, color: "#ca8a04" },
};

export const GROUPS: { group: PlaceGroup; label: string; icon: LucideIcon }[] = [
  { group: "eat", label: "Eat & drink", icon: UtensilsCrossed },
  { group: "pray", label: "Pray", icon: MoonStar },
  { group: "essentials", label: "Essentials", icon: Pill },
  { group: "relax", label: "Relax", icon: Trees },
  { group: "shop", label: "Shop", icon: ShoppingBag },
];

export const BANDS: { band: WalkBand; label: string }[] = [
  { band: "≤2 min", label: "Within 2 min walk" },
  { band: "≤5 min", label: "Within 5 min walk" },
  { band: "≤10 min", label: "Within 10 min walk" },
  { band: "≤15 min", label: "Within 15 min walk" },
  { band: "farther", label: "A bit farther" },
];

/** "Open · closes 22:00", "Closed · opens 08:00"… null when hours are unknown: we never guess. */
export function openStatusText(place: Pick<CompanionPlace, "openStatus" | "closesAt" | "opensAt">): string | null {
  switch (place.openStatus) {
    case "open":
      return place.closesAt ? `Open · closes ${place.closesAt}` : "Open 24/7";
    case "closed":
      return place.opensAt ? `Closed · opens ${place.opensAt}` : "Closed";
    default:
      return null;
  }
}

export const walkingDirectionsUrl = (from: { lat: number; lng: number }, to: { lat: number; lng: number }) =>
  `https://www.google.com/maps/dir/?api=1&origin=${from.lat},${from.lng}&destination=${to.lat},${to.lng}&travelmode=walking`;
