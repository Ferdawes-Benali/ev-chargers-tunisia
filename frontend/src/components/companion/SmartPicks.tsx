import { PLACE_NAME_LANGUAGE, pickPlaceName } from "@/lib/placeName";
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
  const cards = picks
    .map((pick) => ({ pick, place: places.find((p) => p.id === pick.placeId) }))
    .filter((c): c is { pick: CompanionPick; place: CompanionPlace } => !!c.place);
  if (cards.length === 0) return null;

  return (
    <ul aria-label="Suggestions for your wait" className="grid gap-2 sm:grid-cols-3">
      {cards.map(({ pick, place }) => {
        const { icon: Icon, color } = CATEGORIES[place.category];
        const isSelected = selectedId === place.id;
        return (
          <li key={pick.label}>
            <button
              type="button"
              onClick={() => onSelect(place.id)}
              aria-pressed={isSelected}
              className={cn(
                "flex h-full w-full items-start gap-3 rounded-xl p-3 text-left ring-1 ring-foreground/10 transition-colors outline-none hover:bg-muted/60 focus-visible:ring-3 focus-visible:ring-ring/50",
                isSelected && "bg-muted ring-foreground/30",
              )}
            >
              <span className="grid size-9 shrink-0 place-items-center rounded-full" style={{ backgroundColor: `${color}1a`, color }}>
                <Icon aria-hidden="true" className="size-5" />
              </span>
              <span className="min-w-0 space-y-0.5">
                <span className="block text-xs font-medium tracking-wide text-muted-foreground uppercase">{pick.label}</span>
                <span className="block truncate text-sm font-semibold">{pickPlaceName(place, PLACE_NAME_LANGUAGE)}</span>
                <span className="block text-xs text-muted-foreground">{place.walkMinutes} min walk</span>
                <OpenStatusText place={place} className="block" />
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
