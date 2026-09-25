import { useStations } from "@/hooks/useStations";
import { useFilterStore } from "@/store/filterStore";
import StationCard from "@/components/StationCard";
import StationCardSkeleton from "@/components/StationCardSkeleton";
import FilterBar from "@/components/FilterBar";

export default function ListPage() {
  const { connectorType, minPowerKw } = useFilterStore();
  const { data, isLoading, error } = useStations(1, 20, connectorType, minPowerKw);

  return (
    <div className="p-4">
      <h1 className="text-xl font-bold mb-4">Stations</h1>
      <FilterBar />

      {isLoading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <StationCardSkeleton key={i} />
          ))}
        </div>
      )}

      {error && (
        <div className="text-center text-muted-foreground">Couldn't load stations. Please try again.</div>
      )}

      {!isLoading && !error && (!data || data.data.length === 0) && (
        <div className="text-center text-muted-foreground">No stations match your filters.</div>
      )}

      {!isLoading && !error && data && data.data.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.data.map((s) => (
            <StationCard key={s.id} station={s} />
          ))}
        </div>
      )}
    </div>
  );
}