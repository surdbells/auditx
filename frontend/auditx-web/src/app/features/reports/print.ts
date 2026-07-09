/**
 * Prints a self-contained report HTML document (inline styles, no external assets) via the browser's print
 * dialog — from which the user can "Save as PDF". Renders the EXACT downloaded artefact bytes in a hidden,
 * same-origin `srcdoc` iframe, so nothing is client re-rendered and the artefact's integrity hash is untouched.
 *
 * The iframe is sandboxed to `allow-same-origin allow-modals` (the parent drives `print()`; the report carries
 * no scripts), and removed once the print dialog closes.
 */
export function printReportHtml(html: string): void {
  const iframe = document.createElement('iframe');
  iframe.setAttribute('sandbox', 'allow-same-origin allow-modals');
  iframe.setAttribute('aria-hidden', 'true');
  Object.assign(iframe.style, {
    position: 'fixed',
    right: '0',
    bottom: '0',
    width: '0',
    height: '0',
    border: '0',
  });
  iframe.srcdoc = html;

  iframe.onload = () => {
    const win = iframe.contentWindow;
    if (!win) {
      iframe.remove();
      return;
    }
    const cleanup = () => iframe.remove();
    win.addEventListener('afterprint', cleanup);
    // Safety net if afterprint never fires (some browsers): reap the iframe after a few minutes.
    setTimeout(cleanup, 5 * 60 * 1000);
    win.focus();
    win.print();
  };

  document.body.appendChild(iframe);
}
