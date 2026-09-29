// Applies an appearance choice to the page that is already showing, and remembers it for a year.
//
// The theme <link>s are rendered once by the server (see AppTheme), one per theme and marked with
// data-theme. Changing the choice only changes which of them applies, through their media, so nothing is
// added, removed or downloaded and the page never shows unstyled content in between.
//
// The server reads the cookie on the next page load, so that page is rendered in the chosen appearance
// from its first paint.
export function applyAppearance(cookieName, cookieValue, sheets) {
    for (const { theme, media } of sheets) {
        const link = document.querySelector(`link[data-theme="${theme}"]`);
        if (link) {
            link.media = media ?? 'all';
        }
    }

    document.cookie = `${cookieName}=${encodeURIComponent(cookieValue)}; max-age=31536000; path=/; SameSite=Lax`;
}
