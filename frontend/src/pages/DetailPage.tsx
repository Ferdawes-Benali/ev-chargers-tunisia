import { Link, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Building2, Heart, MapPin, Navigation, Plug, PlugZap, SearchX, ShieldCheck, Star } from "lucide-react";
import { useStation } from "@/hooks/useStation";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/hooks/useAuth";
import { useProfile, useToggleFavorite } from "@/hooks/useProfile";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useLocale } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import ReviewsSection from "@/components/ReviewsSection";
import ChargingCompanion from "@/components/ChargingCompanion";
import StatusBadge from "@/components/StatusBadge";
import SectionCard from "@/components/SectionCard";
import EmptyState from "@/components/EmptyState";

export default function DetailPage() {
  const { t } = useTranslation();
  const { number } = useLocale();
  const { id } = useParams();
  const { data: station, isLoading, error } = useStation(id);
  const { isLoggedIn } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const toggleFavorite = useToggleFavorite();
  const queryClient = useQueryClient();

  const verifyMutation = useMutation({
    mutationFn: async () => api.post(`/api/v1/stations/${id}/verify`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["station", id] });
    },
  });

  if (isLoading) {
    return (
      <div className="mx-auto max-w-4xl space-y-6 px-4 py-6 sm:py-8" aria-busy="true" aria-label={t("detail.loading")}>
        <div className="space-y-4 rounded-2xl border bg-card p-5 sm:p-6">
          <div className="flex items-start gap-4">
            <Skeleton className="size-14 shrink-0 rounded-2xl" />
            <div className="flex-1 space-y-2.5 pt-1">
              <Skeleton className="h-7 w-2/3" />
              <Skeleton className="h-4 w-1/2" />
            </div>
          </div>
          <Skeleton className="h-10 w-40" />
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 rounded-xl" />
          ))}
        </div>
        <Skeleton className="h-32 w-full rounded-xl" />
      </div>
    );
  }

  if (error || !station) {
    return (
      <div className="mx-auto max-w-4xl px-4 py-10">
        <EmptyState
          icon={SearchX}
          title={t("detail.notFound")}
          action={
            <Link to="/stations" className={buttonVariants({ variant: "outline" })}>
              {t("detail.backToList")}
            </Link>
          }
        />
      </div>
    );
  }

  // Direct turn-by-turn navigation to the charger (from the user's current position)
  const directionsUrl = `https://www.google.com/maps/dir/?api=1&destination=${station.lat},${station.lng}&travelmode=driving`;

  const isFavorite = profile?.favoriteStationIds.includes(station.id) ?? false;
  // Display only: the strongest connector and how many plugs there are in total
  const maxPowerKw = station.connectors.length > 0 ? Math.max(...station.connectors.map((c) => c.powerKw)) : null;
  const plugCount = station.connectors.reduce((sum, c) => sum + c.count, 0);
  const rating = station.avgRating !== null
    ? number(station.avgRating, { minimumFractionDigits: 1, maximumFractionDigits: 1 })
    : null;

  return (
    <div className="mx-auto max-w-4xl space-y-6 px-4 py-6 sm:py-8">
      <header className="relative overflow-hidden rounded-2xl border bg-card shadow-sm">
        {/* No photos exist: a themed gradient and a large charger glyph instead */}
        <div aria-hidden="true" className="absolute inset-0 bg-linear-to-br from-primary/20 via-accent/8 to-transparent" />
        <PlugZap aria-hidden="true" className="absolute -bottom-6 inset-e-4 size-40 text-primary/10" strokeWidth={1.25} />

        <div className="relative space-y-5 p-5 sm:p-6">
          <div className="flex items-start gap-4">
            <span className="grid size-14 shrink-0 place-items-center rounded-2xl bg-primary text-primary-foreground shadow-md">
              <PlugZap aria-hidden="true" className="size-7" />
            </span>
            <div className="min-w-0 flex-1 space-y-1.5">
              <h1 className="text-2xl font-bold tracking-tight wrap-break-word sm:text-3xl"><bdi>{station.name}</bdi></h1>
              {station.address && (
                <p className="flex items-start gap-1.5 text-muted-foreground">
                  <MapPin aria-hidden="true" className="mt-1 size-4 shrink-0" />
                  <span>{station.address}</span>
                </p>
              )}
              {station.operatorName && (
                <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
                  <Building2 aria-hidden="true" className="size-4 shrink-0" />
                  {t("detail.operator", { name: station.operatorName })}
                </p>
              )}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <a
              href={directionsUrl}
              target="_blank"
              rel="noopener noreferrer"
              className={buttonVariants({ className: "h-10 gap-2 px-4 text-base" })}
            >
              <Navigation aria-hidden="true" className="size-4.5 fill-current rtl:-scale-x-100" />
              {t("detail.directions")}
            </a>
            {isLoggedIn && (
              <Button
                variant="outline"
                size="icon-lg"
                className="size-10 rounded-full"
                onClick={() => toggleFavorite.mutate({ stationId: station.id, isFavorite })}
                aria-pressed={isFavorite}
                aria-label={t(isFavorite ? "list.removeFavorite" : "list.addFavorite", { name: station.name })}
              >
                <Heart aria-hidden="true" className={cn("size-5", isFavorite ? "fill-danger text-danger" : "text-muted-foreground")} />
              </Button>
            )}
          </div>
        </div>
      </header>

      {profile?.isAdmin && station.status === "Pending" && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dashed border-warning/60 bg-warning/10 p-3 ps-4">
          <p className="flex items-center gap-2 text-sm font-medium">
            <ShieldCheck aria-hidden="true" className="size-5 shrink-0 text-warning-ink" />
            {t("detail.adminPending")}
          </p>
          <Button variant="outline" onClick={() => verifyMutation.mutate()} disabled={verifyMutation.isPending}>
            <ShieldCheck aria-hidden="true" />
            {verifyMutation.isPending ? t("detail.verifying") : t("detail.verify")}
          </Button>
        </div>
      )}

      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Fact label={t("detail.facts.status")}>
          <StatusBadge status={station.status} />
        </Fact>
        <Fact label={t("detail.facts.maxPower")}>
          {maxPowerKw !== null ? t("detail.powerKw", { power: number(maxPowerKw) }) : t("detail.facts.unknown")}
        </Fact>
        <Fact label={t("detail.facts.connectors")}>{number(plugCount)}</Fact>
        <Fact label={t("detail.facts.rating")}>
          {rating !== null ? (
            <span className="inline-flex items-center gap-1.5" aria-label={t("common.ratingShort", { rating })}>
              <Star aria-hidden="true" className="size-4.5 fill-warning text-warning" />
              {rating}
            </span>
          ) : (
            <span className="text-sm font-normal text-muted-foreground">{t("common.noReviews")}</span>
          )}
        </Fact>
      </dl>

      <SectionCard title={t("detail.connectors")} icon={Plug}>
        {station.connectors.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("detail.noConnectors")}</p>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2">
            {station.connectors.map((c, i) => (
              <li key={i} className="flex items-center gap-3 rounded-lg border bg-muted/40 p-3">
                <span className="sr-only">{t("detail.connectorLine", { type: c.type, power: number(c.powerKw), count: c.count })}</span>
                <span aria-hidden="true" className="grid size-9 shrink-0 place-items-center rounded-lg bg-card text-success-ink ring-1 ring-border">
                  <PlugZap className="size-4.5" />
                </span>
                <span aria-hidden="true" className="min-w-0 flex-1">
                  <span className="block font-medium">{c.type}</span>
                  <span className="block text-sm text-muted-foreground tabular-nums">{t("detail.powerKw", { power: number(c.powerKw) })}</span>
                </span>
                <span aria-hidden="true" className="rounded-full bg-card px-2.5 py-1 text-sm font-semibold tabular-nums ring-1 ring-border">
                  {t("detail.connectorCount", { value: number(c.count) })}
                </span>
              </li>
            ))}
          </ul>
        )}
      </SectionCard>

      <ChargingCompanion station={station} />

      <ReviewsSection station={station} />
    </div>
  );
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border bg-card p-3.5 shadow-xs">
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1.5 text-lg font-semibold tabular-nums">{children}</dd>
    </div>
  );
}
