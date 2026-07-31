import { useTranslation } from "react-i18next";
import { isLanguage, DEFAULT_LANGUAGE, type Language } from "@/i18n";

/**
 * Intl locales per language. "-u-nu-latn" keeps Western digits (0-9) everywhere,
 * including Arabic, as is usual in Tunisia.
 */
export const INTL_LOCALES: Record<Language, string> = {
  fr: "fr-TN-u-nu-latn",
  ar: "ar-TN-u-nu-latn",
  en: "en-u-nu-latn",
};

export const intlLocale = (lang: Language) => INTL_LOCALES[lang];

export function formatNumber(value: number, lang: Language, options?: Intl.NumberFormatOptions) {
  return new Intl.NumberFormat(intlLocale(lang), options).format(value);
}

/** Current language, its Intl locale and a number formatter bound to it. */
export function useLocale() {
  const { i18n } = useTranslation();
  const lang: Language = isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : DEFAULT_LANGUAGE;
  return {
    lang,
    locale: intlLocale(lang),
    isRtl: lang === "ar",
    number: (value: number, options?: Intl.NumberFormatOptions) => formatNumber(value, lang, options),
  };
}
