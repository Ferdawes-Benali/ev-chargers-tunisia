import { useSyncExternalStore } from "react";

export type Theme = "light" | "dark";

// Same key as the inline script in index.html, which applies the theme before the first paint
const STORAGE_KEY = "theme";
const DARK_QUERY = "(prefers-color-scheme: dark)";

/** The theme the user explicitly chose on this browser, if any. */
function readStoredTheme(): Theme | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === "light" || value === "dark" ? value : null;
  } catch {
    return null; // storage blocked (private mode, strict settings)
  }
}

function applyTheme(theme: Theme) {
  document.documentElement.classList.toggle("dark", theme === "dark");
}

const getSnapshot = (): Theme => (document.documentElement.classList.contains("dark") ? "dark" : "light");

function subscribe(onChange: () => void) {
  // The "dark" class on <html> is the single source of truth
  const observer = new MutationObserver(onChange);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });

  // Without a saved choice, follow the system setting as it changes
  const media = window.matchMedia(DARK_QUERY);
  const onSystemChange = () => {
    if (!readStoredTheme()) applyTheme(media.matches ? "dark" : "light");
  };
  media.addEventListener("change", onSystemChange);

  return () => {
    observer.disconnect();
    media.removeEventListener("change", onSystemChange);
  };
}

/** Current theme, and a toggle that saves the choice on this browser. */
export function useTheme() {
  const theme = useSyncExternalStore(subscribe, getSnapshot);

  const setTheme = (next: Theme) => {
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Not fatal: the choice just won't survive a reload
    }
    applyTheme(next);
  };

  return { theme, setTheme, toggleTheme: () => setTheme(theme === "dark" ? "light" : "dark") };
}
