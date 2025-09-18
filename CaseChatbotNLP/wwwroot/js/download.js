
async function downloadFile() {
    const fileId = document.getElementById('fileId').value;
    const statusElement = document.getElementById('downloadStatus');

    if (!fileId) {
        statusElement.textContent = "⚠️ Ingresa un ID de archivo.";
        return;
    }

    try {
        const response = await fetch(`https://ch-npl-d5djc6cafehnfgf7.eastus-01.azurewebsites.net/Embedding/download?name=${fileId}`, {
            method: 'GET',
        });

        if (!response.ok) throw new Error("Archivo no encontrado");

        // Obtener el blob (para PDF, imágenes, etc.)
        const blob = await response.blob();

        // Crear un enlace de descarga
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `archivo-${fileId}`;  // Nombre del archivo
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);

        statusElement.textContent = "✅ Descarga iniciada.";
    } catch (error) {
        statusElement.textContent = `❌ Error: ${error.message}`;
    }
}