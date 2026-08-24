(() => {
    const storageKey = "marine-insight-theme";
    const lightThemeColor = "#F2F7F9";
    const darkThemeColor = "#0A131F";

    const readSavedTheme = () => {
        try {
            const value = window.localStorage.getItem(storageKey);
            return value === "light" || value === "dark" ? value : null;
        } catch {
            return null;
        }
    };

    const updateToggle = (theme) => {
        const nextThemeLabel = theme === "dark" ? "日间" : "夜间";

        document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
            button.setAttribute("aria-label", `切换至${nextThemeLabel}模式`);
            button.setAttribute("title", `切换至${nextThemeLabel}模式`);
            button.setAttribute("aria-pressed", String(theme === "dark"));

            const label = button.querySelector(".theme-toggle-label");
            if (label) {
                label.textContent = nextThemeLabel;
            }
        });
    };

    const applyTheme = (theme) => {
        document.documentElement.dataset.theme = theme;
        document.documentElement.style.colorScheme = theme;

        const themeColor = document.querySelector('meta[name="theme-color"]');
        if (themeColor) {
            themeColor.setAttribute("content", theme === "dark" ? darkThemeColor : lightThemeColor);
        }

        updateToggle(theme);
    };

    const initialTheme = readSavedTheme() ?? "light";
    applyTheme(initialTheme);

    document.addEventListener("click", (event) => {
        const toggle = event.target.closest("[data-theme-toggle]");
        if (!toggle) {
            return;
        }

        const nextTheme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
        try {
            window.localStorage.setItem(storageKey, nextTheme);
        } catch {
            // Storage can be unavailable in private or hardened browser contexts; the current page still switches.
        }

        applyTheme(nextTheme);
    });

    document.addEventListener("DOMContentLoaded", () => updateToggle(document.documentElement.dataset.theme));
    document.addEventListener("enhancedload", () => updateToggle(document.documentElement.dataset.theme));

    window.setUrlHash = (hash) => {
        if (history && history.replaceState) {
            history.replaceState(null, "", "#" + hash);
        }
    };
})();
