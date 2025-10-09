// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Copy buttons for facilitator URLs
document.addEventListener('click', async function (e) {
  var btn = e.target.closest && e.target.closest('.copy-btn');
  if (!btn) return;

  var value = btn.getAttribute('data-copy');
  try {
    await navigator.clipboard.writeText(value);
    var original = btn.textContent;
    btn.textContent = 'Copied!';
    setTimeout(function(){ btn.textContent = original; }, 1200);
  } catch (err) {
    window.prompt('Copy URL:', value);
  }
});