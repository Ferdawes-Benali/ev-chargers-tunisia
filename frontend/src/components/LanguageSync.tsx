import { useEffect, useRef } from "react";
import { useAuth } from "@/hooks/useAuth";
import { useProfile } from "@/hooks/useProfile";
import { currentLanguage, isLanguage, readStoredLanguage } from "@/i18n";
import { changeLanguage, syncLanguageToBackend } from "@/i18n/language";

/**
 * Once per login: with no choice saved on this browser, adopt the account's language;
 * with a saved choice that differs from the account's, send it to the backend so emails follow it.
 */
export default function LanguageSync() {
  const { isLoggedIn } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const syncedFor = useRef<string | null>(null);

  useEffect(() => {
    if (!profile || syncedFor.current === profile.id) return;
    syncedFor.current = profile.id;

    const stored = readStoredLanguage();
    if (!stored) {
      if (isLanguage(profile.preferredLanguage) && profile.preferredLanguage !== currentLanguage())
        void changeLanguage(profile.preferredLanguage, { syncBackend: false }); // it came from the backend
    } else if (stored !== profile.preferredLanguage) {
      syncLanguageToBackend(stored);
    }
  }, [profile]);

  return null;
}
