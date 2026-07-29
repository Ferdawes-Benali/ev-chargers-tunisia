import type { CompanionPlace, PlaceLanguage } from "@/types/companion";

/** Until i18n lands, places are shown in French; i18n will pass the user's language instead. */
export const PLACE_NAME_LANGUAGE: PlaceLanguage = "fr";

/** The place's name in `lang`, else French, else English, else the fallback name. */
export function pickPlaceName(place: Pick<CompanionPlace, "name" | "names">, lang: PlaceLanguage): string {
  return place.names[lang] ?? place.names.fr ?? place.names.en ?? place.name;
}
