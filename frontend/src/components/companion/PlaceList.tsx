import { useTranslation } from "react-i18next";
import { pickPlaceName } from "@/lib/placeName";
import { useLocale } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { CompanionPlace } from "@/types/companion";
import { BANDS, CATEGORIES } from "./meta";
import OpenStatusText from "./OpenStatusText";

interface PlaceListProps {
  places: CompanionPlace[];
  selectedId: string | null;
  onSelect: (placeId: string) => void;
}

/** Places grouped by walking band; each row is a button that selects the place. */
export default function PlaceList({ places, selectedId, onSelect }: PlaceListProps) {
  const { t } = useTranslation();
  const { lang } = useLocale();
  return (
    <div className="space-y-4">
      {BANDS.map((band) => {
        const inBand = places.filter((p) => p.band === band);
        if (inBand.length === 0) return null;
        return (
          <div key={band}>
            <h3 className="mb-1 text-xs font-medium tracking-wide text-muted-foreground uppercase">{t(`companion.bands.${band}`)}</h3>
            <ul className="divide-y rounded-xl ring-1 ring-foreground/10">
              {inBand.map((place) => {
                const { icon: Icon, color } = CATEGORIES[place.category];
                const isSelected = selectedId === place.id;
                return (
                  <li key={place.id}>
                    <button
                      type="button"
                      onClick={() => onSelect(place.id)}
                      aria-pressed={isSelected}
                      className={cn(
                        "flex w-full items-center gap-3 p-3 text-start transition-colors outline-none hover:bg-muted/60 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:ring-inset",
                        isSelected && "bg-muted",
                      )}
                    >
                      <Icon aria-hidden="true" className="size-4 shrink-0" style={{ color }} />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{pickPlaceName(place, lang)}</span>
                        <span className="block text-xs text-muted-foreground">
                          {t("companion.walk", { count: place.walkMinutes })} · {t("companion.roundTrip", { count: place.walkMinutes * 2 })}
                        </span>
                      </span>
                      <OpenStatusText place={place} className="shrink-0 text-end" />
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        );
      })}
    </div>
  );
}
