import { useTranslation } from "react-i18next";
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
    <div className="p-4 max-w-sm mx-auto space-y-4">
      <h1 className="text-xl font-bold">{t("auth.title")}</h1>
      <Button className="w-full" onClick={() => signIn("google")}>
        {t("auth.google")}
      </Button>
      <Button className="w-full" variant="outline" onClick={() => signIn("github")}>
        {t("auth.github")}
      </Button>
    </div>
  );
}
