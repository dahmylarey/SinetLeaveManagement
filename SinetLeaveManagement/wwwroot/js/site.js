if (typeof signalR === 'undefined') {
    console.error('SignalR library is not loaded. Please check the script inclusion in _Layout.cshtml.');
} else {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", function (message) {
        const notifications = document.getElementById("notifications");
        const notificationMessage = document.getElementById("notificationMessage");
        notificationMessage.textContent = message;
        notifications.style.display = "block";
        setTimeout(() => {
            notifications.classList.remove("show");
            setTimeout(() => notifications.style.display = "none", 500);
        }, 5000);
    });

    connection.start().catch(function (err) {
        console.error(err.toString());
    });

    // Bootstrap tooltips
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
}