import { Outlet, Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/hooks/useAuth";
import { supabase } from "@/lib/supabase";
import { Button } from "@/components/ui/button";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import LanguageSync from "@/components/LanguageSync";

export default function Layout() {
  const { t } = useTranslation();
  const { session, isLoggedIn } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await supabase.auth.signOut();
    navigate("/");
  };

  return (
    <div className="h-dvh flex flex-col">
      <LanguageSync />
      <header className="shrink-0 border-b px-4 py-3 flex flex-wrap gap-x-4 gap-y-2 items-center justify-between">
        <div className="flex flex-wrap gap-x-4 gap-y-1 items-center">
          {/* Brand name: never translated */}
          <span className="font-bold" lang="en" dir="ltr">EV Chargers Tunisia</span>
          <nav aria-label={t("nav.main")} className="flex gap-3 text-sm">
            <Link to="/">{t("nav.map")}</Link>
            <Link to="/stations">{t("nav.list")}</Link>
            <Link to="/submit">{t("nav.submit")}</Link>
            <Link to="/profile">{t("nav.profile")}</Link>
          </nav>
        </div>
        <div className="flex items-center gap-2">
          <LanguageSwitcher />
          {isLoggedIn ? (
            <>
              <span className="hidden text-sm text-muted-foreground md:inline" dir="ltr">{session?.user.email}</span>
              <Button size="sm" variant="outline" onClick={handleLogout}>
                {t("auth.logOut")}
              </Button>
            </>
          ) : (
            <Link to="/login">
              <Button size="sm">{t("auth.logIn")}</Button>
            </Link>
          )}
        </div>
      </header>
      <main className="flex-1 min-h-0 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}
