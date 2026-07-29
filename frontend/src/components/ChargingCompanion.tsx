import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useCompanion, useWalkingRoute } from "@/hooks/useCompanion";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import CompanionMap from "@/components/companion/CompanionMap";
import PlaceCard from "@/components/companion/PlaceCard";
import PlaceList from "@/components/companion/PlaceList";
import SmartPicks from "@/components/companion/SmartPicks";
import { GROUPS } from "@/components/companion/meta";
import type { PlaceGroup } from "@/types/companion";
import type { StationDetail } from "@/types/station";

/** "Plan my wait": what to do near this charger while the car charges. */
export default function ChargingCompanion({ station }: { station: StationDetail }) {
  const { data, isLoading, isError } = useCompanion(station.id);
  const [filter, setFilter] = useState<PlaceGroup | "all">("all");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Where focus was before the place card opened, to give it back on close
  const returnFocusTo = useRef<HTMLElement | null>(null);

  const places = useMemo(() => data?.places ?? [], [data]);
  const presentGroups = GROUPS.filter(({ group }) => places.some((p) => p.group === group));
  // "All" hides the extra ATMs/banks; the Essentials filter shows every one.
  // Memoized: a new array would re-fit the map on every render.
  const visible = useMemo(
    () => (filter === "all" ? places.filter((p) => p.inDefaultList) : places.filter((p) => p.group === filter)),
    [places, filter],
  );
  const selected = useMemo(() => places.find((p) => p.id === selectedId), [places, selectedId]);
  const route = useWalkingRoute(station.id, selectedId);

  const select = (placeId: string) => {
    if (placeId === selectedId) return close();
    if (!selectedId) returnFocusTo.current = document.activeElement as HTMLElement | null;
    // A pick may point to a place the current filter hides
    const place = places.find((p) => p.id === placeId);
    if (place && !visible.some((p) => p.id === placeId)) setFilter("all");
    setSelectedId(placeId);
  };

  const close = useCallback(() => {
    setSelectedId(null);
    const target = returnFocusTo.current;
    returnFocusTo.current = null;
    if (target?.isConnected) target.focus();
  }, []);

  useEffect(() => {
    if (!selectedId) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [selectedId, close]);

  const toggleFilter = (group: PlaceGroup | "all") => {
    setFilter((current) => (current === group ? "all" : group));
    setSelectedId(null);
  };

  return (
    <section aria-labelledby="companion-heading" className="space-y-3">
      <div>
        <h2 id="companion-heading" className="font-semibold">While you charge</h2>
        {data && (
          data.chargeMinutes !== null && data.backBy ? (
            <>
              <p className="text-sm">
                You'll charge for about {data.chargeMinutes} min — back by <strong>{data.backBy}</strong>
              </p>
              <p className="text-xs text-muted-foreground">
                {data.maxPowerKw} kW charger, 20→80% for a typical car
              </p>
            </>
          ) : (
            <p className="text-sm text-muted-foreground">Places within a short walk</p>
          )
        )}
      </div>

      {isLoading ? (
        <div className="space-y-2" aria-label="Loading nearby places">
          <div className="grid gap-2 sm:grid-cols-3">
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
          </div>
          <Skeleton className="h-62.5 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : isError || data?.unavailable ? (
        <p className="text-sm text-muted-foreground">Nearby places are unavailable right now.</p>
      ) : places.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Nothing within walking distance — a good moment for a coffee in the car ☕
        </p>
      ) : (
        <>
          <SmartPicks picks={data?.picks ?? []} places={places} selectedId={selectedId} onSelect={select} />

          <div role="group" aria-label="Filter places" className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant={filter === "all" ? "default" : "outline"}
              aria-pressed={filter === "all"}
              onClick={() => toggleFilter("all")}
            >
              All
            </Button>
            {presentGroups.map(({ group, label, icon: Icon }) => (
              <Button
                key={group}
                size="sm"
                variant={filter === group ? "default" : "outline"}
                aria-pressed={filter === group}
                onClick={() => toggleFilter(group)}
              >
                <Icon aria-hidden="true" />
                {label}
              </Button>
            ))}
          </div>

          <CompanionMap
            station={station}
            places={visible}
            selected={selected}
            route={route.data}
            onSelect={select}
          />

          {selected && (
            <PlaceCard
              place={selected}
              station={station}
              route={route.data}
              routeLoading={route.isLoading}
              chargeMinutes={data?.chargeMinutes ?? null}
              onClose={close}
            />
          )}

          <PlaceList places={visible} selectedId={selectedId} onSelect={select} />
        </>
      )}

      {data && !data.unavailable && (
        <p className="text-xs text-muted-foreground">
          Places ©{" "}
          <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer" className="underline">
            OpenStreetMap contributors
          </a>
        </p>
      )}
    </section>
  );
}
