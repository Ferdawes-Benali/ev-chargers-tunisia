import { useTranslation } from "react-i18next";
import { SearchX, TriangleAlert } from "lucide-react";
import { useStations } from "@/hooks/useStations";
import { useFilterStore } from "@/store/filterStore";
import StationCard from "@/components/StationCard";
import StationCardSkeleton from "@/components/StationCardSkeleton";
import FilterBar from "@/components/FilterBar";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { useLocale } from "@/lib/format";

export default function ListPage() {
  const { t } = useTranslation();
  const { number } = useLocale();
  const { connectorType, minPowerKw, setConnectorType, setMinPowerKw } = useFilterStore();
  const { data, isLoading, error } = useStations(1, 20, connectorType, minPowerKw);
  const hasFilters = connectorType !== null || minPowerKw !== null;

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:py-8">
      <div className="mb-5 flex flex-wrap items-end justify-between gap-2">
        <h1 className="text-2xl font-bold tracking-tight">{t("list.title")}</h1>
        {!isLoading && !error && data && data.total > 0 && (
          <p className="text-sm text-muted-foreground tabular-nums">
            {t("list.count", { count: data.total, formatted: number(data.total) })}
          </p>
        )}
      </div>
      <FilterBar />

      {isLoading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true" aria-label={t("common.loading")}>
          {Array.from({ length: 6 }).map((_, i) => (
            <StationCardSkeleton key={i} />
          ))}
        </div>
      )}

      {error && <EmptyState icon={TriangleAlert} tone="danger" title={t("list.loadError")} />}

      {!isLoading && !error && (!data || data.data.length === 0) && (
        <EmptyState
          icon={SearchX}
          title={t("list.emptyTitle")}
          text={hasFilters ? t("list.empty") : undefined}
          action={
            hasFilters && (
              <Button
                variant="outline"
                onClick={() => {
                  setConnectorType(null);
                  setMinPowerKw(null);
                }}
              >
                {t("list.filters.clear")}
              </Button>
            )
          }
        />
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
