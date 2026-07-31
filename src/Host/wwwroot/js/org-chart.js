/**
 * Organization Chart — pan/zoom, context menu, and HTMX wiring.
 *
 * This module handles:
 * - Mouse-wheel zoom (scale)
 * - Zoom-in/zoom-out/reset buttons
 * - Mouse-drag pan
 * - Auto-fit on load (compute bounding box, set viewBox)
 * - Right-click context menu on SVG nodes
 * - HTMX triggers for dialog and action handlers
 */
(function () {
    'use strict';

    let scale = 1;
    let translateX = 0;
    let translateY = 0;
    let isDragging = false;
    let dragStartX, dragStartY;
    let currentOrgId = null;

    const svg = document.getElementById('org-chart-svg');
    const content = document.getElementById('chart-content');
    const viewport = document.getElementById('chart-viewport');
    const contextMenu = document.getElementById('org-context-menu');

    if (!svg || !content) return;

    // ── Transform ──────────────────────────────────────────

    function applyTransform() {
        content.setAttribute('transform',
            `translate(${translateX}, ${translateY}) scale(${scale})`);
    }

    // ── Zoom ───────────────────────────────────────────────

    function zoom(delta) {
        const prevScale = scale;
        scale = Math.max(0.1, Math.min(5, scale * delta));
        applyTransform();
    }

    document.getElementById('zoom-in-btn')?.addEventListener('click', () => zoom(1.2));
    document.getElementById('zoom-out-btn')?.addEventListener('click', () => zoom(0.8));
    document.getElementById('zoom-reset-btn')?.addEventListener('click', autoFit);

    svg.addEventListener('wheel', function (e) {
        e.preventDefault();
        const delta = e.deltaY > 0 ? 0.9 : 1.1;
        zoom(delta);
    }, { passive: false });

    // ── Pan ────────────────────────────────────────────────

    svg.addEventListener('mousedown', function (e) {
        if (e.button !== 0) return; // Only left-click
        isDragging = true;
        dragStartX = e.clientX - translateX;
        dragStartY = e.clientY - translateY;
        svg.style.cursor = 'grabbing';
    });

    window.addEventListener('mousemove', function (e) {
        if (!isDragging) return;
        translateX = e.clientX - dragStartX;
        translateY = e.clientY - dragStartY;
        applyTransform();
    });

    window.addEventListener('mouseup', function () {
        isDragging = false;
        svg.style.cursor = 'grab';
    });

    // ── Auto-fit ──────────────────────────────────────────

    function autoFit() {
        const bbox = content.getBBox();
        const padding = 40;
        const width = bbox.width + padding * 2;
        const height = bbox.height + padding * 2;

        svg.setAttribute('viewBox',
            `${bbox.x - padding} ${bbox.y - padding} ${width} ${height}`);
        scale = 1;
        translateX = 0;
        translateY = 0;
        applyTransform();
    }

    // Run auto-fit after initial render
    if (content.children.length > 0) {
        requestAnimationFrame(autoFit);
    }

    // ── Context Menu ──────────────────────────────────────

    svg.addEventListener('contextmenu', function (e) {
        const nodeGroup = e.target.closest('[data-org-id]');
        if (!nodeGroup) return;

        e.preventDefault();
        currentOrgId = nodeGroup.getAttribute('data-org-id');

        showContextMenu(e.clientX, e.clientY, currentOrgId);
    });

    document.addEventListener('click', function (e) {
        if (!contextMenu.contains(e.target)) {
            hideContextMenu();
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') hideContextMenu();
    });

    function showContextMenu(x, y, orgId) {
        const node = svg.querySelector(`[data-org-id="${orgId}"]`);
        const isDisabled = node?.getAttribute('data-disabled') === 'true';
        const isRoot = node?.getAttribute('data-root') === 'true';

        let html = '<ul style="list-style:none;padding:4px 0;margin:0;min-width:200px;">';

        html += `<li style="padding:6px 16px;cursor:pointer;" hx-get="/Admin/Organizations/Chart/EditDialog?id=${orgId}" hx-target="#modal-container" onclick="document.getElementById('org-context-menu').style.display='none'">Edit Organization</li>`;

        if (!isRoot) {
            if (isDisabled) {
                html += `<li style="padding:6px 16px;cursor:pointer;" hx-post="/Admin/Organizations/Chart/Enable?id=${orgId}" hx-swap="outerHTML" onclick="document.getElementById('org-context-menu').style.display='none'">Enable Organization</li>`;
            } else {
                html += `<li style="padding:6px 16px;cursor:pointer;" hx-post="/Admin/Organizations/Chart/Disable?id=${orgId}" hx-confirm="Disable this org and all its descendants?" hx-swap="outerHTML" onclick="document.getElementById('org-context-menu').style.display='none'">Disable Organization</li>`;
            }
        }

        html += `<li style="padding:6px 16px;cursor:pointer;" hx-get="/Admin/Organizations/Chart/CreateChildDialog?parentId=${orgId}" hx-target="#modal-container" onclick="document.getElementById('org-context-menu').style.display='none'">Add Child Organization</li>`;

        html += `<li style="padding:6px 16px;cursor:pointer;" hx-get="/Admin/Organizations/Chart/AddUserDialog?orgId=${orgId}" hx-target="#modal-container" onclick="document.getElementById('org-context-menu').style.display='none'">Add New User</li>`;
        html += `<li style="padding:6px 16px;cursor:pointer;" hx-get="/Admin/Organizations/Chart/AssignUserDialog?orgId=${orgId}" hx-target="#modal-container" onclick="document.getElementById('org-context-menu').style.display='none'">Assign Existing User</li>`;
        html += `<li style="padding:6px 16px;cursor:pointer;" hx-get="/Admin/Organizations/Chart/AssignCourseDialog?orgId=${orgId}" hx-target="#modal-container" onclick="document.getElementById('org-context-menu').style.display='none'">Assign Course</li>`;

        html += '</ul>';

        contextMenu.innerHTML = html;
        contextMenu.style.left = x + 'px';
        contextMenu.style.top = y + 'px';
        contextMenu.style.display = 'block';
    }

    function hideContextMenu() {
        contextMenu.style.display = 'none';
        currentOrgId = null;
    }

})();
