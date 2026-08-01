import { Outlet, Link, NavLink, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { List, LogIn, LogOut, Map as MapIcon, PlusCircle, User, Zap } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { supabase } from "@/lib/supabase";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import LanguageSync from "@/components/LanguageSync";
import ThemeToggle from "@/components/ThemeToggle";

const NAV_ITEMS = [
  { to: "/", key: "nav.map", icon: MapIcon, end: true },
  { to: "/stations", key: "nav.list", icon: List, end: false },
  { to: "/submit", key: "nav.submit", icon: PlusCircle, end: false },
  { to: "/profile", key: "nav.profile", icon: User, end: false },
] as const;

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
      <header className="shrink-0 border-b bg-card px-3 py-2 shadow-xs sm:px-4">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 lg:gap-x-5">
          {/* Brand name: never translated */}
          <Link
            to="/"
            className="flex items-center gap-2 rounded-lg outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            <span className="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground shadow-sm">
              <Zap aria-hidden="true" className="size-4.5 fill-current" />
            </span>
            {/* Very narrow screens keep the logo only; the name stays available to screen readers */}
            <span className="sr-only font-semibold tracking-tight whitespace-nowrap min-[400px]:not-sr-only" lang="en" dir="ltr">
              EV Chargers Tunisia
            </span>
          </Link>

          <nav
            aria-label={t("nav.main")}
            className="order-last -mx-1 flex w-full gap-1 overflow-x-auto px-1 pb-0.5 text-sm lg:order-0 lg:mx-0 lg:w-auto lg:px-0 lg:pb-0"
          >
            {NAV_ITEMS.map(({ to, key, icon: Icon, end }) => (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) =>
                  cn(
                    "inline-flex h-8 shrink-0 items-center gap-1.5 rounded-lg px-2 font-medium sm:px-2.5 whitespace-nowrap transition-colors outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                    isActive
                      ? "bg-primary/12 text-foreground ring-1 ring-primary/40"
                      : "text-muted-foreground hover:bg-muted hover:text-foreground",
                  )
                }
              >
                <Icon aria-hidden="true" className="hidden size-4 sm:block" />
                {t(key)}
              </NavLink>
            ))}
          </nav>

          <div className="ms-auto flex items-center gap-1.5">
            <LanguageSwitcher />
            <ThemeToggle />
            {isLoggedIn ? (
              <>
                <span className="hidden max-w-48 truncate text-sm text-muted-foreground xl:inline" dir="ltr">{session?.user.email}</span>
                <Button size="sm" variant="outline" onClick={handleLogout} className="h-8">
                  <LogOut aria-hidden="true" className="rtl:-scale-x-100" />
                  <span className="sr-only sm:not-sr-only">{t("auth.logOut")}</span>
                </Button>
              </>
            ) : (
              <Link to="/login" className={buttonVariants({ size: "sm", className: "h-8" })}>
                <LogIn aria-hidden="true" className="rtl:-scale-x-100" />
                <span className="sr-only sm:not-sr-only">{t("auth.logIn")}</span>
              </Link>
            )}
          </div>
        </div>
      </header>
      <main className="flex-1 min-h-0 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}
