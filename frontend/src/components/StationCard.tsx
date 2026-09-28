import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/hooks/useAuth";
import { useProfile, useToggleFavorite } from "@/hooks/useProfile";
import { useLocale } from "@/lib/format";
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
    <Card className="hover:shadow-md transition-shadow relative">
      <Link to={`/stations/${station.id}`}>
        <CardHeader>
          <CardTitle className="flex items-center justify-between gap-2 pe-8">
            <span>{station.name}</span>
            <Badge variant={station.status === "Verified" ? "default" : "secondary"}>
              {t(`common.status.${station.status}`, { defaultValue: station.status })}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            {rating !== null
              ? <span aria-label={t("common.ratingShort", { rating })}>★ {rating}</span>
              : t("common.noReviews")}
          </p>
        </CardContent>
      </Link>
      {isLoggedIn && (
        <Button
          size="sm"
          variant="ghost"
          className="absolute top-2 inset-e-2"
          onClick={handleFavoriteClick}
          aria-pressed={isFavorite}
          aria-label={t(isFavorite ? "list.removeFavorite" : "list.addFavorite", { name: station.name })}
        >
          <span aria-hidden="true">{isFavorite ? "♥" : "♡"}</span>
        </Button>
      )}
    </Card>
  );
}
