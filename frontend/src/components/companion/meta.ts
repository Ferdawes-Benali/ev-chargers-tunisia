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
import type { TFunction } from "i18next";
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

/** Chip order; labels are translated from "companion.groups.<group>". */
export const GROUPS: { group: PlaceGroup; icon: LucideIcon }[] = [
  { group: "eat", icon: UtensilsCrossed },
  { group: "pray", icon: MoonStar },
  { group: "essentials", icon: Pill },
  { group: "relax", icon: Trees },
  { group: "shop", icon: ShoppingBag },
];

/** List order; labels are translated from "companion.bands.<band>". */
export const BANDS: WalkBand[] = ["min2", "min5", "min10", "min15", "far"];

/** Formats opening status in the current language; returns null when opening hours are unknown. */
export function openStatusText(place: Pick<CompanionPlace, "openStatus" | "closesAt" | "opensAt">, t: TFunction): string | null {
  switch (place.openStatus) {
    case "open":
      return place.closesAt ? t("companion.status.openUntil", { time: place.closesAt }) : t("companion.status.open247");
    case "closed":
      return place.opensAt ? t("companion.status.closedUntil", { time: place.opensAt }) : t("companion.status.closed");
    default:
      return null;
  }
}

export const walkingDirectionsUrl = (from: { lat: number; lng: number }, to: { lat: number; lng: number }) =>
  `https://www.google.com/maps/dir/?api=1&origin=${from.lat},${from.lng}&destination=${to.lat},${to.lng}&travelmode=walking`;
