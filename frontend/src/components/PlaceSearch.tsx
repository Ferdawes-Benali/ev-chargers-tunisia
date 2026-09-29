import { useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import { LoaderCircle, MapPin, Search, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { usePlaceSearch } from "@/hooks/useReach";
import type { PlaceSuggestion } from "@/types/reach";

interface PlaceSearchProps {
  value: PlaceSuggestion | null;
  onSelect: (place: PlaceSuggestion) => void;
  onClear: () => void;
  /** Suggestions near this point rank higher */
  focus: { lat: number; lng: number } | null;
  label: string;
  /** Defaults to the translated "Search a city or place". */
  placeholder?: string;
}

function useDebouncedValue<T>(value: T, delayMs: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}

/** Place search with suggestions, following the ARIA combobox pattern. */
export default function PlaceSearch({ value, onSelect, onClear, focus, label, placeholder }: PlaceSearchProps) {
  const { t } = useTranslation();
  const id = useId();
  const listId = `${id}-list`;
  const inputRef = useRef<HTMLInputElement>(null);
  const [text, setText] = useState(value?.label ?? "");
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [rect, setRect] = useState<{ left: number; top: number; width: number } | null>(null);

  // Show the chosen place (or nothing) when the value changes from outside
  useEffect(() => { setText(value?.label ?? ""); }, [value]);

  const query = useDebouncedValue(text, 300);
  const searching = open && query.trim().length >= 2 && query !== value?.label;
  const { data, isFetching, isError } = usePlaceSearch(searching ? query : "", focus);
  const suggestions = searching ? data ?? [] : [];

  useEffect(() => { setActiveIndex(-1); }, [data]);

  // The popup lives in <body> (above the map and outside the panel's scroll area), so follow the input
  useLayoutEffect(() => {
    if (!searching) return;
    const update = () => {
      const r = inputRef.current?.getBoundingClientRect();
      if (r) setRect({ left: r.left, top: r.bottom + 4, width: r.width });
    };
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [searching]);

  const select = (place: PlaceSuggestion) => {
    setText(place.label);
    setOpen(false);
    onSelect(place);
  };

  const clear = () => {
    setText("");
    setOpen(false);
    onClear();
    inputRef.current?.focus();
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setOpen(true);
      if (suggestions.length) setActiveIndex((i) => (i + 1) % suggestions.length);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      if (suggestions.length) setActiveIndex((i) => (i <= 0 ? suggestions.length - 1 : i - 1));
    } else if (e.key === "Enter") {
      if (open && activeIndex >= 0 && suggestions[activeIndex]) {
        e.preventDefault();
        select(suggestions[activeIndex]);
      }
    } else if (e.key === "Escape") {
      if (open) {
        e.preventDefault();
        setOpen(false);
      } else if (text) {
        clear();
      }
    }
  };

  const expanded = searching && rect !== null;
  const waiting = isFetching && suggestions.length === 0;
  const status = isError
    ? t("reach.search.error")
    : waiting
      ? t("reach.search.searching")
      : suggestions.length === 0 && !isFetching
        ? t("reach.search.none")
        : null;

  return (
    <div className="relative">
      <label htmlFor={`${id}-input`} className="sr-only">{label}</label>
      <Search className="pointer-events-none absolute inset-s-2.5 top-1/2 size-4 -translate-y-1/2 text-slate-400" aria-hidden />
      <input
        ref={inputRef}
        id={`${id}-input`}
        type="text"
        role="combobox"
        aria-expanded={expanded}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={expanded && activeIndex >= 0 ? `${id}-opt-${activeIndex}` : undefined}
        autoComplete="off"
        spellCheck={false}
        placeholder={placeholder ?? t("reach.search.placeholder")}
        value={text}
        onChange={(e) => { setText(e.target.value); setOpen(true); }}
        onFocus={() => { if (text && text !== value?.label) setOpen(true); }}
        onBlur={() => setOpen(false)}
        onKeyDown={onKeyDown}
        className="h-9 w-full rounded-lg border border-input bg-white ps-8 pe-8 text-sm outline-none placeholder:text-slate-400 focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
      />
      {isFetching && searching && suggestions.length > 0 ? (
        <LoaderCircle className="absolute inset-e-2.5 top-1/2 size-4 -translate-y-1/2 animate-spin text-slate-400" aria-hidden />
      ) : text ? (
        <button
          type="button"
          onClick={clear}
          aria-label={t("reach.search.clear")}
          className="absolute inset-e-1.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-500 hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-slate-900"
        >
          <X className="size-4" />
        </button>
      ) : null}

      {/* Announces results to screen readers without moving focus */}
      <p className="sr-only" aria-live="polite">
        {expanded && !waiting && !isError ? (suggestions.length ? t("reach.search.found", { count: suggestions.length }) : status) : ""}
      </p>

      {createPortal(
        <div
          hidden={!expanded}
          style={rect ? { left: rect.left, top: rect.top, width: rect.width } : undefined}
          dir={document.documentElement.dir || "ltr"}
          className="fixed z-[1100] overflow-hidden rounded-lg bg-white text-sm shadow-lg ring-1 ring-slate-900/10"
          // Keep focus in the input while clicking a suggestion
          onMouseDown={(e) => e.preventDefault()}
        >
          <ul id={listId} role="listbox" aria-label={label} className="max-h-64 overflow-y-auto py-1">
            {suggestions.map((place, i) => (
              <li
                key={`${place.label}-${place.lat}-${place.lng}`}
                id={`${id}-opt-${i}`}
                role="option"
                aria-selected={i === activeIndex}
                onClick={() => select(place)}
                onMouseMove={() => setActiveIndex(i)}
                className={cn("flex cursor-pointer items-start gap-2 px-3 py-2", i === activeIndex && "bg-slate-100")}
              >
                <MapPin className="mt-0.5 size-4 shrink-0 text-slate-400" aria-hidden />
                <span>{place.label}</span>
              </li>
            ))}
          </ul>
          {status && <p className="px-3 py-2 text-slate-500">{status}</p>}
        </div>,
        document.body
      )}
    </div>
  );
}
