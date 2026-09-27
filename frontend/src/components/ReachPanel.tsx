import { useEffect, useRef, useState } from "react";
import { Clock, MapPin, Milestone, Route, Thermometer, X, Zap } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import PlaceSearch from "@/components/PlaceSearch";
import { cn } from "@/lib/utils";
import { batteryColor } from "@/lib/battery";
import { useVehicles } from "@/hooks/useReach";
import type { PlaceSuggestion, ReachEstimateResult, TripPlanResult } from "@/types/reach";
import "./ReachPanel.css";

export interface ReachInputs {
  vehicleId: string;
  batteryPercent: number;
}

interface ReachPanelProps {
  /** fresh = the user pressed the button, so the start point may be picked again */
  onEstimate: (inputs: ReachInputs, options: { fresh: boolean }) => void;
  reachResult: ReachEstimateResult | undefined;
  tripResult: TripPlanResult | undefined;
  isPending: boolean;
  error: string | null;
  originLabel: string;
  /** Changes when the start point changes, to recalculate */
  originKey: string;
  destination: PlaceSuggestion | null;
  onDestinationChange: (place: PlaceSuggestion | null) => void;
  searchFocus: { lat: number; lng: number } | null;
  onClose: () => void;
}

type Tone = "good" | "tight" | "bad" | "neutral";

const TONE_STYLES: Record<Tone, string> = {
  good: "border-emerald-200 bg-emerald-50 text-emerald-950",
  tight: "border-amber-200 bg-amber-50 text-amber-950",
  bad: "border-rose-200 bg-rose-50 text-rose-950",
  neutral: "border-sky-200 bg-sky-50 text-sky-950",
};

/** Turns raw numbers into the one thing the driver needs: a decision. */
function getVerdict(rangeKm: number, trip: TripPlanResult | undefined, reserve: number): { tone: Tone; title: string; detail: string } {
  const km = Math.round(rangeKm);

  if (rangeKm === 0) {
    return { tone: "bad", title: "Charge now", detail: "Your battery is at its reserve level. Find a charger before driving further." };
  }

  if (trip) {
    const arrival = Math.round(trip.batteryOnArrival);
    if (!trip.reachable) {
      const stop = trip.recommendedStop;
      if (stop) {
        return {
          tone: "bad",
          title: "Charge on the way",
          detail: `Stop at ${stop.name}, ${Math.round(stop.distanceAlongKm)} km into your trip. You'll arrive there with about ${Math.round(stop.batteryOnArrivalPercent)}%.`,
        };
      }
      const shortKm = Math.max(1, Math.ceil(trip.shortfallKm));
      return {
        tone: "bad",
        title: "Charge on the way",
        detail: `You'd run about ${shortKm} km short, and there's no known charger on this route before your battery runs low.`,
      };
    }
    if (trip.batteryOnArrival - reserve < 5) {
      return { tone: "tight", title: "Tight, but you'll make it", detail: `You'd arrive with about ${arrival}%, just above your reserve. A short charging stop is safer.` };
    }
    return { tone: "good", title: "You'll make it", detail: `You'll arrive with about ${arrival}% battery.` };
  }

  if (km < 20) {
    return { tone: "tight", title: `Only about ${km} km left`, detail: "Head to a charger now." };
  }
  return { tone: "neutral", title: `About ${km} km of driving`, detail: "Search for a destination to check a specific trip." };
}

function formatDuration(minutes: number) {
  const total = Math.max(1, Math.round(minutes));
  if (total < 60) return `${total} min`;
  const h = Math.floor(total / 60);
  const m = total % 60;
  return m === 0 ? `${h} h` : `${h} h ${m} min`;
}

/** Distance, time and the conditions detected automatically for the trip. */
function TripFacts({ trip }: { trip: TripPlanResult }) {
  const facts = [
    { icon: Route, text: `${Math.round(trip.distanceKm)} km` },
    { icon: Clock, text: formatDuration(trip.durationMinutes) },
    { icon: Milestone, text: trip.motorwayShare >= 0.5 ? "Mostly motorway" : "Mostly local roads" },
    ...(trip.temperatureC !== null ? [{ icon: Thermometer, text: `${Math.round(trip.temperatureC)} °C` }] : []),
  ];
  return (
    <ul aria-label="Trip details" className="flex flex-wrap gap-x-4 gap-y-1.5 text-sm text-slate-700">
      {facts.map(({ icon: Icon, text }) => (
        <li key={text} className="flex items-center gap-1.5">
          <Icon className="size-4 text-slate-500" aria-hidden />
          {text}
        </li>
      ))}
    </ul>
  );
}

