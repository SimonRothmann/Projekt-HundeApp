import { defineConfig, globalIgnores } from "eslint/config";
import { fixupConfigRules } from "@eslint/compat";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  // fixupConfigRules: eslint-config-next bringt eslint-plugin-react 7.37 mit,
  // das noch Kontext-Methoden aufruft, die ESLint 10 entfernt hat
  // (context.getFilename u.a.) - ohne den Wrapper bricht der Lint-Lauf mit
  // "contextOrFilename.getFilename is not a function" ab. @eslint/compat ist
  // die offizielle Brücke des ESLint-Teams dafür und ergänzt die Methoden.
  // Geprüft: identisches Ergebnis wie unter ESLint 9 (gleiche Regeln, gleiche
  // Befunde). Entfernen, sobald eslint-plugin-react ESLint 10 selbst
  // unterstützt (siehe TODO.md, Abhängigkeiten).
  ...fixupConfigRules([...nextVitals, ...nextTs]),
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
]);

export default eslintConfig;
