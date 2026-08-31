(() => {
    const storageKey = "marine-insight-theme";
    const lightThemeColor = "#F2F7F9";
    const darkThemeColor = "#0A131F";
    const darkQuery = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

    // 读取用户显式选择过的主题；没有合法记录时返回 null，交给系统偏好判定。
    const readSavedTheme = () => {
        try {
            const value = window.localStorage.getItem(storageKey);
            return value === "light" || value === "dark" ? value : null;
        } catch {
            return null;
        }
    };

    // 同步所有主题切换按钮的可访问文案、按压态与文字，避免 Blazor 增强导航后残留旧标签。
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

    // 主题解析顺序：localStorage 明确选择 → 系统偏好（暗色）→ 默认 light。首屏同步执行，避免闪烁。
    const initialTheme = readSavedTheme() ?? (darkQuery && darkQuery.matches ? "dark" : "light");
    applyTheme(initialTheme);

    // 全局委托：Header 里的 [data-theme-toggle] 按钮点击后翻转主题并持久化。
    document.addEventListener("click", (event) => {
        const toggle = event.target.closest("[data-theme-toggle]");
        if (!toggle) {
            return;
        }

        const nextTheme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
        try {
            window.localStorage.setItem(storageKey, nextTheme);
        } catch {
            // localStorage 在无痕/受限浏览器可能不可用；当前页仍然应用切换。
        }

        applyTheme(nextTheme);
    });

    // 系统偏好切换（macOS 夜览、Windows 深色模式等）时自动跟随，仅在用户没有显式选择时生效。
    if (darkQuery) {
        const handleChange = (event) => {
            if (readSavedTheme()) {
                return;
            }
            applyTheme(event.matches ? "dark" : "light");
        };

        try {
            if (darkQuery.addEventListener) {
                darkQuery.addEventListener("change", handleChange);
            } else if (darkQuery.addListener) {
                darkQuery.addListener(handleChange);
            }
        } catch {
            // 老浏览器 addListener 兜底也失败时忽略，主题仍可手动切换。
        }
    }

    // Blazor 增强导航或首帧渲染后重新同步 toggle 文案（此时按钮才挂载到 DOM）。
    document.addEventListener("DOMContentLoaded", () => updateToggle(document.documentElement.dataset.theme));
    document.addEventListener("enhancedload", () => updateToggle(document.documentElement.dataset.theme));

    // Blazor 查询完成后调用：把浏览器视口平滑滚动到锚点，并同步 URL hash 便于分享。
    window.scrollToAnchor = (hash) => {
        if (!hash) {
            return;
        }
        const target = document.getElementById(hash);
        if (target && typeof target.scrollIntoView === "function") {
            target.scrollIntoView({ behavior: "smooth", block: "start" });
        }
        if (window.history && typeof window.history.replaceState === "function") {
            window.history.replaceState(null, "", "#" + hash);
        }
    };
})();
