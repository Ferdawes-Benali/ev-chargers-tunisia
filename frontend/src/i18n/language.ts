import { api } from "@/lib/api";
import { supabase } from "@/lib/supabase";
import i18n, { storeLanguage, type Language } from "@/i18n";

/** Tell the backend, so emails follow the choice. Fire-and-forget: a failure only warns. */
export function syncLanguageToBackend(lang: Language) {
  void (async () => {
    try {
      const { data } = await supabase.auth.getSession();
      if (!data.session) return; // logged out: nothing to sync
      await api.put("/api/v1/me/language", { language: lang });
    } catch (error) {
      console.warn("Couldn't save the language preference", error);
    }
  })();
}

/**
 * Switch the whole site to a language: <html lang/dir> follow (see i18n/index.ts),
 * the choice is saved on this browser, and sent to the backend when logged in.
 */
export async function changeLanguage(lang: Language, { syncBackend = true } = {}) {
  storeLanguage(lang);
  await i18n.changeLanguage(lang);
  if (syncBackend) syncLanguageToBackend(lang);
}
