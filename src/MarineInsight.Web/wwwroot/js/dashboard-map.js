const maps = new Map();

export function init(elementId, dotNetReference, options) {
    const element = document.getElementById(elementId);
    if (!element) {
        return false;
    }

    if (!window.L) {
        notifyUnavailable(dotNetReference, "地图脚本加载失败，请直接输入经纬度继续查询。");
        return false;
    }

    const latitude = toFiniteNumber(options?.latitude, 30.194);
    const longitude = toFiniteNumber(options?.longitude, 122.687);
    const zoom = toFiniteNumber(options?.zoom, 9);

    if (maps.has(elementId)) {
        updateSelection(elementId, latitude, longitude, zoom);
        return true;
    }

    const map = window.L.map(element, {
        zoomControl: true
    }).setView([latitude, longitude], zoom);

    // Tianditu WMTS tiles use CGCS2000, which is aligned with WGS-84 for point picking; base + label layers.
    const tk = options?.tk ?? "";
    const baseLayer = window.L.tileLayer(
        `https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk=${tk}`,
        {
            subdomains: ["0", "1", "2", "3", "4", "5", "6", "7"],
            maxZoom: 18,
            attribution: '&copy; <a href="https://www.tianditu.gov.cn" target="_blank" rel="noopener noreferrer">天地图</a>'
        });
    const labelLayer = window.L.tileLayer(
        `https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk=${tk}`,
        {
            subdomains: ["0", "1", "2", "3", "4", "5", "6", "7"],
            maxZoom: 18
        });

    baseLayer.on("tileerror", () => notifyUnavailable(
        dotNetReference,
        "地图瓦片加载失败，请直接输入经纬度继续查询。"));
    baseLayer.addTo(map);
    labelLayer.addTo(map);

    const marker = window.L.marker([latitude, longitude]).addTo(map);
    map.on("click", event => {
        const selected = normalizeLatLng(event.latlng.lat, event.latlng.lng);
        marker.setLatLng([selected.latitude, selected.longitude]);
        dotNetReference
            .invokeMethodAsync("SelectMapPointFromJs", selected.latitude, selected.longitude)
            .catch(() => undefined);
    });

    addLocateControl(map, marker, dotNetReference);

    maps.set(elementId, { map, marker });
    window.setTimeout(() => map.invalidateSize(), 0);
    return true;
}

export function updateSelection(elementId, latitude, longitude, zoom) {
    const instance = maps.get(elementId);
    if (!instance) {
        return;
    }

    const selected = normalizeLatLng(latitude, longitude);
    instance.marker.setLatLng([selected.latitude, selected.longitude]);
    instance.map.setView([selected.latitude, selected.longitude], zoom ?? instance.map.getZoom());
    window.setTimeout(() => instance.map.invalidateSize(), 0);
}

export function dispose(elementId) {
    const instance = maps.get(elementId);
    if (!instance) {
        return;
    }

    instance.map.remove();
    maps.delete(elementId);
}

function notifyUnavailable(dotNetReference, message) {
    dotNetReference
        ?.invokeMethodAsync("HandleMapUnavailableFromJs", message)
        .catch(() => undefined);
}

// 定位控件叠加在缩放控件下方（topleft）。仅在用户主动点击时才请求定位权限，避免页面加载即弹权限提示。
function addLocateControl(map, marker, dotNetReference) {
    const LocateControl = window.L.Control.extend({
        options: { position: "topleft" },
        onAdd() {
            const container = window.L.DomUtil.create("div", "leaflet-control-locate");
            container.title = "定位到当前位置";
            container.setAttribute("role", "button");
            container.setAttribute("tabindex", "0");
            container.setAttribute("aria-label", "定位到当前位置");
            container.innerHTML = locateIcon();

            window.L.DomEvent.disableClickPropagation(container);
            window.L.DomEvent.disableScrollPropagation(container);
            window.L.DomEvent.on(container, "click", () => requestLocation(map, marker, dotNetReference, container));
            window.L.DomEvent.on(container, "keydown", event => {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    requestLocation(map, marker, dotNetReference, container);
                }
            });
            return container;
        }
    });
    map.addControl(new LocateControl());
}

function requestLocation(map, marker, dotNetReference, container) {
    if (!("geolocation" in navigator)) {
        notifyLocateUnavailable(dotNetReference, "当前浏览器不支持定位，请直接输入经纬度。");
        return;
    }

    container.classList.add("locating");
    navigator.geolocation.getCurrentPosition(
        position => {
            container.classList.remove("locating");
            const selected = normalizeLatLng(position.coords.latitude, position.coords.longitude);
            marker.setLatLng([selected.latitude, selected.longitude]);
            map.setView([selected.latitude, selected.longitude], 13);
            dotNetReference
                .invokeMethodAsync("SelectMapPointFromJs", selected.latitude, selected.longitude)
                .catch(() => undefined);
        },
        error => {
            container.classList.remove("locating");
            notifyLocateUnavailable(dotNetReference, locateErrorMessage(error));
        },
        { enableHighAccuracy: true, timeout: 10000, maximumAge: 30000 });
}

function locateErrorMessage(error) {
    switch (error.code) {
        case error.PERMISSION_DENIED:
            return "定位权限被拒绝，请通过地图或坐标输入选择位置。";
        case error.POSITION_UNAVAILABLE:
            return "暂时无法获取位置，请直接输入经纬度。";
        case error.TIMEOUT:
            return "定位超时，请重试或直接输入经纬度。";
        default:
            return "定位失败，请直接输入经纬度。";
    }
}

function notifyLocateUnavailable(dotNetReference, message) {
    dotNetReference
        ?.invokeMethodAsync("HandleLocateUnavailableFromJs", message)
        .catch(() => undefined);
}

function locateIcon() {
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="3"></circle><path d="M12 2v3M12 19v3M2 12h3M19 12h3"></path></svg>';
}

function normalizeLatLng(latitude, longitude) {
    return {
        latitude: roundCoordinate(clamp(toFiniteNumber(latitude, 0), -90, 90)),
        longitude: roundCoordinate(clamp(toFiniteNumber(longitude, 0), -180, 180))
    };
}

function roundCoordinate(value) {
    return Math.round(value * 1e6) / 1e6;
}

function toFiniteNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}
