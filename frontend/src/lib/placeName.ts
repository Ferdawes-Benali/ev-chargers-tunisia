import type { CompanionPlace, PlaceLanguage } from "@/types/companion";

export const PLACE_NAME_LANGUAGE: PlaceLanguage = "fr";

export function pickPlaceName(place: Pick<CompanionPlace, "name" | "names">, lang: PlaceLanguage): string {
  return place.names[lang] ?? place.names.fr ?? place.names.en ?? place.name;
}
