import { useTranslation } from "react-i18next";
import { Gauge, PlugZap, SlidersHorizontal } from "lucide-react";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useFilterStore } from "@/store/filterStore";
import { cn } from "@/lib/utils";

// Connector names are standards: never translated
const CONNECTOR_TYPES = ["Type2", "CCS", "CHAdeMO", "Tesla"];
const POWER_OPTIONS = ["0", "22", "50", "100"];

/** Filter "chip": a rounded Select trigger, highlighted while a filter is applied. */
const chipClass = (active: boolean) =>
  cn(
    "h-9 min-w-44 rounded-full ps-3 pe-2.5 font-medium",
    active && "border-primary/60 bg-primary/12 hover:bg-primary/20 dark:hover:bg-primary/20",
  );

export default function FilterBar() {
  const { t } = useTranslation();
  const { connectorType, minPowerKw, setConnectorType, setMinPowerKw } = useFilterStore();

  const powerLabel = (value: string) =>
    value === "0" ? t("list.filters.anyPower") : t("list.filters.powerAtLeast", { power: value });

  return (
    <div className="mb-6 flex flex-wrap items-center gap-2 rounded-xl border bg-card p-2.5 shadow-xs">
      <span className="flex items-center gap-1.5 px-1.5 text-sm font-medium text-muted-foreground">
        <SlidersHorizontal aria-hidden="true" className="size-4" />
        {t("list.filters.title")}
      </span>
      <Select
        value={connectorType ?? "all"}
        onValueChange={(v) => setConnectorType(v === "all" ? null : v)}
        items={[
          { value: "all", label: t("list.filters.allConnectors") },
          ...CONNECTOR_TYPES.map((type) => ({ value: type, label: type })),
        ]}
      >
        <SelectTrigger className={chipClass(connectorType !== null)} aria-label={t("list.filters.connector")}>
          <PlugZap aria-hidden="true" className="text-muted-foreground" />
          <SelectValue placeholder={t("list.filters.connector")} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("list.filters.allConnectors")}</SelectItem>
          {CONNECTOR_TYPES.map((type) => (
            <SelectItem key={type} value={type}>{type}</SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        value={String(minPowerKw ?? 0)}
        onValueChange={(v) => setMinPowerKw(v === "0" ? null : Number(v))}
        items={POWER_OPTIONS.map((value) => ({ value, label: powerLabel(value) }))}
      >
        <SelectTrigger className={chipClass(minPowerKw !== null)} aria-label={t("list.filters.minPower")}>
          <Gauge aria-hidden="true" className="text-muted-foreground" />
          <SelectValue placeholder={t("list.filters.minPower")} className="tabular-nums" />
        </SelectTrigger>
        <SelectContent>
          {POWER_OPTIONS.map((value) => (
            <SelectItem key={value} value={value} className="tabular-nums">{powerLabel(value)}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
