async function uploadFile() {
    const fileInput = document.getElementById('fileInput');
    const statusElement = document.getElementById('uploadStatus');

    if (!fileInput.files[0]) {
        statusElement.textContent = "⚠️ No has seleccionado ningún archivo.";
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    try {
        const response = await fetch('https://ch-npl-d5djc6cafehnfgf7.eastus-01.azurewebsites.net/Embedding/upload', {
            method: 'POST',
            body: formData,  // No necesitas headers con FormData
        });

        if (!response.ok) throw new Error("Error en la subida");

        const data = await response.json();
        statusElement.textContent = `✅ Archivo subido. ID: ${data.fileId}`;
    } catch (error) {
        statusElement.textContent = `❌ Error: ${error.message}`;
    }
}