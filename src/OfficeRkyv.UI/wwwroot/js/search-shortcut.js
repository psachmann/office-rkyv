// Opens the search dialog on Ctrl+K (Cmd+K on a Mac), wherever the focus is.
//
// The key is cancelled here, at once: a handler that answered through .NET would be too late to stop the browser's
// own Ctrl+K (a search bar or the address bar). What to do about it is the component's to decide, so the script only
// says whether a dialog is open - Radzen's DialogService has no list of them to ask. A confirm dialog is an
// alertdialog, every other Radzen dialog a dialog.
//
// The page console gets two lines, which is how the desktop app's stdout shows that the key reached the page.
let listener = null;

export function register(dotNetReference) {
    unregister();

    listener = event => {
        if (event.key.toLowerCase() !== 'k' || !(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) {
            return;
        }

        event.preventDefault();
        console.log('search shortcut pressed');

        const aDialogIsOpen = document.querySelector('[role="dialog"], [role="alertdialog"]') !== null;
        dotNetReference.invokeMethodAsync('OnShortcut', aDialogIsOpen);
    };

    document.addEventListener('keydown', listener, true);
    console.log('search shortcut registered');
}

export function unregister() {
    if (listener !== null) {
        document.removeEventListener('keydown', listener, true);
        listener = null;
    }
}

// Puts the focus back into the search dialog's field: the shortcut was pressed while the dialog was open.
export function focusSearchField() {
    document.querySelector('.search-dialog input')?.focus();
}
