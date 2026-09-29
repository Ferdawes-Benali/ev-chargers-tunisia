import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import { CircleAlert, CircleCheck, Clock, Info, MapPin, Milestone, Route, Thermometer, TriangleAlert, X, Zap } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import PlaceSearch from "@/components/PlaceSearch";
import { cn } from "@/lib/utils";
import { batteryColor } from "@/lib/battery";
import { useLocale } from "@/lib/format";
import { useVehicles } from "@/hooks/useReach";
import type { PlaceSuggestion, ReachEstimateResult, TripPlanResult } from "@/types/reach";
import "./ReachPanel.css";

export interface ReachInputs {
  vehicleId: string;
  batteryPercent: number;
}

interface ReachPanelProps {
  onEstimate: (inputs: ReachInputs, options: { fresh: boolean }) => void;
  reachResult: ReachEstimateResult | undefined;
  tripResult: TripPlanResult | undefined;
  isPending: boolean;
  error: string | null;
  originLabel: string;
  /** True when the start is the map center (no GPS position): shows a hint to use "Locate me". */
  originIsMapCenter: boolean;
  originKey: string;
  destination: PlaceSuggestion | null;
  onDestinationChange: (place: PlaceSuggestion | null) => void;
  searchFocus: { lat: number; lng: number } | null;
  onClose: () => void;
}

type Tone = "good" | "tight" | "bad" | "neutral";

const TONE_STYLES: Record<Tone, string> = {
  good: "border-success/40 bg-success/10",
  tight: "border-warning/45 bg-warning/10",
  bad: "border-danger/40 bg-danger/10",
  neutral: "border-accent/40 bg-accent/10",
};

const TONE_ICONS: Record<Tone, { icon: typeof Info; className: string }> = {
  good: { icon: CircleCheck, className: "text-success-ink" },
  tight: { icon: TriangleAlert, className: "text-warning-ink" },
  bad: { icon: CircleAlert, className: "text-danger-ink" },
  neutral: { icon: Info, className: "text-accent-ink" },
};

type Format = (value: number) => string;

function getVerdict(
  rangeKm: number, trip: TripPlanResult | undefined, reserve: number, t: TFunction, number: Format,
): { tone: Tone; title: string; detail: string } {
  const km = number(Math.round(rangeKm));

  if (rangeKm === 0) {
    return { tone: "bad", title: t("trip.verdict.chargeNowTitle"), detail: t("trip.verdict.chargeNowDetail") };
  }

  if (trip) {
    const arrival = Math.round(trip.batteryOnArrival);
    if (!trip.reachable) {
      const stop = trip.recommendedStop;
      if (stop) {
        return {
          tone: "bad",
          title: t("trip.verdict.chargeOnWayTitle"),
          detail: t("trip.verdict.stopAt", {
            name: stop.name,
            km: number(Math.round(stop.distanceAlongKm)),
            percent: Math.round(stop.batteryOnArrivalPercent),
          }),
        };
      }
      const shortKm = Math.max(1, Math.ceil(trip.shortfallKm));
      return {
        tone: "bad",
        title: t("trip.verdict.chargeOnWayTitle"),
        detail: t("trip.verdict.shortNoCharger", { km: number(shortKm) }),
      };
    }
    if (trip.batteryOnArrival - reserve < 5) {
      return { tone: "tight", title: t("trip.verdict.tightTitle"), detail: t("trip.verdict.tightDetail", { percent: arrival }) };
    }
    return { tone: "good", title: t("trip.verdict.goodTitle"), detail: t("trip.verdict.goodDetail", { percent: arrival }) };
  }

  if (rangeKm < 20) {
    return { tone: "tight", title: t("trip.verdict.lowTitle", { km }), detail: t("trip.verdict.lowDetail") };
  }
  return { tone: "neutral", title: t("trip.verdict.rangeTitle", { km }), detail: t("trip.verdict.rangeDetail") };
}

