import { Link } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import type { StationListItem } from "@/types/station";

export default function StationCard({ station }: { station: StationListItem }) {
  return (
    <Link to={`/stations/${station.id}`}>
      <Card className="hover:shadow-md transition-shadow">
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
      </Card>
    </Link>
  );
}