import { useTranslation } from "react-i18next";
import { Zap } from "lucide-react";
import { supabase } from "@/lib/supabase";
import { Button } from "@/components/ui/button";

export default function LoginPage() {
  const { t } = useTranslation();
  const signIn = async (provider: "google" | "github") => {
    await supabase.auth.signInWithOAuth({
      provider,
      options: { redirectTo: window.location.origin },
    });
  };

  return (
    <div className="relative grid min-h-full place-items-center overflow-hidden px-4 py-10">
      <div aria-hidden="true" className="absolute inset-0 bg-linear-to-br from-primary/15 via-transparent to-accent/10" />
      <div className="relative w-full max-w-sm space-y-6 rounded-2xl border bg-card p-6 text-center shadow-lg sm:p-8">
        <div className="flex flex-col items-center gap-3">
          <span className="grid size-12 place-items-center rounded-xl bg-primary text-primary-foreground shadow-md">
            <Zap aria-hidden="true" className="size-6 fill-current" />
          </span>
          {/* Brand name: never translated */}
          <p className="text-sm font-semibold tracking-tight text-muted-foreground" lang="en" dir="ltr">EV Chargers Tunisia</p>
        </div>
        <div className="space-y-1.5">
          <h1 className="text-2xl font-bold tracking-tight">{t("auth.title")}</h1>
          <p className="text-sm text-muted-foreground">{t("auth.tagline")}</p>
        </div>
        <div className="space-y-3">
          <Button className="h-11 w-full text-base" onClick={() => signIn("google")}>
            {t("auth.google")}
          </Button>
          <Button className="h-11 w-full text-base" variant="outline" onClick={() => signIn("github")}>
            {t("auth.github")}
          </Button>
        </div>
      </div>
    </div>
  );
}
