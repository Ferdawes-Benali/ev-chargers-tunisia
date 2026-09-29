import { useTranslation } from "react-i18next";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useFilterStore } from "@/store/filterStore";

// Connector names are standards: never translated
const CONNECTOR_TYPES = ["Type2", "CCS", "CHAdeMO", "Tesla"];
const POWER_OPTIONS = ["0", "22", "50", "100"];

export default function FilterBar() {
  const { t } = useTranslation();
  const { connectorType, minPowerKw, setConnectorType, setMinPowerKw } = useFilterStore();

  const powerLabel = (value: string) =>
    value === "0" ? t("list.filters.anyPower") : t("list.filters.powerAtLeast", { power: value });

  return (
    <div className="flex flex-wrap gap-3 mb-4">
      <Select
        value={connectorType ?? "all"}
        onValueChange={(v) => setConnectorType(v === "all" ? null : v)}
        items={[
          { value: "all", label: t("list.filters.allConnectors") },
          ...CONNECTOR_TYPES.map((type) => ({ value: type, label: type })),
        ]}
      >
        <SelectTrigger className="w-44" aria-label={t("list.filters.connector")}>
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
        <SelectTrigger className="w-44" aria-label={t("list.filters.minPower")}>
          <SelectValue placeholder={t("list.filters.minPower")} />
        </SelectTrigger>
        <SelectContent>
          {POWER_OPTIONS.map((value) => (
            <SelectItem key={value} value={value}>{powerLabel(value)}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
