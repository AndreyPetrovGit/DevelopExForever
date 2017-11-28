

let hubUrl = '/Manager';
let httpConnection = new signalR.HttpConnection(hubUrl);
let hubConnection = new signalR.HubConnection(httpConnection);

document.getElementById("url").value = "https://en.wikipedia.org/wiki/New_York_City";
document.getElementById("maxThreadCount").value = 10;
document.getElementById("searchText").value = "Trump";
document.getElementById("scanUrlCount").value = 100;


document.getElementById("startBtn").addEventListener("click", function (e) {
    let url = document.getElementById("url").value;
    let maxThreadCount = document.getElementById("maxThreadCount").value;
    let searchText = document.getElementById("searchText").value;
    let scanUrlCount = document.getElementById("scanUrlCount").value;
    hubConnection.invoke("Start", url, maxThreadCount, searchText, scanUrlCount);

});

document.getElementById("pauseBtn").addEventListener("click", function (e) {
    hubConnection.invoke("Pause");
});

hubConnection.on("workCompleted", function (message) {
    alert(message);
});

document.getElementById("stopBtn").addEventListener("click", function (e) {
    hubConnection.invoke("Stop");
});


hubConnection.on("consoleLog", console.log);

hubConnection.on("resetState", function () {
    document.querySelectorAll(".siteElement").forEach(e => e.parentNode.removeChild(e));
});

hubConnection.on("moveTo", function (siteId, stateName, message) {
    console.log("moveTo  " + siteId + "  " + stateName);
    let siteElement = document.getElementById(siteId);
    siteElement.remove();

    let messageEl = document.createElement('span');
    messageEl.classList.add("red");
    messageEl.innerHTML = message;

    siteElement.insertBefore(messageEl, siteElement.children[0]);
    let container = document.getElementById(stateName);
    container.appendChild(siteElement);
});

hubConnection.on("renderSite", function (siteId, url, stateName) {
    let siteElement = document.createElement("p");
    siteElement.innerHTML="<span>" + url + "</span>";
    siteElement.id = siteId;
    siteElement.classList.add("siteElement");
    let container = document.getElementById(stateName);
    container.appendChild(siteElement);
});

hubConnection.on("progressChanged", function (progress) {
    document.querySelector('#progressbar > div').style.width = progress + "%";
});

hubConnection.on("progressGlobalChanged", function (progress) {
    document.querySelector('#progressbarGlobal > div').style.width = progress + "%";
});

hubConnection.start();