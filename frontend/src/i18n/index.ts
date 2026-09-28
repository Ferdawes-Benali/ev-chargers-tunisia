import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import fr from "@/locales/fr.json";
import ar from "@/locales/ar.json";
import en from "@/locales/en.json";

export const LANGUAGES = ["fr", "ar", "en"] as const;
export type Language = (typeof LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = "fr";

/** Native names, shown the same whatever the current language. */
export const LANGUAGE_NAMES: Record<Language, { long: string; short: string }> = {
  fr: { long: "Français", short: "FR" },
  ar: { long: "العربية", short: "ع" },
  en: { long: "English", short: "EN" },
};

const STORAGE_KEY = "lang";

export const isLanguage = (value: unknown): value is Language => LANGUAGES.includes(value as Language);

/** The language the user explicitly chose on this browser, if any. */
export function readStoredLanguage(): Language | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return isLanguage(value) ? value : null;
  } catch {
    return null; // storage blocked (private mode, strict settings)
  }
}

export function storeLanguage(lang: Language) {
  try {
    localStorage.setItem(STORAGE_KEY, lang);
  } catch {
    // Not fatal: the choice just won't survive a reload
  }
}

/** First fr/ar/en entry of the browser's preferred languages ("ar-TN" → "ar"). */
function navigatorLanguage(): Language | null {
  const list = navigator.languages?.length ? navigator.languages : [navigator.language];
  for (const tag of list) {
    const base = tag?.slice(0, 2).toLowerCase();
    if (isLanguage(base)) return base;
  }
  return null;
}

/**
 * Startup order: saved choice → browser languages → French.
 * The logged-in user's preferred language is applied later (see LanguageSync), once the profile loads.
 */
function detectInitialLanguage(): Language {
  return readStoredLanguage() ?? navigatorLanguage() ?? DEFAULT_LANGUAGE;
}

export const textDirection = (lang: string) => (lang === "ar" ? "rtl" : "ltr");

function applyToDocument(lang: string) {
  document.documentElement.lang = lang;
  document.documentElement.dir = textDirection(lang);
}

void i18n.use(initReactI18next).init({
  resources: {
    fr: { translation: fr },
    ar: { translation: ar },
    en: { translation: en },
  },
  lng: detectInitialLanguage(),
  fallbackLng: DEFAULT_LANGUAGE,
  supportedLngs: [...LANGUAGES],
  interpolation: { escapeValue: false }, // React already escapes
});

applyToDocument(i18n.language);
i18n.on("languageChanged", applyToDocument);

/** The current language, always one of ours. */
export const currentLanguage = (): Language =>
  isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : DEFAULT_LANGUAGE;

export default i18n;
