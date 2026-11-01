/* The logo / headline web font is requested without blocking the first paint: its stylesheet starts as media=print and is
   switched on here. A slow or unreachable font host therefore never stalls the page (it keeps the fallback font). */
document.querySelectorAll('link[data-font-swap]').forEach(function (link) { link.media = 'all'; });
