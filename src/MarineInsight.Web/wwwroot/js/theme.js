(() => {
    const storageKey = "marine-insight-theme";
    const manualStorageKey = "marine-insight-theme-manual";
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

    const persistTheme = (theme) => {
        try {
            window.localStorage.setItem(storageKey, theme);
        } catch {
            // localStorage 在无痕/受限浏览器可能不可用；当前页面仍然应用识别结果。
        }
    };

    const markManualTheme = () => {
        try {
            window.localStorage.setItem(manualStorageKey, "1");
        } catch {
            // 手动选择标记不可写时仍保留当前页面的主题结果。
        }
    };

    const hasManualTheme = () => {
        try {
            return window.localStorage.getItem(manualStorageKey) === "1";
        } catch {
            return false;
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

    // 主题解析顺序：localStorage 缓存 → 系统偏好（暗色）→ 默认 light。首屏同步执行，避免闪烁。
    // 连系统偏好识别结果也写入缓存，完整页面导航时不会重新落回日间默认值。
    const initialTheme = readSavedTheme() ?? (darkQuery && darkQuery.matches ? "dark" : "light");
    persistTheme(initialTheme);
    applyTheme(initialTheme);

    // 全局委托：Header 里的 [data-theme-toggle] 按钮点击后翻转主题并持久化。
    document.addEventListener("click", (event) => {
        const toggle = event.target.closest("[data-theme-toggle]");
        if (!toggle) {
            return;
        }

        const nextTheme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
        persistTheme(nextTheme);
        markManualTheme();
        applyTheme(nextTheme);
    });

    // 系统偏好切换（macOS 夜览、Windows 深色模式等）时自动跟随，仅在用户没有显式选择时生效。
    if (darkQuery) {
        const handleChange = (event) => {
            if (hasManualTheme()) {
                return;
            }
            const nextTheme = event.matches ? "dark" : "light";
            persistTheme(nextTheme);
            applyTheme(nextTheme);
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
    const resyncTheme = () => {
        const savedTheme = readSavedTheme();
        if (savedTheme) {
            applyTheme(savedTheme);
        } else {
            updateToggle(document.documentElement.dataset.theme);
        }
    };

    document.addEventListener("DOMContentLoaded", resyncTheme);
    document.addEventListener("enhancedload", resyncTheme);
    window.addEventListener("pageshow", resyncTheme);

    // 增强导航替换页面片段时可能短暂移除根节点属性；观察该变化可避免页面回落到无主题状态。
    if (window.MutationObserver && document.documentElement) {
        const themeObserver = new MutationObserver(() => {
            const savedTheme = readSavedTheme();
            if (savedTheme && document.documentElement.dataset.theme !== savedTheme) {
                applyTheme(savedTheme);
            }
        });
        themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
    }

    // Blazor 查询完成后调用：只滚动到足以看见结果的位置，避免把上方查询功能完全推出视口。
    window.scrollToAnchor = (hash) => {
        if (!hash) {
            return;
        }
        const target = document.getElementById(hash);
        if (target && typeof target.scrollIntoView === "function") {
            target.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }
        if (window.history && typeof window.history.replaceState === "function") {
            window.history.replaceState(null, "", "#" + hash);
        }
    };
})();
