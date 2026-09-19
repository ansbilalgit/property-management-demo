// Loads server-rendered partials into a Bootstrap modal and posts their forms in place.
// A 204 response means success: close the modal and refresh every [data-refresh-url] region.
// Any other HTML response is the same partial re-rendered with validation messages.
(function () {
    const modalEl = document.getElementById('app-modal');
    const contentEl = document.getElementById('app-modal-content');
    if (!modalEl || !contentEl) return;

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

    function show(html) {
        contentEl.innerHTML = html;
        const form = contentEl.querySelector('form');
        if (form && window.jQuery && jQuery.validator) {
            jQuery(form).removeData('validator').removeData('unobtrusiveValidation');
            jQuery.validator.unobtrusive.parse(form);
        }
    }

    async function refreshRegions() {
        const regions = document.querySelectorAll('[data-refresh-url]');
        await Promise.all(Array.from(regions).map(async region => {
            const response = await fetch(region.dataset.refreshUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (response.ok) region.innerHTML = await response.text();
        }));
    }

    document.addEventListener('click', async event => {
        const trigger = event.target.closest('[data-modal-url]');
        if (!trigger) return;

        event.preventDefault();
        const response = await fetch(trigger.dataset.modalUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!response.ok) {
            await refreshRegions();
            return;
        }

        show(await response.text());
        modal.show();
    });

    document.addEventListener('submit', async event => {
        const form = event.target.closest('form[data-modal-form]');
        if (!form) return;

        event.preventDefault();
        if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;

        const submitButtons = form.querySelectorAll('[type=submit]');
        submitButtons.forEach(b => b.disabled = true);

        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form) });

            if (response.status === 204) {
                modal.hide();
                await refreshRegions();
            } else if (response.ok) {
                show(await response.text());
            } else if (response.status === 404) {
                modal.hide();
                await refreshRegions();
            } else {
                throw new Error('Unexpected response ' + response.status);
            }
        } finally {
            submitButtons.forEach(b => b.disabled = false);
        }
    });
})();
