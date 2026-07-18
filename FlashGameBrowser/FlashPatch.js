// FlashGameBrowser - Flash 检测修复脚本 v3
// 核心思路：4399 通过 document.write 创建 <embed> 并调用其 checkflash() 方法
// 我们提前在 HTMLEmbedElement 原型上添加 checkflash 方法，使检测直接通过
(function(){
    'use strict';
    var TAG = '[FlashGameBrowser]';

    // === 核心修复：让所有 embed/object 元素自带 checkflash 方法 ===
    // 4399 检测: document.getElementById("testplayer1").checkflash()
    // 这个方法会在 Flash SWF 加载后被 ExternalInterface 注册
    // 我们在 DOM 层面直接提供此方法，检测永不失败
    try {
        if (typeof HTMLEmbedElement !== 'undefined') {
            HTMLEmbedElement.prototype.checkflash = function() {
                return 1;
            };
            console.log(TAG + ' HTMLEmbedElement.prototype.checkflash installed');
        }
    } catch(e) {
        console.log(TAG + ' HTMLEmbedElement not available yet: ' + e);
    }

    try {
        if (typeof HTMLObjectElement !== 'undefined') {
            HTMLObjectElement.prototype.checkflash = function() {
                return 1;
            };
            console.log(TAG + ' HTMLObjectElement.prototype.checkflash installed');
        }
    } catch(e) {
        console.log(TAG + ' HTMLObjectElement not available yet: ' + e);
    }

    // === 备用方案：拦截 showBlockFlash (以防它在我们之前被调用) ===
    // 使用 property trap 确保无论何时定义都能拦截
    var blockedOnce = false;
    function patchBlockFlash() {
        if (blockedOnce) return;
        if (typeof window.showBlockFlash === 'function') {
            var origBlock = window.showBlockFlash;
            window.showBlockFlash = function() {
                console.log(TAG + ' showBlockFlash CALLED, attempting recovery...');
                var attempts = 0;
                function tryRestore() {
                    attempts++;
                    if (typeof window.closeBlockFlash === 'function') {
                        window.closeBlockFlash();
                        console.log(TAG + ' closeBlockFlash succeeded on attempt ' + attempts);
                        // Check result
                        var iframe = document.getElementById('flash22');
                        if (iframe) console.log(TAG + ' iframe flash22 src=' + iframe.src);
                    }
                    if (attempts < 8) setTimeout(tryRestore, 1000);
                }
                setTimeout(tryRestore, 500);
            };
            window.showBlockFlashIE = window.showBlockFlash;
            blockedOnce = true;
            console.log(TAG + ' showBlockFlash patched reactively');
        }
    }

    // 立即尝试修补
    patchBlockFlash();

    // 如果 showBlockFlash 还未定义，持续监控直到定义
    if (!blockedOnce) {
        var checkCount = 0;
        var checkInterval = setInterval(function() {
            checkCount++;
            patchBlockFlash();
            if (blockedOnce || checkCount > 30) {
                clearInterval(checkInterval);
                console.log(TAG + ' showBlockFlash monitoring ended (patched=' + blockedOnce + ', checks=' + checkCount + ')');
            }
        }, 100);
    }

    // === 诊断信息 ===
    console.log(TAG + ' injected on ' + window.location.href);
    console.log(TAG + ' navigator.plugins.length=' + navigator.plugins.length);
    console.log(TAG + ' mimeTypes flash=' + !!navigator.mimeTypes['application/x-shockwave-flash']);
    for (var pi = 0; pi < Math.min(navigator.plugins.length, 5); pi++) {
        console.log(TAG + ' plugin[' + pi + ']=' + navigator.plugins[pi].name);
    }

    // 修补 navigator.plugins (如果需要)
    try {
        var hasFP = false;
        var pl = navigator.plugins;
        for (var i = 0; i < pl.length; i++) {
            if (pl[i].name && pl[i].name.indexOf('Shockwave Flash') !== -1) { hasFP = true; break; }
        }
        if (!hasFP && navigator.mimeTypes && navigator.mimeTypes['application/x-shockwave-flash']) {
            var fm = navigator.mimeTypes['application/x-shockwave-flash'];
            var fp = { name: 'Shockwave Flash', description: 'Shockwave Flash 34.0 r0', filename: 'pepflashplayer.dll', length: 1 };
            fp[0] = fm;
            var rp = navigator.plugins;
            var ap = [fp];
            for (var j = 0; j < rp.length; j++) ap.push(rp[j]);
            Object.defineProperty(navigator, 'plugins', {
                get: function() {
                    var arr = { length: ap.length, refresh: function(){} };
                    for (var k = 0; k < ap.length; k++) arr[k] = ap[k];
                    arr.item = function(i) { return ap[i] || null; };
                    arr.namedItem = function(n) { return n === 'Shockwave Flash' ? fp : (rp.namedItem ? rp.namedItem(n) : null); };
                    return arr;
                },
                configurable: true
            });
            console.log(TAG + ' navigator.plugins polyfill applied');
        }
    } catch(ex) {}

    console.log(TAG + ' initialization complete');
})();
