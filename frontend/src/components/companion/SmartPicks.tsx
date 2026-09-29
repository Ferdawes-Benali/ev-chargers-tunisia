import { useTranslation } from "react-i18next";
import { pickPlaceName } from "@/lib/placeName";
import { useLocale } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { CompanionPick, CompanionPlace } from "@/types/companion";
import { CATEGORIES } from "./meta";
import OpenStatusText from "./OpenStatusText";

interface SmartPicksProps {
  picks: CompanionPick[];
  places: CompanionPlace[];
  selectedId: string | null;
  onSelect: (placeId: string) => void;
}

export default function SmartPicks({ picks, places, selectedId, onSelect }: SmartPicksProps) {
  const { t } = useTranslation();
  const { lang } = useLocale();
  const cards = picks
    .map((pick) => ({ pick, place: places.find((p) => p.id === pick.placeId) }))
    .filter((c): c is { pick: CompanionPick; place: CompanionPlace } => !!c.place);
  if (cards.length === 0) return null;

  return (
    <ul aria-label={t("companion.picks")} className="grid gap-2 sm:grid-cols-3">
      {cards.map(({ pick, place }) => {
        const { icon: Icon, color } = CATEGORIES[place.category];
        const isSelected = selectedId === place.id;
        return (
          <li key={pick.kind}>
            <button
              type="button"
              onClick={() => onSelect(place.id)}
              aria-pressed={isSelected}
              className={cn(
                "flex h-full w-full items-start gap-3 rounded-xl bg-card p-3 text-start shadow-xs ring-1 ring-border transition-colors outline-none hover:bg-muted/60 focus-visible:ring-3 focus-visible:ring-ring/50",
                isSelected && "bg-primary/10 ring-2 ring-primary/60 hover:bg-primary/10",
              )}
            >
              <span className="grid size-9 shrink-0 place-items-center rounded-full" style={{ backgroundColor: `${color}1a`, color }}>
                <Icon aria-hidden="true" className="size-5" />
              </span>
              <span className="min-w-0 space-y-0.5">
                <span className="block text-xs font-medium tracking-wide text-muted-foreground uppercase">
                  {t(`companion.pickKind.${pick.kind}`)}
                </span>
                <span className="block truncate text-sm font-semibold">{pickPlaceName(place, lang)}</span>
                <span className="block text-xs text-muted-foreground tabular-nums">{t("companion.walk", { count: pick.walkMinutes })}</span>
                <OpenStatusText place={place} className="block" />
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
