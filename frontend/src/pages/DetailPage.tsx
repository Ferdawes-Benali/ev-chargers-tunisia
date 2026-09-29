import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ArrowRight } from "lucide-react";
import { useStation } from "@/hooks/useStation";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/hooks/useAuth";
import { useProfile } from "@/hooks/useProfile";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useLocale } from "@/lib/format";
import { Button } from "@/components/ui/button";
import ReviewsSection from "@/components/ReviewsSection";
import ChargingCompanion from "@/components/ChargingCompanion";

export default function DetailPage() {
  const { t } = useTranslation();
  const { number } = useLocale();
  const { id } = useParams();
  const { data: station, isLoading, error } = useStation(id);
  const { isLoggedIn } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const queryClient = useQueryClient();

  const verifyMutation = useMutation({
    mutationFn: async () => api.post(`/api/v1/stations/${id}/verify`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["station", id] });
    },
  });

  if (isLoading) {
    return (
      <div className="p-4 space-y-3" aria-busy="true" aria-label={t("detail.loading")}>
        <Skeleton className="h-8 w-1/2" />
        <Skeleton className="h-4 w-1/3" />
        <Skeleton className="h-24 w-full" />
      </div>
    );
  }

  if (error || !station) {
    return <div className="p-4 text-center text-muted-foreground">{t("detail.notFound")}</div>;
  }

  // Direct turn-by-turn navigation to the charger (from the user's current position)
  const directionsUrl = `https://www.google.com/maps/dir/?api=1&destination=${station.lat},${station.lng}&travelmode=driving`;

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-2xl font-bold">{station.name}</h1>
        <Badge variant={station.status === "Verified" ? "default" : "secondary"}>
          {t(`common.status.${station.status}`, { defaultValue: station.status })}
        </Badge>
      </div>

      {profile?.isAdmin && station.status === "Pending" && (
        <Button size="sm" onClick={() => verifyMutation.mutate()} disabled={verifyMutation.isPending}>
          {verifyMutation.isPending ? t("detail.verifying") : t("detail.verify")}
        </Button>
      )}

      {station.address && <p className="text-muted-foreground">{station.address}</p>}
      {station.operatorName && <p className="text-sm">{t("detail.operator", { name: station.operatorName })}</p>}

      <div>
        <h2 className="font-semibold mb-2">{t("detail.connectors")}</h2>
        {station.connectors.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("detail.noConnectors")}</p>
        ) : (
          <ul className="space-y-1">
            {station.connectors.map((c, i) => (
              <li key={i} className="text-sm">
                {t("detail.connectorLine", { type: c.type, power: number(c.powerKw), count: c.count })}
              </li>
            ))}
          </ul>
        )}
      </div>

      <a
        href={directionsUrl}
        target="_blank"
        rel="noopener noreferrer"
        className="inline-flex items-center gap-1 underline text-sm"
      >
        {t("detail.directions")}
        <ArrowRight aria-hidden="true" className="size-4 rtl:-scale-x-100" />
      </a>

      <ChargingCompanion station={station} />

      <ReviewsSection station={station} />
    </div>
  );
}
