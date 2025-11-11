// src/IrcChat.Client/wwwroot/js/scroll-helper.js

/**
 * Fait défiler un élément jusqu'en bas
 * @param {HTMLElement} element - L'élément à faire défiler
 */
export function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

/**
 * Vérifie si un élément est scrollé jusqu'en bas
 * @param {HTMLElement} element - L'élément à vérifier
 * @returns {boolean} - True si l'élément est en bas
 */
export function isScrolledToBottom(element) {
    if (!element) {
        return true;
    }
    
    const threshold = 10; // pixels de tolérance
    return Math.abs(
        element.scrollHeight - element.clientHeight - element.scrollTop
    ) < threshold;
}

/**
 * Attache un listener pour détecter le scroll manuel
 * @param {HTMLElement} element - L'élément à surveiller
 * @param {object} dotNetHelper - Référence .NET pour les callbacks
 */
export function attachScrollListener(element, dotNetHelper) {
    if (!element) {
        return;
    }

    let scrollTimeout;
    
    element.addEventListener('scroll', function() {
        clearTimeout(scrollTimeout);
        
        scrollTimeout = setTimeout(() => {
            const isAtBottom = Math.abs(
                element.scrollHeight - element.clientHeight - element.scrollTop
            ) < 10;
            
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnUserScroll', isAtBottom);
            }
        }, 150);
    });
}

/**
 * Récupère la position de scroll actuelle
 * @param {HTMLElement} element - L'élément
 * @returns {object} - Position et dimensions
 */
export function getScrollPosition(element) {
    if (!element) {
        return { top: 0, height: 0, scrollHeight: 0 };
    }
    
    return {
        top: element.scrollTop,
        height: element.clientHeight,
        scrollHeight: element.scrollHeight
    };
}