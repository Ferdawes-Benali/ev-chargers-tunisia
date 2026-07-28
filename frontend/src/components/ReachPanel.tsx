import { useEffect, useRef, useState } from "react";
import { Building2, Route, Snowflake, MapPin, X, Zap } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { cn } from "@/lib/utils";
import { useVehicles } from "@/hooks/useReach";
import type { DrivingCondition, ReachEstimateResult } from "@/types/reach";
import "./ReachPanel.css";

export interface ReachInputs {
  vehicleId: string;
  batteryPercent: number;
  drivingCondition: DrivingCondition;
}

interface ReachPanelProps {
  onEstimate: (inputs: ReachInputs) => void;
  result: ReachEstimateResult | undefined;
  isPending: boolean;
  isError: boolean;
  originLabel: string;
  destinationKey: string | null;
  pickingDestination: boolean;
  onTogglePickDestination: () => void;
  onClearDestination: () => void;
  onClose: () => void;
}

const CONDITIONS = [
  { value: "city", label: "City", icon: Building2 },
  { value: "highway", label: "Highway", icon: Route },
  { value: "cold", label: "Cold", icon: Snowflake },
] as const;

type Tone = "good" | "tight" | "bad" | "neutral";

const TONE_STYLES: Record<Tone, string> = {
  good: "border-emerald-200 bg-emerald-50 text-emerald-950",
  tight: "border-amber-200 bg-amber-50 text-amber-950",
  bad: "border-rose-200 bg-rose-50 text-rose-950",
  neutral: "border-sky-200 bg-sky-50 text-sky-950",
};

/** Battery color by level: green, then amber, then red. */
function batteryColor(percent: number) {
  if (percent <= 15) return "#D1495B";
  if (percent <= 35) return "#E8A33D";
  return "#0E9F6E";
}

