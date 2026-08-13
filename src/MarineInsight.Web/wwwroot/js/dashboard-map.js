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

    // OpenStreetMap public tiles are loaded only for the visible map; no prefetch or offline cache is used here.
    const tileLayer = window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    });

    tileLayer.on("tileerror", () => notifyUnavailable(
        dotNetReference,
        "地图瓦片加载失败，请直接输入经纬度继续查询。"));
    tileLayer.addTo(map);

    const marker = window.L.marker([latitude, longitude]).addTo(map);
    map.on("click", event => {
        const selected = normalizeLatLng(event.latlng.lat, event.latlng.lng);
        marker.setLatLng([selected.latitude, selected.longitude]);
        dotNetReference
            .invokeMethodAsync("SelectMapPointFromJs", selected.latitude, selected.longitude)
            .catch(() => undefined);
    });

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
