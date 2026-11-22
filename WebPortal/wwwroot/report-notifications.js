window.reportNotifications = {
    enable: async () => {
        if ("Notification" in window && Notification.permission === "default") {
            await Notification.requestPermission();
        }
    },
    show: (title, body) => {
        if (!("Notification" in window) || Notification.permission !== "granted") return;
        const notification = new Notification(title, { body });
        notification.onclick = () => {
            window.focus();
            window.location.assign(new URL("reports", document.baseURI).href);
            notification.close();
        };
    }
};
