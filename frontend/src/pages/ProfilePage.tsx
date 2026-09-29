import { useTranslation } from "react-i18next";
import { Heart, Mail, TriangleAlert, UserRound } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { useFavorites } from "@/hooks/useProfile";
import { Link, Navigate } from "react-router-dom";
import StationCard from "@/components/StationCard";
import StationCardSkeleton from "@/components/StationCardSkeleton";
import SectionCard from "@/components/SectionCard";
import EmptyState from "@/components/EmptyState";
import { buttonVariants } from "@/components/ui/button";

export default function ProfilePage() {
  const { t } = useTranslation();
  const { isLoggedIn, loading: authLoading, session } = useAuth();
  const { data: favorites, isLoading, isError } = useFavorites(isLoggedIn);

  if (authLoading) return <div className="mx-auto max-w-6xl px-4 py-8 text-muted-foreground">{t("common.loading")}</div>;
  if (!isLoggedIn) return <Navigate to="/login" replace />;

  const email = session?.user.email;

  return (
    <div className="mx-auto max-w-6xl space-y-6 px-4 py-6 sm:py-8">
      <h1 className="text-2xl font-bold tracking-tight">{t("profile.title")}</h1>

      <SectionCard title={t("profile.account")} icon={UserRound}>
        <div className="flex items-center gap-4">
          {/* Initial of the email, decorative */}
          <span aria-hidden="true" className="grid size-12 shrink-0 place-items-center rounded-full bg-primary text-lg font-semibold text-primary-foreground uppercase">
            {email?.charAt(0)}
          </span>
          <dl className="min-w-0">
            <dt className="flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
              <Mail aria-hidden="true" className="size-3.5" />
              {t("profile.email")}
            </dt>
            <dd dir="ltr" className="truncate text-start font-medium">{email}</dd>
          </dl>
        </div>
      </SectionCard>

      <SectionCard title={t("profile.favorites")} titleId="favorites-heading" icon={Heart}>
        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true" aria-label={t("profile.loading")}>
            {Array.from({ length: 3 }).map((_, i) => (
              <StationCardSkeleton key={i} />
            ))}
          </div>
        ) : isError ? (
          <EmptyState icon={TriangleAlert} tone="danger" title={t("profile.loadError")} />
        ) : !favorites || favorites.length === 0 ? (
          <EmptyState
            icon={Heart}
            title={t("profile.noFavoritesTitle")}
            text={t("profile.noFavorites")}
            action={
              <Link to="/stations" className={buttonVariants({ variant: "outline" })}>
                {t("profile.browse")}
              </Link>
            }
          />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {favorites.map((station) => (
              <StationCard key={station.id} station={station} />
            ))}
          </div>
        )}
      </SectionCard>
    </div>
  );
}
