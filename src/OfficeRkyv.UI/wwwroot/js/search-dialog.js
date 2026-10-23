// The keyboard path through the search dialog, handled in the page and not through .NET so that it needs no round
// trip: Down in the field moves the focus to the first result, Down and Up move through the results, and Up on the
// first one goes back to the field. Enter in the field chooses the first result, as a click on it does.
//
// A result's open button carries data-search-result. Enter on a focused button is the button's own.
export function listenForResultKeys(container) {
    container.addEventListener('keydown', event => {
        const field = container.querySelector('input');
        const results = [...container.querySelectorAll('[data-search-result]')];
        const index = results.indexOf(document.activeElement);

        if (event.key === 'Enter' && document.activeElement === field && results.length > 0) {
            event.preventDefault();
            results[0].click();
        } else if (event.key === 'ArrowDown' && document.activeElement === field && results.length > 0) {
            event.preventDefault();
            results[0].focus();
        } else if (event.key === 'ArrowDown' && index >= 0 && index < results.length - 1) {
            event.preventDefault();
            results[index + 1].focus();
        } else if (event.key === 'ArrowUp' && index === 0) {
            event.preventDefault();
            field.focus();
        } else if (event.key === 'ArrowUp' && index > 0) {
            event.preventDefault();
            results[index - 1].focus();
        }
    });
}