/** A drawing of the battery before, during and after the trip. */
function TripBar({ battery, arrival, reserve }: { battery: number; arrival: number | null; reserve: number }) {
  const [filled, setFilled] = useState(false);

  // Replay the fill each time a new result arrives
  useEffect(() => {
    setFilled(false);
    const t = setTimeout(() => setFilled(true), 30);
    return () => clearTimeout(t);
  }, [battery, arrival]);

  const left = arrival === null ? battery : Math.max(0, Math.min(battery, arrival));
  const used = battery - left;
  const belowReserve = arrival !== null && arrival < reserve;

  return (
    <div>
      <div
        role="img"
        aria-label={
          arrival === null
            ? `${battery}% battery, ${reserve}% kept as reserve`
            : `Leaving with ${battery}%, arriving with ${Math.round(left)}%, reserve ${reserve}%`
        }
        className="relative h-6 overflow-hidden rounded-md border border-slate-300 bg-slate-100"
      >
        <div
          className="absolute inset-y-0 left-0 motion-safe:transition-[width] motion-safe:duration-700 motion-safe:ease-out"
          style={{ width: filled ? `${left}%` : "0%", backgroundColor: batteryColor(left) }}
        />
        {used > 0 && (
          <div
            className="absolute inset-y-0 motion-safe:transition-[width] motion-safe:delay-300 motion-safe:duration-700 motion-safe:ease-out"
            style={{
              left: `${left}%`,
              width: filled ? `${used}%` : "0%",
              background: "repeating-linear-gradient(135deg, #94a3b8 0 4px, #cbd5e1 4px 8px)",
            }}
          />
        )}
        <div
          className="absolute inset-y-0 left-0 border-r-2 border-dashed border-slate-700/70"
          style={{
            width: `${reserve}%`,
            background: "repeating-linear-gradient(135deg, rgba(15,23,42,.15) 0 3px, transparent 3px 6px)",
          }}
        />
      </div>
      <div className="mt-1.5 flex justify-between gap-2 text-xs tabular-nums text-slate-600">
        <span>Reserve {reserve}%</span>
        {arrival !== null &&
          (belowReserve
            ? <span className="font-medium text-rose-700">Below reserve on arrival</span>
            : <span>Arrive with {Math.round(left)}%</span>)}
        <span>Now {battery}%</span>
      </div>
    </div>
  );
}

