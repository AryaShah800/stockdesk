// StockDesk client scripts: jQuery AJAX live product search and the new-sale form.
(function ($) {
    'use strict';

    var money = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : String(value)).html();
    }

    // Calls fn at most once per `wait` ms after the user stops typing.
    function debounce(fn, wait) {
        var timer;
        return function () {
            var args = arguments, self = this;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(self, args); }, wait);
        };
    }

    // Runs a search and ignores responses that arrive after a newer request was sent.
    function searcher(url) {
        var latest = 0;
        return function (term, onResults) {
            var id = ++latest;
            $.getJSON(url, { term: term })
                .done(function (results) { if (id === latest) { onResults(results); } })
                .fail(function () { if (id === latest) { onResults(null); } });
        };
    }

    // Products page: replace the table body with the search results as the user types.
    var $productSearch = $('#product-search');
    if ($productSearch.length) {
        var $rows = $('#product-rows');
        var original = $rows.html();
        var search = searcher($productSearch.data('search-url'));
        var detailsUrl = $productSearch.data('search-url').replace(/Search$/, 'Details/');

        $productSearch.on('input', debounce(function () {
            var term = $.trim($productSearch.val());
            if (!term) { $rows.html(original); return; }
            search(term, function (results) {
                if (results === null) {
                    $rows.html('<tr><td colspan="6" class="text-danger">Search failed. Try again.</td></tr>');
                    return;
                }
                if (results.length === 0) {
                    $rows.html('<tr><td colspan="6" class="text-muted">No active product matches.</td></tr>');
                    return;
                }
                $rows.html($.map(results, function (p) {
                    return '<tr><td>' + escapeHtml(p.sku) + '</td>' +
                        '<td><a href="' + detailsUrl + p.id + '">' + escapeHtml(p.name) + '</a></td>' +
                        '<td>' + escapeHtml(p.category) + '</td>' +
                        '<td class="text-end">₹' + money.format(p.unitPrice) + '</td>' +
                        '<td class="text-end">' + p.quantityInStock + '</td>' +
                        '<td>' + (p.isLowStock ? '<span class="badge text-bg-warning">Low</span>' : '') + '</td></tr>';
                }).join(''));
            });
        }, 250));
    }

    // New-sale page: search, add lines, keep input names indexed, show running totals.
    var $saleForm = $('#sale-form');
    if ($saleForm.length) {
        var $input = $('#sale-search');
        var $results = $('#sale-search-results');
        var $body = $('#sale-lines tbody');
        var saleSearch = searcher($input.data('search-url'));

        var reindex = function () {
            var total = 0;
            $body.find('tr').each(function (i) {
                var $tr = $(this);
                $tr.find('.line-product').attr('name', 'Lines[' + i + '].ProductId');
                var $qty = $tr.find('.line-qty').attr('name', 'Lines[' + i + '].Quantity');
                var amount = (parseFloat($tr.data('price')) || 0) * (parseInt($qty.val(), 10) || 0);
                $tr.find('.line-amount').text('₹' + money.format(amount));
                total += amount;
            });
            $('#sale-total').text('₹' + money.format(total));
            $('#sale-empty').prop('hidden', $body.find('tr').length > 0);
        };

        var addLine = function (p) {
            var $existing = $body.find('tr[data-product-id="' + p.id + '"]');
            if ($existing.length) {
                var $qty = $existing.find('.line-qty');
                $qty.val((parseInt($qty.val(), 10) || 0) + 1);
            } else {
                $body.append(
                    '<tr data-product-id="' + p.id + '" data-price="' + p.unitPrice + '">' +
                    '<td>' + escapeHtml(p.name) + ' <span class="text-muted small">' + escapeHtml(p.sku) + '</span>' +
                    '<input type="hidden" class="line-product" value="' + p.id + '" /></td>' +
                    '<td class="text-end">₹' + money.format(p.unitPrice) + '</td>' +
                    '<td><input type="number" class="form-control line-qty" value="1" min="1" max="' + p.quantityInStock + '" /></td>' +
                    '<td class="text-end line-amount"></td>' +
                    '<td class="text-end"><button type="button" class="btn btn-sm btn-outline-danger line-remove">Remove</button></td></tr>');
            }
            reindex();
        };

        $input.on('input', debounce(function () {
            var term = $.trim($input.val());
            if (!term) { $results.empty(); return; }
            saleSearch(term, function (items) {
                $results.empty();
                if (items === null) {
                    $results.append('<div class="list-group-item text-danger">Search failed. Try again.</div>');
                    return;
                }
                if (items.length === 0) {
                    $results.append('<div class="list-group-item text-muted">No active product matches.</div>');
                    return;
                }
                $.each(items, function (_, p) {
                    var soldOut = p.quantityInStock <= 0;
                    $('<button type="button" class="list-group-item list-group-item-action d-flex justify-content-between"></button>')
                        .prop('disabled', soldOut)
                        .append($('<span>').text(p.name + ' (' + p.sku + ')'))
                        .append($('<span class="small text-muted">').text('₹' + money.format(p.unitPrice) + ' · ' + (soldOut ? 'out of stock' : p.quantityInStock + ' in stock')))
                        .on('click', function () { addLine(p); $results.empty(); $input.val('').trigger('focus'); })
                        .appendTo($results);
                });
            });
        }, 250));

        $body.on('input', '.line-qty', reindex);
        $body.on('click', '.line-remove', function () { $(this).closest('tr').remove(); reindex(); });
        $(document).on('click', function (e) {
            if (!$(e.target).closest('#sale-search, #sale-search-results').length) { $results.empty(); }
        });
        reindex();
    }
})(jQuery);

// Shell: dark mode toggle (remembered per browser) and the mobile menu.
(function () {
    'use strict';
    var root = document.documentElement;
    var toggle = document.getElementById('theme-toggle');
    if (toggle) {
        toggle.addEventListener('click', function () {
            var next = root.dataset.theme === 'dark' ? 'light' : 'dark';
            root.dataset.theme = next;
            try { localStorage.setItem('sd-theme', next); } catch (e) { /* storage may be blocked */ }
        });
    }
    var side = document.getElementById('side'), scrim = document.getElementById('scrim'), menu = document.getElementById('menu-toggle');
    function setOpen(open) { if (side) { side.classList.toggle('open', open); } if (scrim) { scrim.classList.toggle('open', open); } }
    if (menu) { menu.addEventListener('click', function () { setOpen(true); }); }
    if (scrim) { scrim.addEventListener('click', function () { setOpen(false); }); }
})();
