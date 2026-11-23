window.reportNotifications = {
    isEnabled: () => {
        try {
            return localStorage.getItem("reportNotifications.enabled") !== "false";
        } catch {
            return true;
        }
    },
    setEnabled: async (enabled) => {
        window.reportNotifications.enabled = enabled;
        try {
            localStorage.setItem("reportNotifications.enabled", String(enabled));
        } catch {
        }
        if (enabled) await window.reportNotifications.enable();
    },
    initialize: async () => {
        const enabled = window.reportNotifications.isEnabled();
        window.reportNotifications.enabled = enabled;
        if (enabled) await window.reportNotifications.enable();
        return enabled;
    },
    permission: () => "Notification" in window ? Notification.permission : "unsupported",
    enable: async () => {
        if ("Notification" in window && Notification.permission === "default") {
            await Notification.requestPermission();
        }
    },
    show: (title, body) => {
        if (window.reportNotifications.enabled === false || !window.reportNotifications.isEnabled() ||
            !("Notification" in window) || Notification.permission !== "granted") return;
        const notification = new Notification(title, { body });
        notification.onclick = () => {
            window.focus();
            window.location.assign(new URL("reports", document.baseURI).href);
            notification.close();
        };
    }
};
