import { useTranslation } from "react-i18next";
import { useAuth } from "@/hooks/useAuth";
import { useFavorites } from "@/hooks/useProfile";
import { Navigate } from "react-router-dom";
import StationCard from "@/components/StationCard";
import StationCardSkeleton from "@/components/StationCardSkeleton";

export default function ProfilePage() {
  const { t } = useTranslation();
  const { isLoggedIn, loading: authLoading, session } = useAuth();
  const { data: favorites, isLoading, isError } = useFavorites(isLoggedIn);

  if (authLoading) return <div className="p-4">{t("common.loading")}</div>;
  if (!isLoggedIn) return <Navigate to="/login" replace />;

  return (
    <div className="p-4 space-y-4">
      <h1 className="text-xl font-bold">{t("profile.title")}</h1>
      <p dir="ltr" className="text-start">{session?.user.email}</p>

      <section aria-labelledby="favorites-heading">
        <h2 id="favorites-heading" className="font-semibold mb-2">{t("profile.favorites")}</h2>
        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true" aria-label={t("profile.loading")}>
            {Array.from({ length: 3 }).map((_, i) => (
              <StationCardSkeleton key={i} />
            ))}
          </div>
        ) : isError ? (
          <p className="text-sm text-muted-foreground">{t("profile.loadError")}</p>
        ) : !favorites || favorites.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("profile.noFavorites")}</p>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {favorites.map((station) => (
              <StationCard key={station.id} station={station} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
