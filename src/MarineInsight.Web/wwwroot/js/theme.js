(() => {
    const lightThemeColor = "#F2F7F9";
    const darkThemeColor = "#0A131F";

    // 优先使用浏览器/系统偏好识别主题；无法识别时按客户端时间降级（6:00-18:00 日间，其它夜间）。
    const detectPreferredTheme = () => {
        try {
            if (window.matchMedia) {
                if (window.matchMedia("(prefers-color-scheme: dark)").matches) {
                    return "dark";
                }
                if (window.matchMedia("(prefers-color-scheme: light)").matches) {
                    return "light";
                }
            }
        } catch {
            // matchMedia 抛错时忽略，落到时间降级
        }
        const hour = new Date().getHours();
        return (hour >= 6 && hour < 18) ? "light" : "dark";
    };

    const applyTheme = (theme) => {
        document.documentElement.dataset.theme = theme;
        document.documentElement.style.colorScheme = theme;

        const themeColor = document.querySelector('meta[name="theme-color"]');
        if (themeColor) {
            themeColor.setAttribute("content", theme === "dark" ? darkThemeColor : lightThemeColor);
        }
    };

    applyTheme(detectPreferredTheme());

    // 系统偏好切换（macOS 夜览、Windows 深色模式等）时自动跟随，无需用户手动切换。
    try {
        const darkQuery = window.matchMedia("(prefers-color-scheme: dark)");
        const handleChange = () => applyTheme(detectPreferredTheme());
        if (darkQuery.addEventListener) {
            darkQuery.addEventListener("change", handleChange);
        } else if (darkQuery.addListener) {
            darkQuery.addListener(handleChange);
        }
    } catch {
        // 无 matchMedia 支持的老浏览器忽略
    }

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
