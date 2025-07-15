// Check if SignalR is loaded before initializing
if (typeof signalR !== 'undefined') {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .build();

    connection.on("ReceiveNotification", (message) => {
        alert(message); // Example notification
        console.log("Notification received: " + message);
    });

    connection.start()
        .then(() => console.log("SignalR connected"))
        .catch(err => console.error("SignalR connection error: ", err));
} else {
    console.error("SignalR library is not loaded. Please check the script inclusion in _Layout.cshtml.");
}



//if (typeof signalR === 'undefined') {
//    console.error('SignalR library is not loaded. Please check the script inclusion in _Layout.cshtml.');
//} else {
//    const connection = new signalR.HubConnectionBuilder()
//        .withUrl("/notificationHub")
//        .withAutomaticReconnect()
//        .build();

//    connection.on("ReceiveNotification", function (message) {
//        const notifications = document.getElementById("notifications");
//        const notificationMessage = document.getElementById("notificationMessage");
//        notificationMessage.textContent = message;
//        notifications.style.display = "block";
//        setTimeout(() => {
//            notifications.classList.remove("show");
//            setTimeout(() => notifications.style.display = "none", 500);
//        }, 5000);
//    });

//    connection.start().catch(function (err) {
//        console.error(err.toString());
//    });

//    // Bootstrap tooltips
//    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
//    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
//}