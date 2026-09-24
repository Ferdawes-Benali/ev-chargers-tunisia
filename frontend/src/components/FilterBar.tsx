import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useFilterStore } from "@/store/filterStore";

const CONNECTOR_TYPES = ["Type2", "CCS", "CHAdeMO", "Tesla"];
const POWER_OPTIONS = [
  { label: "Any power", value: "0" },
  { label: "22+ kW", value: "22" },
  { label: "50+ kW", value: "50" },
  { label: "100+ kW", value: "100" },
];

export default function FilterBar() {
  const { connectorType, minPowerKw, setConnectorType, setMinPowerKw } = useFilterStore();

  return (
    <div className="flex gap-3 mb-4">
      <Select
        value={connectorType ?? "all"}
        onValueChange={(v) => setConnectorType(v === "all" ? null : v)}
      >
        <SelectTrigger className="w-40">
          <SelectValue placeholder="Connector type" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All connectors</SelectItem>
          {CONNECTOR_TYPES.map((type) => (
            <SelectItem key={type} value={type}>{type}</SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        value={String(minPowerKw ?? 0)}
        onValueChange={(v) => setMinPowerKw(v === "0" ? null : Number(v))}
      >
        <SelectTrigger className="w-40">
          <SelectValue placeholder="Min power" />
        </SelectTrigger>
        <SelectContent>
          {POWER_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}