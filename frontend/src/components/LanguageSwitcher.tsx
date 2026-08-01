import { useTranslation } from "react-i18next";
import { cn } from "@/lib/utils";
import { LANGUAGES, LANGUAGE_NAMES, currentLanguage } from "@/i18n";
import { changeLanguage } from "@/i18n/language";

/**
 * Segmented toggle with each language in its own script ("Français", "العربية", "English"),
 * shortened to "FR", "ع", "EN" on small screens.
 */
export default function LanguageSwitcher({ className }: { className?: string }) {
  const { t } = useTranslation();
  const current = currentLanguage();

  return (
    <div role="group" aria-label={t("nav.language")} className={cn("inline-flex rounded-lg bg-muted p-0.5 ring-1 ring-border", className)}>
      {LANGUAGES.map((lang) => {
        const isCurrent = lang === current;
        const { long, short } = LANGUAGE_NAMES[lang];
        return (
          <button
            key={lang}
            type="button"
            lang={lang}
            aria-label={long}
            aria-pressed={isCurrent}
            onClick={() => { if (!isCurrent) void changeLanguage(lang); }}
            className={cn(
              "h-7 min-w-8 rounded-md px-2 text-xs font-medium transition-colors outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
              isCurrent ? "bg-card text-foreground shadow-sm ring-1 ring-border" : "text-muted-foreground hover:bg-card/70 hover:text-foreground",
            )}
          >
            <span aria-hidden="true" className="sm:hidden">{short}</span>
            <span aria-hidden="true" className="hidden sm:inline">{long}</span>
          </button>
        );
      })}
    </div>
  );
}
