/* Admin area script. Kept in a file (not inline) because the admin's Content-Security-Policy blocks inline scripts.
   Any button or form with a data-confirm attribute asks before it goes ahead. */
document.addEventListener('click', event => {
  const button = event.target.closest('button[data-confirm]');
  if (button && !window.confirm(button.dataset.confirm)) event.preventDefault();
});
document.addEventListener('submit', event => {
  const form = event.target.closest('form[data-confirm]');
  if (form && !window.confirm(form.dataset.confirm)) event.preventDefault();
}, true);
