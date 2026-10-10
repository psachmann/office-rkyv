// Hands a stream from the server to the browser as a download. The browser has no API to write a file
// without asking, so the content is collected into a blob and offered through a temporary link.
export async function saveFile(fileName, contentType, streamReference) {
    const buffer = await streamReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer], { type: contentType }));
    const link = document.createElement('a');

    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();

    // The download has started by the time the click returns; the URL is not needed any longer.
    setTimeout(() => URL.revokeObjectURL(url), 0);
}
