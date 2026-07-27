import { Link } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/hooks/useAuth";
import { useProfile, useToggleFavorite } from "@/hooks/useProfile";
import type { StationListItem } from "@/types/station";

export default function StationCard({ station }: { station: StationListItem }) {
  const { isLoggedIn } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const toggleFavorite = useToggleFavorite();

  const isFavorite = profile?.favoriteStationIds.includes(station.id) ?? false;

  const handleFavoriteClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleFavorite.mutate({ stationId: station.id, isFavorite });
  };

  return (
    <Card className="hover:shadow-md transition-shadow relative">
      <Link to={`/stations/${station.id}`}>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>{station.name}</span>
            <Badge variant={station.status === "Verified" ? "default" : "secondary"}>
              {station.status}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            {station.avgRating !== null
              ? `★ ${station.avgRating.toFixed(1)}`
              : "No reviews yet"}
          </p>
        </CardContent>
      </Link>
      {isLoggedIn && (
        <Button
          size="sm"
          variant="ghost"
          className="absolute top-2 right-2"
          onClick={handleFavoriteClick}
        >
          {isFavorite ? "♥" : "♡"}
        </Button>
      )}
    </Card>
  );
}