function formatDuration(minutes: number, t: TFunction) {
  const total = Math.max(1, Math.round(minutes));
  if (total < 60) return t("trip.duration.minutes", { count: total });
  const h = Math.floor(total / 60);
  const m = total % 60;
  const hours = t("trip.duration.hours", { count: h });
  return m === 0 ? hours : `${hours} ${t("trip.duration.minutes", { count: m })}`;
}

function TripFacts({ trip }: { trip: TripPlanResult }) {
  const { t } = useTranslation();
  const { number } = useLocale();
  const facts = [
    { key: "distance", icon: Route, text: t("trip.facts.distance", { km: number(Math.round(trip.distanceKm)) }) },
    { key: "duration", icon: Clock, text: formatDuration(trip.durationMinutes, t) },
    { key: "roads", icon: Milestone, text: trip.motorwayShare >= 0.5 ? t("trip.facts.motorway") : t("trip.facts.local") },
    ...(trip.temperatureC !== null
      ? [{ key: "temperature", icon: Thermometer, text: t("trip.facts.temperature", { value: number(Math.round(trip.temperatureC)) }) }]
      : []),
  ];
  return (
    <ul aria-label={t("trip.facts.label")} className="grid grid-cols-2 gap-2 text-sm">
      {facts.map(({ key, icon: Icon, text }) => (
        <li key={key} className="flex items-center gap-2 rounded-lg bg-muted px-2.5 py-2 tabular-nums">
          <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          {text}
        </li>
      ))}
    </ul>
  );
}

/**
 * A drawing of the battery before, during and after the trip.
 * A quantity gauge: kept left-to-right in every language (empty at the left, full at the right).
 */
function TripBar({ battery, arrival, reserve }: { battery: number; arrival: number | null; reserve: number }) {
  const { t } = useTranslation();
  const [filled, setFilled] = useState(false);

  // Replay the fill each time a new result arrives
  useEffect(() => {
    setFilled(false);
    const timer = setTimeout(() => setFilled(true), 30);
    return () => clearTimeout(timer);
  }, [battery, arrival]);

  const left = arrival === null ? battery : Math.max(0, Math.min(battery, arrival));
  const used = battery - left;
  const belowReserve = arrival !== null && arrival < reserve;

  return (
    <div dir="ltr">
      <div
        role="img"
        aria-label={
          arrival === null
            ? t("trip.bar.ariaNow", { battery, reserve })
            : t("trip.bar.ariaTrip", { battery, arrival: Math.round(left), reserve })
        }
        className="relative h-6 overflow-hidden rounded-md border bg-muted"
      >
        <div
          className="absolute inset-y-0 left-0 motion-safe:transition-[width] motion-safe:duration-700 motion-safe:ease-out"
          style={{ width: filled ? `${left}%` : "0%", backgroundColor: batteryColor(left) }}
        />
        {used > 0 && (
          <div
            className="trip-used absolute inset-y-0 motion-safe:transition-[width] motion-safe:delay-300 motion-safe:duration-700 motion-safe:ease-out"
            style={{
              left: `${left}%`,
              width: filled ? `${used}%` : "0%",
            }}
          />
        )}
        <div
          className="trip-reserve absolute inset-y-0 left-0 border-r-2 border-dashed border-foreground/60"
          style={{ width: `${reserve}%` }}
        />
      </div>
      <div className="mt-1.5 flex justify-between gap-2 text-xs tabular-nums text-muted-foreground">
        <span dir="auto">{t("trip.bar.reserve", { percent: reserve })}</span>
        {arrival !== null &&
          (belowReserve
            ? <span dir="auto" className="font-medium text-danger-ink">{t("trip.bar.belowReserve")}</span>
            : <span dir="auto">{t("trip.bar.arriveWith", { percent: Math.round(left) })}</span>)}
        <span dir="auto">{t("trip.bar.now", { percent: battery })}</span>
      </div>
    </div>
  );
}

