// src/IrcChat.Client/wwwroot/js/device-detector.js

/**
 * Détecte si l'appareil est mobile
 * @returns {boolean} - True si mobile
 */
export function isMobileDevice() {
  // Vérifier la largeur de l'écran
  const isMobileWidth = globalThis.innerWidth <= 768;

  // Vérifier le user agent
  const isMobileUserAgent = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

  // Vérifier les événements tactiles
  const hasTouchScreen = ('ontouchstart' in globalThis) || (navigator.maxTouchPoints > 0);

  return isMobileWidth || (isMobileUserAgent && hasTouchScreen);
}

/**
 * Obtient la largeur de l'écran
 * @returns {number} - Largeur en pixels
 */
export function getScreenWidth() {
  return globalThis.innerWidth;
}

/**
 * Écoute les changements de taille d'écran
 * @param {object} dotNetHelper - Référence .NET pour les callbacks
 */
export function attachResizeListener(dotNetHelper) {
  let resizeTimeout;

  window.addEventListener('resize', function () {
    clearTimeout(resizeTimeout);

    resizeTimeout = setTimeout(() => {
      const isMobile = isMobileDevice();
      const width = window.innerWidth;

      if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync('OnScreenSizeChanged', isMobile, width);
      }
    }, 250);
  });
}

/**
 * Détache le listener de redimensionnement
 */
export function detachResizeListener() {
  // En production, stocker la référence de la fonction pour pouvoir la supprimer
  globalThis.removeEventListener('resize', null);
}
