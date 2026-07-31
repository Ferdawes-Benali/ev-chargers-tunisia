import type { CompanionPlace, PlaceLanguage } from "@/types/companion";

/** The place's name in `lang` (the current i18n language), else French, else English, else the fallback name. */
export function pickPlaceName(place: Pick<CompanionPlace, "name" | "names">, lang: PlaceLanguage): string {
  return place.names[lang] ?? place.names.fr ?? place.names.en ?? place.name;
}
