import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { ExternalLink, X } from "lucide-react";
import { pickPlaceName } from "@/lib/placeName";
import { useLocale } from "@/lib/format";
import { buttonVariants, Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import type { CompanionPlace, WalkingRoute } from "@/types/companion";
import type { StationDetail } from "@/types/station";
import { CATEGORIES, walkingDirectionsUrl } from "./meta";
import OpenStatusText from "./OpenStatusText";

interface PlaceCardProps {
  place: CompanionPlace;
  station: StationDetail;
  route: WalkingRoute | undefined;
  routeLoading: boolean;
  chargeMinutes: number | null;
  onClose: () => void;
}

/** Streets are never straight: same detour factor as the backend estimate. */
const DETOUR_FACTOR = 1.25;

export default function PlaceCard({ place, station, route, routeLoading, chargeMinutes, onClose }: PlaceCardProps) {
  const { t } = useTranslation();
  const { lang, number } = useLocale();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const name = pickPlaceName(place, lang);
  const { icon: Icon, color } = CATEGORIES[place.category];

  useEffect(() => {
    headingRef.current?.focus();
  }, [place.id]);

  const realRoute = route && !route.estimated ? route : undefined;
  const walkMinutes = realRoute?.durationMinutes ?? route?.durationMinutes ?? place.walkMinutes;
  const meters = realRoute?.distanceMeters ?? Math.round((route?.distanceMeters ?? place.distanceMeters * DETOUR_FACTOR) / 10) * 10;
  const distance = t("companion.meters", { value: number(meters) });

  return (
    <Card aria-labelledby="companion-place-heading" className="bg-muted/40 ring-primary/40">
      <CardHeader className="grid-cols-[1fr_auto]">
        <div className="flex min-w-0 items-start gap-2">
          <Icon aria-hidden="true" className="mt-0.5 size-5 shrink-0" style={{ color }} />
          <div className="min-w-0">
            <h3
              id="companion-place-heading"
              ref={headingRef}
              tabIndex={-1}
              className="rounded-sm text-base font-semibold outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
            >
              {name}
            </h3>
            <OpenStatusText place={place} />
          </div>
        </div>
        <Button variant="ghost" size="icon-sm" onClick={onClose} aria-label={t("companion.closeCard")}>
          <X aria-hidden="true" />
        </Button>
      </CardHeader>

      <CardContent className="space-y-3">
        <p className="text-sm tabular-nums">
          {realRoute ? (
            <>{t("companion.walk", { count: walkMinutes })} · {distance}</>
          ) : (
            <>
              {t("companion.aboutWalk", { count: walkMinutes })} · {distance}
              {routeLoading && <span className="text-muted-foreground"> {t("companion.findingRoute")}</span>}
            </>
          )}
        </p>

        {chargeMinutes !== null && <TimeBudget walkMinutes={walkMinutes} chargeMinutes={chargeMinutes} />}

        <a
          href={walkingDirectionsUrl(station, place)}
          target="_blank"
          rel="noopener noreferrer"
          aria-label={t("companion.openInMapsAria", { name })}
          className={buttonVariants({ variant: "outline", className: "h-9 gap-2" })}
        >
          <ExternalLink aria-hidden="true" />
          {t("companion.openInMaps")}
        </a>
      </CardContent>
    </Card>
  );
}

/** Displays outbound walking time, available stay time, and return walking time relative to charging duration. */
function TimeBudget({ walkMinutes, chargeMinutes }: { walkMinutes: number; chargeMinutes: number }) {
  const { t } = useTranslation();
  const stay = chargeMinutes - 2 * walkMinutes;
  const fits = stay >= 0;
  const total = Math.max(chargeMinutes, 2 * walkMinutes);
  const pct = (minutes: number) => `${(minutes / total) * 100}%`;

  // A quantity gauge: kept left-to-right in every language, like the battery bar
  return (
    <div className="space-y-1">
      <div aria-hidden="true" dir="ltr" className="flex h-2.5 overflow-hidden rounded-full bg-muted">
        <div className="bg-accent" style={{ width: pct(walkMinutes) }} />
        {fits && <div className="bg-success" style={{ width: pct(stay) }} />}
        <div className={fits ? "bg-accent" : "bg-warning"} style={{ width: pct(walkMinutes) }} />
      </div>
      {fits ? (
        <p dir="ltr" className="flex justify-between gap-2 text-xs text-muted-foreground tabular-nums">
          <span>{t("companion.budget.there", { count: walkMinutes })}</span>
          <span className="font-medium text-foreground">{t("companion.budget.stay", { count: stay })}</span>
          <span>{t("companion.budget.back", { count: walkMinutes })}</span>
        </p>
      ) : (
        <p className="text-xs font-medium text-warning-ink">
          {t("companion.budget.tight")}
        </p>
      )}
    </div>
  );
}
