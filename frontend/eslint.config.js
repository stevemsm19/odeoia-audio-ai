import js from "@eslint/js";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";

const sharedGlobals = {
  AbortController: "readonly",
  document: "readonly",
  fetch: "readonly",
  FormData: "readonly",
  localStorage: "readonly",
  setTimeout: "readonly",
  URL: "readonly",
  window: "readonly",
};

const sharedRules = {
  ...reactHooks.configs.recommended.rules,
  "react-refresh/only-export-components": "off",
};

export default [
  { ignores: ["dist", "node_modules", "vite.config.js"] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    // Legacy .jsx/.js files, migrated incrementally to .tsx/.ts (Hito 3, Fase 3).
    files: ["src/**/*.{js,jsx}"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      parserOptions: { ecmaFeatures: { jsx: true } },
      globals: sharedGlobals,
    },
    plugins: { "react-hooks": reactHooks, "react-refresh": reactRefresh },
    rules: sharedRules,
  },
  {
    files: ["src/**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      parserOptions: { ecmaFeatures: { jsx: true } },
      globals: sharedGlobals,
    },
    plugins: { "react-hooks": reactHooks, "react-refresh": reactRefresh },
    rules: sharedRules,
  },
];