export default function ReachPanel(props: ReachPanelProps) {
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
    const t = setTimeout(() => run({ vehicleId, batteryPercent }, false), 400);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only react to user inputs, not to the result itself
  }, [vehicleId, batteryPercent, destinationKey, props.originKey]);

  const trip = props.tripResult;
  const rangeKm = trip?.rangeKm ?? props.reachResult?.rangeKm;
  const reachableCount = (trip ?? props.reachResult)?.reachableStationIds.length ?? 0;
  const plural = (n: number) => `${n} charger${n === 1 ? "" : "s"}`;
  const stationLine = trip
    ? trip.chargersAlongRouteCount === 0
      ? "No known chargers along your route."
      : reachableCount === trip.chargersAlongRouteCount
        ? `${plural(trip.chargersAlongRouteCount)} along your route, shown as green pins.`
        : `${plural(trip.chargersAlongRouteCount)} along your route. ${reachableCount} before your battery runs low, shown as green pins.`
    : reachableCount > 0
      ? `${plural(reachableCount)} within reach, shown as green pins.`
      : "No chargers within reach from here.";
  const reserve = vehicles?.find((v) => v.id === (lastInputs?.vehicleId ?? vehicleId))?.socReservePercent ?? 10;
  const resultBattery = lastInputs?.batteryPercent ?? batteryPercent;
  const verdict = rangeKm !== undefined ? getVerdict(rangeKm, trip, reserve) : null;
  const levelColor = batteryColor(batteryPercent);

  return (
    <section
      aria-label="Check my range"
      className="absolute bottom-3 left-3 right-3 z-1000 max-h-[calc(100vh-110px)] overflow-y-auto rounded-xl border bg-white p-4 shadow-xl sm:right-auto sm:w-88"
    >
      <header className="mb-4 flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-lg font-semibold">
          <Zap className="size-5 text-emerald-600" aria-hidden />
          Can I make it?
        </h2>
        <button
          type="button"
          onClick={props.onClose}
          aria-label="Close"
          className="rounded-md p-1 text-slate-500 hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-slate-900"
        >
          <X className="size-5" />
        </button>
      </header>

      <div className="space-y-4">
        <Select
          value={vehicleId}
          onValueChange={(v) => setVehicleId(v ?? "")}
          items={vehicles?.map((v) => ({ value: v.id, label: v.name })) ?? []}
        >
          <SelectTrigger className="w-full" aria-label="Your car">
            <SelectValue placeholder={vehiclesLoading ? "Loading cars…" : "Choose your car"} />
          </SelectTrigger>
          <SelectContent>
            {vehicles?.map((v) => (
              <SelectItem key={v.id} value={v.id}>{v.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div>
          <div className="mb-2 flex items-baseline justify-between">
            <label htmlFor="battery" className="text-sm text-slate-700">Battery</label>
            <span className="text-2xl font-semibold tabular-nums" style={{ color: levelColor }}>
              {batteryPercent}%
            </span>
          </div>
          <input
            id="battery"
            type="range"
            min={0}
            max={100}
            value={batteryPercent}
            onChange={(e) => setBatteryPercent(Number(e.target.value))}
            className="battery-range"
            style={{ "--level": `${batteryPercent}%`, "--level-color": levelColor } as React.CSSProperties}
          />
        </div>

        <div className="flex gap-3 rounded-lg border p-3 text-sm">
          <div className="flex flex-col items-center pt-1.5" aria-hidden>
            <span className="size-2.5 rounded-full bg-blue-600" />
            <span className="my-1 h-9 border-l-2 border-dotted border-slate-300" />
            <MapPin className={cn("size-4", props.destination ? "text-fuchsia-600" : "text-slate-400")} />
          </div>
          <div className="min-w-0 flex-1 space-y-3">
            <div>
              <p className="text-xs text-slate-500">From</p>
              <p className="font-medium">{props.originLabel}</p>
              {props.originLabel === "Map center" && (
                <p className="text-xs text-slate-500">Tap "Locate me" for a precise start.</p>
              )}
            </div>
            <div>
              <p className="mb-1 text-xs text-slate-500">To (optional)</p>
              <PlaceSearch
                label="Destination"
                value={props.destination}
                onSelect={props.onDestinationChange}
                onClear={() => props.onDestinationChange(null)}
                focus={props.searchFocus}
              />
            </div>
          </div>
        </div>

        <Button
          className="w-full"
          disabled={!vehicleId || props.isPending}
          onClick={() => run({ vehicleId, batteryPercent }, true)}
        >
          {props.isPending ? "Checking…" : props.destination ? "Check trip" : "Check range"}
        </Button>
        {!vehicleId && <p className="-mt-2 text-center text-xs text-slate-500">Choose your car to start.</p>}

        {props.error && (
          <p role="alert" className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm text-rose-900">
            {props.error}
          </p>
        )}

        {verdict && rangeKm !== undefined && (
          <div aria-live="polite" aria-busy={props.isPending} className={cn("space-y-3", props.isPending && "opacity-60")}>
            <div className={cn("rounded-lg border p-3", TONE_STYLES[verdict.tone])}>
              <p className="text-base font-semibold">{verdict.title}</p>
              <p className="text-sm">{verdict.detail}</p>
            </div>

            {trip && <TripFacts trip={trip} />}

            <TripBar battery={resultBattery} arrival={trip ? trip.batteryOnArrival : null} reserve={reserve} />

            {rangeKm > 0 && (
              <p className="text-sm text-slate-700">
                {stationLine}
                {trip && <span className="text-slate-500"> Full range about {Math.round(rangeKm)} km.</span>}
              </p>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
