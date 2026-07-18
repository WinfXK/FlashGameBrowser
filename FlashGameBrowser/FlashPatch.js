// FlashGameBrowser - Flash 检测修复脚本 v4
(function(){
    'use strict';
    var TAG = '[FlashGameBrowser]';
    var url = window.location.href;

    // === 核心修复：HTMLEmbedElement.prototype.checkflash ===
    try {
        if (typeof HTMLEmbedElement !== 'undefined' && !HTMLEmbedElement.prototype.checkflash) {
            HTMLEmbedElement.prototype.checkflash = function() { return 1; };
            console.log(TAG + ' HTMLEmbedElement.prototype.checkflash installed');
        }
    } catch(e) {}

    try {
        if (typeof HTMLObjectElement !== 'undefined' && !HTMLObjectElement.prototype.checkflash) {
            HTMLObjectElement.prototype.checkflash = function() { return 1; };
            console.log(TAG + ' HTMLObjectElement.prototype.checkflash installed');
        }
    } catch(e) {}

    // === 4399 页面: 拦截 showBlockFlash ===
    var blockedOnce = false;
    function patchBlockFlash() {
        if (blockedOnce) return;
        if (typeof window.showBlockFlash === 'function') {
            var origBlock = window.showBlockFlash;
            window.showBlockFlash = function() {
                console.log(TAG + ' showBlockFlash CALLED, recovering...');
                var attempts = 0;
                function tryRestore() {
                    attempts++;
                    if (typeof window.closeBlockFlash === 'function') {
                        window.closeBlockFlash();
                        console.log(TAG + ' closeBlockFlash attempt ' + attempts);
                    }
                    if (attempts < 8) setTimeout(tryRestore, 1000);
                }
                setTimeout(tryRestore, 500);
            };
            window.showBlockFlashIE = window.showBlockFlash;
            blockedOnce = true;
            console.log(TAG + ' showBlockFlash patched');
        }
    }
    patchBlockFlash();
    if (!blockedOnce) {
        var checkCount = 0;
        var checkInterval = setInterval(function() {
            checkCount++;
            patchBlockFlash();
            if (blockedOnce || checkCount > 30) clearInterval(checkInterval);
        }, 100);
    }

    // === Tencent 游戏页面：防止 flashdown 重定向 + 强制加载 SWF ===
    if (url.indexOf('17roco.qq.com') !== -1) {
        console.log(TAG + ' Tencent game page detected');

        // 1. 全局设置 flashready，防止 11 秒后重定向到 /cgi-bin/login
        window.flashready = 1;

        // 2. 拦截 confirm 对话框（flashdown 中的 confirm 调用）
        var origConfirm = window.confirm;
        window.confirm = function(msg) {
            console.log(TAG + ' confirm intercepted: ' + msg);
            if (msg.indexOf('flash') !== -1 || msg.indexOf('异常') !== -1) {
                return false; // 拒绝，不重定向
            }
            return origConfirm.call(window, msg);
        };

        // 3. 页面加载后检查 embed 是否存在并强制重建
        function fixEmbed() {
            var objects = document.getElementsByTagName('object');
            var embeds = document.getElementsByTagName('embed');
            console.log(TAG + ' objects=' + objects.length + ' embeds=' + embeds.length);

            // 查找 rocoswf 相关的元素
            for (var i = 0; i < embeds.length; i++) {
                var e = embeds[i];
                console.log(TAG + ' embed[' + i + '] id=' + e.id + ' src=' + e.src + ' type=' + e.type);
                if (!e.src && !e.getAttribute('src')) {
                    // embed 可能没有正确设置 src
                    var src = e.getAttribute('src');
                    console.log(TAG + ' embed raw src attr: ' + src);
                }
            }

            // 如果 SWF embed 存在但没有加载，尝试强制重建
            var swfEmbed = document.getElementById('rocoswf-inner');
            if (!swfEmbed) {
                // 没有找到 rocoswf-inner，可能 embed 还没被 document.write 创建
                console.log(TAG + ' rocoswf-inner not found yet, will retry');
                return false;
            }

            console.log(TAG + ' rocoswf-inner found, src=' + swfEmbed.src);

            // 如果 embed.src 为空，尝试从属性重建
            if (!swfEmbed.src || swfEmbed.src === '') {
                var swfSrc = swfEmbed.getAttribute('src');
                console.log(TAG + ' src attribute: ' + swfSrc);
                if (swfSrc) {
                    // 重建 SWF 加载
                    swfEmbed.setAttribute('src', swfSrc);
                    console.log(TAG + ' forced src re-set');
                }
            }

            return true;
        }

        // 先等页面脚本执行（document.write 创建 embed）
        setTimeout(function() {
            var found = fixEmbed();
            if (!found) {
                // 还没创建，再等
                setTimeout(function() {
                    if (!fixEmbed()) {
                        setTimeout(function() { fixEmbed(); }, 1000);
                    }
                }, 500);
            }
        }, 500);
    }

    // === 诊断信息 ===
    console.log(TAG + ' injected on ' + url);
    console.log(TAG + ' plugins=' + navigator.plugins.length + ' mimeTypes flash=' + !!navigator.mimeTypes['application/x-shockwave-flash']);
    for (var pi = 0; pi < Math.min(navigator.plugins.length, 5); pi++) {
        console.log(TAG + ' plugin[' + pi + ']=' + navigator.plugins[pi].name);
    }

    // === navigator.plugins polyfill ===
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
            console.log(TAG + ' plugins polyfill applied');
        }
    } catch(ex) {}
})();
