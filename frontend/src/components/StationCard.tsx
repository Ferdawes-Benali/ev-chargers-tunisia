import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ChevronRight, Heart, Star, Zap } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import StatusBadge from "@/components/StatusBadge";
import { useAuth } from "@/hooks/useAuth";
import { useProfile, useToggleFavorite } from "@/hooks/useProfile";
import { useLocale } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { StationListItem } from "@/types/station";

export default function StationCard({ station }: { station: StationListItem }) {
  const { t } = useTranslation();
  const { number } = useLocale();
  const { isLoggedIn } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const toggleFavorite = useToggleFavorite();

  const isFavorite = profile?.favoriteStationIds.includes(station.id) ?? false;

  const handleFavoriteClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleFavorite.mutate({ stationId: station.id, isFavorite });
  };

  const rating = station.avgRating !== null ? number(station.avgRating, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) : null;

  return (
    <Card className="relative gap-0 py-0 transition-[box-shadow,transform] hover:-translate-y-0.5 hover:shadow-md hover:ring-primary/40 motion-reduce:hover:translate-y-0">
      <Link
        to={`/stations/${station.id}`}
        className="flex h-full flex-col gap-3 rounded-xl p-4 outline-none focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:ring-inset"
      >
        <div className={cn("flex items-start gap-3", isLoggedIn && "pe-9")}>
          <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary/12 text-success-ink">
            <Zap aria-hidden="true" className="size-5" />
          </span>
          <p className="line-clamp-2 min-w-0 flex-1 pt-0.5 font-semibold leading-snug"><bdi>{station.name}</bdi></p>
        </div>
        <div className="mt-auto flex items-center justify-between gap-2">
          <StatusBadge status={station.status} />
          <span className="flex items-center gap-1 text-sm text-muted-foreground">
            {rating !== null ? (
              <span aria-label={t("common.ratingShort", { rating })} className="inline-flex items-center gap-1 font-medium text-foreground tabular-nums">
                <Star aria-hidden="true" className="size-4 fill-warning text-warning" />
                {rating}
              </span>
            ) : (
              t("common.noReviews")
            )}
            <ChevronRight aria-hidden="true" className="size-4 rtl:-scale-x-100" />
          </span>
        </div>
      </Link>
      {isLoggedIn && (
        <Button
          size="icon"
          variant="ghost"
          className="absolute top-3 inset-e-3 rounded-full"
          onClick={handleFavoriteClick}
          aria-pressed={isFavorite}
          aria-label={t(isFavorite ? "list.removeFavorite" : "list.addFavorite", { name: station.name })}
        >
          <Heart aria-hidden="true" className={cn("size-5", isFavorite ? "fill-danger text-danger" : "text-muted-foreground")} />
        </Button>
      )}
    </Card>
  );
}