/** Turns raw numbers into the one thing the driver needs: a decision. */
function getVerdict(r: ReachEstimateResult, battery: number, reserve: number): { tone: Tone; title: string; detail: string } {
  const km = Math.round(r.rangeKm);

  if (r.rangeKm === 0) {
    return { tone: "bad", title: "Charge now", detail: "Your battery is at its reserve level. Find a charger before driving further." };
  }

  if (r.destinationReachable !== null && r.batteryPercentOnArrival !== null) {
    const arrival = Math.round(r.batteryPercentOnArrival);
    if (!r.destinationReachable) {
      // Convert the missing battery % into kilometres: the range covers (battery - reserve) %
      const usable = battery - reserve;
      const shortKm = usable > 0
        ? Math.max(1, Math.ceil(((reserve - r.batteryPercentOnArrival) * r.rangeKm) / usable))
        : 0;
      return { tone: "bad", title: "Charge on the way", detail: `You'd run about ${shortKm} km short of your destination.` };
    }
    if (r.batteryPercentOnArrival - reserve < 5) {
      return { tone: "tight", title: "Tight, but you'll make it", detail: `You'd arrive with about ${arrival}%, just above your reserve. A short charging stop is safer.` };
    }
    return { tone: "good", title: "You'll make it", detail: `You'll arrive with about ${arrival}% battery.` };
  }

  if (km < 20) {
    return { tone: "tight", title: `Only about ${km} km left`, detail: "Head to a charger now." };
  }
  return { tone: "neutral", title: `About ${km} km of driving`, detail: "Set a destination to check a specific trip." };
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
  const [condition, setCondition] = useState<DrivingCondition>("city");
  const [lastInputs, setLastInputs] = useState<ReachInputs | null>(null);

  // Keep the latest callback without re-triggering effects on every render
  const onEstimateRef = useRef(props.onEstimate);
  useEffect(() => { onEstimateRef.current = props.onEstimate; });

  const run = (inputs: ReachInputs) => {
    setLastInputs(inputs); // the bar must reflect the inputs of THIS result
    onEstimateRef.current(inputs);
  };

  // After the first check, any change recalculates automatically (debounced)
  const hasResult = !!props.result;
  useEffect(() => {
    if (!hasResult || !vehicleId) return;
    const t = setTimeout(() => run({ vehicleId, batteryPercent, drivingCondition: condition }), 400);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only react to user inputs, not to the result itself
  }, [vehicleId, batteryPercent, condition, props.destinationKey]);

  const r = props.result;
  const reserve = vehicles?.find((v) => v.id === (lastInputs?.vehicleId ?? vehicleId))?.socReservePercent ?? 10;
  const resultBattery = lastInputs?.batteryPercent ?? batteryPercent;
  const verdict = r ? getVerdict(r, resultBattery, reserve) : null;
  const levelColor = batteryColor(batteryPercent);

  return (
    <section
      aria-label="Check my range"
      className="absolute bottom-3 left-3 right-3 z-[1000] max-h-[calc(100vh-110px)] overflow-y-auto rounded-xl border bg-white p-4 shadow-xl sm:right-auto sm:w-[22rem]"
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

        <div role="group" aria-label="Driving conditions" className="grid grid-cols-3 gap-1 rounded-lg bg-slate-100 p-1">
          {CONDITIONS.map(({ value, label, icon: Icon }) => (
            <button
              key={value}
              type="button"
              aria-pressed={condition === value}
              onClick={() => setCondition(value)}
              className={cn(
                "flex items-center justify-center gap-1.5 rounded-md py-1.5 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-slate-900",
                condition === value ? "bg-white font-medium text-slate-900 shadow-sm" : "text-slate-600 hover:text-slate-900"
              )}
            >
              <Icon className="size-4" aria-hidden />
              {label}
            </button>
          ))}
        </div>

        <div className="flex gap-3 rounded-lg border p-3 text-sm">
          <div className="flex flex-col items-center pt-1.5" aria-hidden>
            <span className="size-2.5 rounded-full bg-blue-600" />
            <span className="my-1 h-7 border-l-2 border-dotted border-slate-300" />
            <MapPin className={cn("size-4", props.destinationKey ? "text-fuchsia-600" : "text-slate-400")} />
          </div>
          <div className="flex-1 space-y-3">
            <div>
              <p className="text-xs text-slate-500">From</p>
              <p className="font-medium">{props.originLabel}</p>
              {props.originLabel === "Map center" && (
                <p className="text-xs text-slate-500">Tap "Locate me" for a precise start.</p>
              )}
            </div>
            <div className="flex items-center justify-between gap-2">
              <div>
                <p className="text-xs text-slate-500">To</p>
                <p className={cn("font-medium", !props.destinationKey && "text-slate-500")}>
                  {props.pickingDestination
                    ? "Tap a spot on the map…"
                    : props.destinationKey ? "Point on the map" : "Anywhere (optional)"}
                </p>
              </div>
              <div className="flex items-center gap-1">
                <Button size="sm" variant="outline" onClick={props.onTogglePickDestination}>
                  {props.pickingDestination ? "Cancel" : props.destinationKey ? "Change" : "Set on map"}
                </Button>
                {props.destinationKey && (
                  <button
                    type="button"
                    onClick={props.onClearDestination}
                    aria-label="Remove destination"
                    className="rounded-md p-1 text-slate-500 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-slate-900"
                  >
                    <X className="size-4" />
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>

        <Button
          className="w-full"
          disabled={!vehicleId || props.isPending}
          onClick={() => run({ vehicleId, batteryPercent, drivingCondition: condition })}
        >
          {props.isPending ? "Checking…" : "Check range"}
        </Button>
        {!vehicleId && <p className="-mt-2 text-center text-xs text-slate-500">Choose your car to start.</p>}

        {props.isError && (
          <p className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm text-rose-900">
            Couldn't check your range. Check your connection and try again.
          </p>
        )}

        {r && verdict && (
          <div aria-live="polite" aria-busy={props.isPending} className={cn("space-y-3", props.isPending && "opacity-60")}>
            <div className={cn("rounded-lg border p-3", TONE_STYLES[verdict.tone])}>
              <p className="text-base font-semibold">{verdict.title}</p>
              <p className="text-sm">{verdict.detail}</p>
            </div>

            <TripBar battery={resultBattery} arrival={r.batteryPercentOnArrival} reserve={reserve} />

            {r.rangeKm > 0 && (
              <p className="text-sm text-slate-700">
                {r.reachableStationIds.length > 0
                  ? `${r.reachableStationIds.length} charger${r.reachableStationIds.length > 1 ? "s" : ""} within reach, shown as green pins.`
                  : "No chargers within reach from here."}
                {r.destinationReachable !== null && (
                  <span className="text-slate-500"> Full range about {Math.round(r.rangeKm)} km.</span>
                )}
              </p>
            )}

            {r.rangeKm > 0 && (
              <details className="text-xs text-slate-600">
                <summary className="cursor-pointer select-none font-medium text-slate-700">How to read the map</summary>
                <ul className="mt-2 space-y-1.5">
                  <li className="flex items-center gap-2">
                    <span className="size-2.5 shrink-0 rounded-full bg-blue-600" aria-hidden />Your starting point
                  </li>
                  <li className="flex items-center gap-2">
                    <span className="w-4 shrink-0 border-t-2 border-dashed border-blue-600" aria-hidden />Straight-line estimate of your range
                  </li>
                  <li className="flex items-center gap-2">
                    <span className="size-3 shrink-0 rounded-sm border border-green-600 bg-green-600/30" aria-hidden />
                    Roads you can reach{r.rangeKm > 120 ? " (drawn up to 120 km)" : ""}
                  </li>
                  <li className="flex items-center gap-2">
                    <span className="size-2.5 shrink-0 rounded-full bg-green-600" aria-hidden />Chargers within reach
                  </li>
                </ul>
              </details>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
