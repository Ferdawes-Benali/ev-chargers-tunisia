// Verifies that fr, ar and en translations have the same keys.
// Plural keys ("walk_one", "walk_other"…) are compared by their base name ("walk"),
// since Arabic needs more plural forms than French or English.
// Usage: node scripts/check-i18n.mjs  → exits 1 on any mismatch.
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const LOCALES_DIR = join(dirname(fileURLToPath(import.meta.url)), "..", "src", "locales");
const LANGUAGES = ["fr", "ar", "en"];
const PLURAL_SUFFIX = /_(zero|one|two|few|many|other)$/;
// Forms each language must provide for a plural key: the CLDR categories i18next asks for.
// French has "many" for millions (1 000 000 → "many"); i18next doesn't fall back to "_other".
const REQUIRED_FORMS = {
  fr: ["one", "many", "other"],
  en: ["one", "other"],
  ar: ["zero", "one", "two", "few", "many", "other"],
};

/** Flattens {"a": {"b": "x"}} into ["a.b"]. */
function flatten(object, prefix = "") {
  return Object.entries(object).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === "object") return flatten(value, path);
    if (typeof value !== "string" || value.trim() === "") {
      problems.push(`[${currentLanguage}] ${path}: empty or not a string`);
    }
    return [path];
  });
}

const problems = [];
let currentLanguage = "";
/** language → Map(baseKey → Set(plural forms)); a non-plural key has an empty set. */
const keysByLanguage = {};

for (const lang of LANGUAGES) {
  currentLanguage = lang;
  const file = join(LOCALES_DIR, `${lang}.json`);
  let json;
  try {
    json = JSON.parse(readFileSync(file, "utf8"));
  } catch (error) {
    console.error(`✗ Cannot read ${file}: ${error.message}`);
    process.exit(1);
  }
  const keys = new Map();
  for (const path of flatten(json)) {
    const match = path.match(PLURAL_SUFFIX);
    const base = match ? path.slice(0, -match[0].length) : path;
    if (!keys.has(base)) keys.set(base, new Set());
    if (match) keys.get(base).add(match[1]);
  }
  keysByLanguage[lang] = keys;
}

const allBases = new Set(LANGUAGES.flatMap((lang) => [...keysByLanguage[lang].keys()]));
const isPlural = (base) => LANGUAGES.some((lang) => (keysByLanguage[lang].get(base)?.size ?? 0) > 0);

for (const base of [...allBases].sort()) {
  for (const lang of LANGUAGES) {
    const forms = keysByLanguage[lang].get(base);
    if (!forms) {
      problems.push(`[${lang}] missing: ${base}`);
      continue;
    }
    if (!isPlural(base)) continue;
    if (forms.size === 0) {
      problems.push(`[${lang}] ${base}: plural in another language but not here (add ${REQUIRED_FORMS[lang].map((f) => `_${f}`).join(", ")})`);
      continue;
    }
    const missing = REQUIRED_FORMS[lang].filter((form) => !forms.has(form));
    if (missing.length) problems.push(`[${lang}] ${base}: missing plural forms ${missing.map((f) => `_${f}`).join(", ")}`);
    const extra = [...forms].filter((form) => !REQUIRED_FORMS[lang].includes(form));
    if (extra.length) problems.push(`[${lang}] ${base}: extra plural forms ${extra.map((f) => `_${f}`).join(", ")}`);
  }
}

const plurals = [...allBases].filter(isPlural).length;
if (problems.length) {
  console.error(`✗ i18n check failed (${problems.length} problem${problems.length === 1 ? "" : "s"}):`);
  for (const problem of problems) console.error(`  - ${problem}`);
  process.exit(1);
}
console.log(`✓ fr, ar and en match: ${allBases.size} keys (${plurals} with plural forms).`);
