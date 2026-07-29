import { useEffect, useRef } from "react";
import { ExternalLink, X } from "lucide-react";
import { PLACE_NAME_LANGUAGE, pickPlaceName } from "@/lib/placeName";
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
  const headingRef = useRef<HTMLHeadingElement>(null);
  const name = pickPlaceName(place, PLACE_NAME_LANGUAGE);
  const { icon: Icon, color } = CATEGORIES[place.category];

  // Move focus to the card when it opens or shows another place
  useEffect(() => {
    headingRef.current?.focus();
  }, [place.id]);

  const realRoute = route && !route.estimated ? route : undefined;
  const walkMinutes = realRoute?.durationMinutes ?? route?.durationMinutes ?? place.walkMinutes;
  const meters = realRoute?.distanceMeters ?? Math.round((route?.distanceMeters ?? place.distanceMeters * DETOUR_FACTOR) / 10) * 10;

  return (
    <Card aria-labelledby="companion-place-heading">
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
        <Button variant="ghost" size="icon-sm" onClick={onClose} aria-label="Close place details">
          <X aria-hidden="true" />
        </Button>
      </CardHeader>

      <CardContent className="space-y-3">
        <p className="text-sm">
          {realRoute ? (
            <>{walkMinutes} min walk · {meters} m</>
          ) : (
            <>
              About {walkMinutes} min walk · {meters} m
              {routeLoading && <span className="text-muted-foreground"> (finding the walking route…)</span>}
            </>
          )}
        </p>

        {chargeMinutes !== null && <TimeBudget walkMinutes={walkMinutes} chargeMinutes={chargeMinutes} />}

        <a
          href={walkingDirectionsUrl(station, place)}
          target="_blank"
          rel="noopener noreferrer"
          aria-label={`Open walking directions to ${name} in Google Maps`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          <ExternalLink aria-hidden="true" />
          Open in Google Maps
        </a>
      </CardContent>
    </Card>
  );
}

/** walk there | time you can stay | walk back, relative to the charging time. */
function TimeBudget({ walkMinutes, chargeMinutes }: { walkMinutes: number; chargeMinutes: number }) {
  const stay = chargeMinutes - 2 * walkMinutes;
  const fits = stay >= 0;
  const total = Math.max(chargeMinutes, 2 * walkMinutes);
  const pct = (minutes: number) => `${(minutes / total) * 100}%`;

  return (
    <div className="space-y-1">
      <div aria-hidden="true" className="flex h-2.5 overflow-hidden rounded-full bg-muted">
        <div className="bg-sky-500" style={{ width: pct(walkMinutes) }} />
        {fits && <div className="bg-emerald-500" style={{ width: pct(stay) }} />}
        <div className={fits ? "bg-sky-500" : "bg-amber-500"} style={{ width: pct(walkMinutes) }} />
      </div>
      {fits ? (
        <p className="flex justify-between gap-2 text-xs text-muted-foreground">
          <span>{walkMinutes} min there</span>
          <span className="font-medium text-foreground">about {stay} min to stay</span>
          <span>{walkMinutes} min back</span>
        </p>
      ) : (
        <p className="text-xs font-medium text-amber-700 dark:text-amber-400">
          Tight: you'd be back after your car is ready
        </p>
      )}
    </div>
  );
}
