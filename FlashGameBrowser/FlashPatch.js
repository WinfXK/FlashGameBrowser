// FlashGameBrowser - Flash 检测修复脚本
// 注入到每个页面，修复 4399/7k7k 等网站的 Flash 检测
(function(){
    'use strict';

    // 1. 拦截 4399 的 showBlockFlash —— 自动恢复游戏加载
    var _blockTimeout = null;
    window.showBlockFlash = function() {
        console.log('[FlashGameBrowser] 4399 Flash detection failed, auto-recovering...');
        if (_blockTimeout) clearTimeout(_blockTimeout);
        _blockTimeout = setTimeout(function() {
            if (window.closeBlockFlash) {
                window.closeBlockFlash();
                console.log('[FlashGameBrowser] Game iframe restored');
            }
        }, 2000);
    };
    window.showBlockFlashIE = window.showBlockFlash;

    // 2. 修补 navigator.plugins —— 确保 Flash 插件在枚举中可见
    try {
        var hasFlashInPlugins = false;
        var pl = navigator.plugins;
        for (var i = 0; i < pl.length; i++) {
            if (pl[i].name && pl[i].name.indexOf('Shockwave Flash') !== -1) {
                hasFlashInPlugins = true;
                break;
            }
        }
        if (!hasFlashInPlugins && navigator.mimeTypes &&
            navigator.mimeTypes['application/x-shockwave-flash']) {
            var fakeMime = navigator.mimeTypes['application/x-shockwave-flash'];
            var fakePlugin = {
                name: 'Shockwave Flash',
                description: 'Shockwave Flash 34.0 r0',
                filename: 'pepflashplayer.dll',
                length: 1
            };
            fakePlugin[0] = fakeMime;
            var realPlugins = navigator.plugins;
            var allPlugins = [fakePlugin];
            for (var j = 0; j < realPlugins.length; j++) {
                allPlugins.push(realPlugins[j]);
            }
            Object.defineProperty(navigator, 'plugins', {
                get: function() {
                    var arr = { length: allPlugins.length, refresh: function(){} };
                    for (var k = 0; k < allPlugins.length; k++) {
                        arr[k] = allPlugins[k];
                    }
                    arr.item = function(idx) { return allPlugins[idx] || null; };
                    arr.namedItem = function(n) {
                        if (n === 'Shockwave Flash') return fakePlugin;
                        return realPlugins.namedItem ? realPlugins.namedItem(n) : null;
                    };
                    return arr;
                },
                configurable: true
            });
            console.log('[FlashGameBrowser] navigator.plugins polyfill applied');
        }
    } catch(ex) {
        console.log('[FlashGameBrowser] polyfill error:', ex);
    }
})();
