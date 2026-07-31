import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Trans, useTranslation } from "react-i18next";
import { useCompanion, useWalkingRoute } from "@/hooks/useCompanion";
import { useLocale } from "@/lib/format";
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
  const { t } = useTranslation();
  const { number } = useLocale();
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
        <h2 id="companion-heading" className="font-semibold">{t("companion.title")}</h2>
        {data && (
          data.chargeMinutes !== null && data.backBy ? (
            <>
              <p className="text-sm">
                <Trans
                  i18nKey="companion.chargeFor"
                  count={data.chargeMinutes}
                  values={{ time: data.backBy }}
                  components={{ strong: <strong /> }}
                />
              </p>
              {data.maxPowerKw !== null && (
                <p className="text-xs text-muted-foreground">
                  {t("companion.chargerHint", { power: number(data.maxPowerKw) })}
                </p>
              )}
            </>
          ) : (
            <p className="text-sm text-muted-foreground">{t("companion.shortWalk")}</p>
          )
        )}
      </div>

      {isLoading ? (
        <div className="space-y-2" aria-busy="true" aria-label={t("companion.loading")}>
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
        <p className="text-sm text-muted-foreground">{t("companion.unavailable")}</p>
      ) : places.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("companion.empty")}</p>
      ) : (
        <>
          <SmartPicks picks={data?.picks ?? []} places={places} selectedId={selectedId} onSelect={select} />

          <div role="group" aria-label={t("companion.filters")} className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant={filter === "all" ? "default" : "outline"}
              aria-pressed={filter === "all"}
              onClick={() => toggleFilter("all")}
            >
              {t("companion.all")}
            </Button>
            {presentGroups.map(({ group, icon: Icon }) => (
              <Button
                key={group}
                size="sm"
                variant={filter === group ? "default" : "outline"}
                aria-pressed={filter === group}
                onClick={() => toggleFilter(group)}
              >
                <Icon aria-hidden="true" />
                {t(`companion.groups.${group}`)}
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
          {t("companion.credit")}{" "}
          <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer" className="underline">
            {t("companion.creditLink")}
          </a>
        </p>
      )}
    </section>
  );
}
