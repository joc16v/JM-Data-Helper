vizrt.onClick = async (name: string) => {
    if (name !== "testHelper") return;

    try {
        const response = await fetch(
            "http://127.0.0.1:8765/api/dhmz/warnings?day=1",
            { cache: "no-store" }
        );

        const data = await response.json();
        console.log("JM DATA HELPER:", data);

        if (!response.ok || !data.ok) {
            throw new Error(data.error || `HTTP ${response.status}`);
        }

        console.table(data.counties);
        console.table(data.sea);
        app.notify("success", `Helper radi - ${data.counties.length} županija`);
    }
    catch (error) {
        console.error("JM Data Helper error:", error);
        app.notify("error", "JM Data Helper nije dostupan");
    }
};
