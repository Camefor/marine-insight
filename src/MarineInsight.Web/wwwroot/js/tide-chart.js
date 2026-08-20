const chartStates = new Map();
let echartsLoader;

function loadECharts() {
    if (globalThis.echarts) {
        return Promise.resolve(globalThis.echarts);
    }

    echartsLoader ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = new URL("_content/Vizor.ECharts/js/vizor-echarts-bundle-min.js", document.baseURI).toString();
        script.async = true;
        script.onload = () => globalThis.echarts ? resolve(globalThis.echarts) : reject(new Error("ECharts 未正确初始化。"));
        script.onerror = () => reject(new Error("ECharts 资源加载失败。"));
        document.head.appendChild(script);
    }).catch(error => {
        echartsLoader = undefined;
        throw error;
    });

    return echartsLoader;
}

export async function render(elementId, model) {
    dispose(elementId);

    const element = document.getElementById(elementId);
    if (!element || !model?.points?.length) {
        return false;
    }

    const echarts = await loadECharts();
    const chart = echarts.init(element, null, { renderer: "canvas" });
    const extrema = model.points
        .filter(point => point.type === "high" || point.type === "low")
        .map(point => ({
            coord: [point.label, point.height],
            name: point.type === "high" ? "高潮" : "低潮",
            value: point.height,
            itemStyle: { color: point.type === "high" ? "#ff806d" : "#48e0c0" }
        }));

    chart.setOption({
        animationDuration: 420,
        aria: {
            enabled: true,
            description: model.accessibleDescription
        },
        backgroundColor: "transparent",
        grid: {
            top: 30,
            right: 22,
            bottom: 56,
            left: 48,
            containLabel: true
        },
        tooltip: {
            trigger: "axis",
            confine: true,
            backgroundColor: "rgba(4, 19, 33, 0.96)",
            borderColor: "rgba(151, 201, 222, 0.35)",
            textStyle: { color: "#edf8fb" },
            formatter(parameters) {
                const point = model.points[parameters[0]?.dataIndex ?? 0];
                if (!point) {
                    return "";
                }

                const tideType = point.type === "high" ? " · 高潮" : point.type === "low" ? " · 低潮" : "";
                return `${point.fullLabel}<br/><strong>${point.height.toFixed(2)} m</strong> · ${point.trendText}${tideType}`;
            }
        },
        xAxis: {
            type: "category",
            boundaryGap: false,
            data: model.points.map(point => point.label),
            axisLine: { lineStyle: { color: "rgba(151, 201, 222, 0.24)" } },
            axisTick: { show: false },
            axisLabel: {
                color: "#91b2c4",
                hideOverlap: true,
                margin: 14,
                fontSize: 11
            }
        },
        yAxis: {
            type: "value",
            name: "潮位 m",
            nameTextStyle: { color: "#91b2c4", padding: [0, 0, 6, 0] },
            scale: true,
            splitNumber: 4,
            axisLabel: {
                color: "#91b2c4",
                formatter: value => Number(value).toFixed(1)
            },
            splitLine: { lineStyle: { color: "rgba(151, 201, 222, 0.1)" } }
        },
        dataZoom: model.points.length > 48 ? [
            {
                type: "inside",
                start: 0,
                end: Math.min(100, 48 / model.points.length * 100),
                zoomOnMouseWheel: false,
                moveOnMouseWheel: true,
                moveOnMouseMove: true
            },
            {
                type: "slider",
                height: 16,
                bottom: 8,
                borderColor: "rgba(151, 201, 222, 0.18)",
                backgroundColor: "rgba(3, 14, 25, 0.42)",
                fillerColor: "rgba(72, 224, 192, 0.18)",
                handleStyle: { color: "#48e0c0", borderColor: "#0d2943" },
                textStyle: { color: "#91b2c4" },
                showDetail: false
            }
        ] : [],
        series: [
            {
                name: "潮位",
                type: "line",
                smooth: 0.35,
                showSymbol: model.points.length <= 32,
                symbolSize: 6,
                data: model.points.map(point => point.height),
                lineStyle: { color: "#48e0c0", width: 3 },
                itemStyle: { color: "#7af0d8", borderColor: "#07324a", borderWidth: 2 },
                areaStyle: {
                    color: {
                        type: "linear",
                        x: 0,
                        y: 0,
                        x2: 0,
                        y2: 1,
                        colorStops: [
                            { offset: 0, color: "rgba(72, 224, 192, 0.34)" },
                            { offset: 1, color: "rgba(72, 224, 192, 0.02)" }
                        ]
                    }
                },
                markPoint: {
                    symbol: "pin",
                    symbolSize: 44,
                    label: {
                        color: "#061423",
                        fontSize: 10,
                        fontWeight: 700,
                        formatter: parameter => parameter.name
                    },
                    data: extrema
                },
                emphasis: { focus: "series" }
            }
        ]
    });

    const observer = new ResizeObserver(() => chart.resize());
    observer.observe(element);
    chartStates.set(elementId, { chart, observer });
    return true;
}

export function dispose(elementId) {
    const state = chartStates.get(elementId);
    if (!state) {
        return;
    }

    state.observer.disconnect();
    state.chart.dispose();
    chartStates.delete(elementId);
}