function stationLine(trip: TripPlanResult | undefined, reachableCount: number, t: TFunction): string {
  if (trip) {
    const along = trip.chargersAlongRouteCount;
    if (along === 0) return t("trip.stations.noneAlong");
    if (reachableCount === along) return t("trip.stations.alongAll", { count: along });
    return `${t("trip.stations.along", { count: along })} ${t("trip.stations.beforeLow", { count: reachableCount })}`;
  }
  return reachableCount > 0 ? t("trip.stations.withinReach", { count: reachableCount }) : t("trip.stations.noneInReach");
}

export default function ReachPanel(props: ReachPanelProps) {
  const { t } = useTranslation();
  const { number } = useLocale();
  const { data: vehicles, isLoading: vehiclesLoading } = useVehicles();
  const [vehicleId, setVehicleId] = useState("");
  const [batteryPercent, setBatteryPercent] = useState(80);
  const [lastInputs, setLastInputs] = useState<ReachInputs | null>(null);

  // Keep the latest callback without re-triggering effects on every render
  const onEstimateRef = useRef(props.onEstimate);
  useEffect(() => { onEstimateRef.current = props.onEstimate; });

  const run = (inputs: ReachInputs, fresh: boolean) => {
    setLastInputs(inputs); // the bar must reflect the inputs of THIS result
    onEstimateRef.current(inputs, { fresh });
  };

  // After the first check, or once a destination is set, any change recalculates automatically (debounced)
  const destinationKey = props.destination ? `${props.destination.lat},${props.destination.lng}` : null;
  const hasRun = lastInputs !== null;
  useEffect(() => {
    if (!vehicleId || (!hasRun && !destinationKey)) return;
    const timer = setTimeout(() => run({ vehicleId, batteryPercent }, false), 400);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only react to user inputs, not to the result itself
  }, [vehicleId, batteryPercent, destinationKey, props.originKey]);

  const trip = props.tripResult;
  const rangeKm = trip?.rangeKm ?? props.reachResult?.rangeKm;
  const reachableCount = (trip ?? props.reachResult)?.reachableStationIds.length ?? 0;
  const reserve = vehicles?.find((v) => v.id === (lastInputs?.vehicleId ?? vehicleId))?.socReservePercent ?? 10;
  const resultBattery = lastInputs?.batteryPercent ?? batteryPercent;
  const verdict = rangeKm !== undefined ? getVerdict(rangeKm, trip, reserve, t, (n) => number(n)) : null;
  const levelColor = batteryColor(batteryPercent);
  const VerdictIcon = verdict ? TONE_ICONS[verdict.tone].icon : null;

  return (
    <section
      aria-label={t("reach.panel")}
      // Mobile: a bottom sheet. Wider screens: a side card at the start side, below the zoom control.
      className="absolute inset-x-0 bottom-0 z-1000 max-h-[72%] overflow-y-auto rounded-t-2xl border-t bg-card p-4 pt-2 text-card-foreground shadow-2xl sm:inset-x-auto sm:top-24 sm:bottom-auto sm:inset-s-3 sm:max-h-[calc(100%-7.5rem)] sm:w-96 sm:rounded-2xl sm:border sm:p-5"
    >
      <div aria-hidden className="mx-auto mb-2 h-1.5 w-10 rounded-full bg-border sm:hidden" />
      <header className="mb-4 flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2.5 text-lg font-semibold">
          <span className="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground">
            <Zap className="size-4.5 fill-current" aria-hidden />
          </span>
          {t("reach.title")}
        </h2>
        <Button type="button" variant="ghost" size="icon" onClick={props.onClose} aria-label={t("common.close")}>
          <X className="size-5" />
        </Button>
      </header>

      <div className="space-y-4">
        <Select
          value={vehicleId}
          onValueChange={(v) => setVehicleId(v ?? "")}
          items={vehicles?.map((v) => ({ value: v.id, label: v.name })) ?? []}
        >
          <SelectTrigger className="h-10 w-full" aria-label={t("reach.yourCar")}>
            <SelectValue placeholder={vehiclesLoading ? t("reach.loadingCars") : t("reach.chooseCar")} />
          </SelectTrigger>
          <SelectContent>
            {vehicles?.map((v) => (
              <SelectItem key={v.id} value={v.id}>{v.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div>
          <div className="mb-2 flex items-baseline justify-between">
            <label htmlFor="battery" className="text-sm font-medium text-muted-foreground">{t("reach.battery")}</label>
            <span className="text-2xl font-semibold tabular-nums" style={{ color: levelColor }} dir="ltr">
              {batteryPercent}%
            </span>
          </div>
          {/* A gauge: fills left-to-right in every language, like the battery bar */}
          <input
            id="battery"
            type="range"
            dir="ltr"
            min={0}
            max={100}
            value={batteryPercent}
            onChange={(e) => setBatteryPercent(Number(e.target.value))}
            className="battery-range"
            style={{ "--level": `${batteryPercent}%`, "--level-color": levelColor } as React.CSSProperties}
          />
        </div>

        <div className="flex gap-3 rounded-xl border bg-muted/40 p-3 text-sm">
          <div className="flex flex-col items-center pt-1.5" aria-hidden>
            <span className="size-2.5 rounded-full bg-blue-600 ring-2 ring-blue-600/25" />
            <span className="my-1 h-9 border-s-2 border-dotted border-muted-foreground/40" />
            <MapPin className={cn("size-4", props.destination ? "text-fuchsia-600 dark:text-fuchsia-400" : "text-muted-foreground")} />
          </div>
          <div className="min-w-0 flex-1 space-y-3">
            <div>
              <p className="text-xs text-muted-foreground">{t("reach.from")}</p>
              <p className="font-medium">{props.originLabel}</p>
              {props.originIsMapCenter && (
                <p className="text-xs text-muted-foreground">{t("reach.locateHint")}</p>
              )}
            </div>
            <div>
              <p className="mb-1 text-xs text-muted-foreground">{t("reach.to")}</p>
              <PlaceSearch
                label={t("reach.destination")}
                value={props.destination}
                onSelect={props.onDestinationChange}
                onClear={() => props.onDestinationChange(null)}
                focus={props.searchFocus}
              />
            </div>
          </div>
        </div>

        <Button
          className="h-10 w-full text-base"
          disabled={!vehicleId || props.isPending}
          onClick={() => run({ vehicleId, batteryPercent }, true)}
        >
          {props.isPending ? t("reach.checking") : props.destination ? t("reach.checkTrip") : t("reach.checkRange")}
        </Button>
        {!vehicleId && <p className="-mt-2 text-center text-xs text-muted-foreground">{t("reach.chooseCarFirst")}</p>}

        {props.error && (
          <p role="alert" className="rounded-lg border border-danger/40 bg-danger/10 p-3 text-sm">
            {props.error}
          </p>
        )}

        {verdict && rangeKm !== undefined && (
          <div aria-live="polite" aria-busy={props.isPending} className={cn("space-y-3", props.isPending && "opacity-60")}>
            <div className={cn("flex gap-2.5 rounded-xl border p-3", TONE_STYLES[verdict.tone])}>
              {VerdictIcon && <VerdictIcon aria-hidden className={cn("mt-0.5 size-5 shrink-0", TONE_ICONS[verdict.tone].className)} />}
              <div className="min-w-0">
                <p className="text-base font-semibold">{verdict.title}</p>
                <p className="text-sm text-muted-foreground">{verdict.detail}</p>
              </div>
            </div>

            {trip && <TripFacts trip={trip} />}

            <TripBar battery={resultBattery} arrival={trip ? trip.batteryOnArrival : null} reserve={reserve} />

            {rangeKm > 0 && (
              <p className="text-sm">
                {stationLine(trip, reachableCount, t)}
                {trip && <span className="text-muted-foreground"> {t("trip.stations.fullRange", { km: number(Math.round(rangeKm)) })}</span>}
              </p>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
