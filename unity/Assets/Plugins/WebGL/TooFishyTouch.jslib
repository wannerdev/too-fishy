// Touch detection for the browser build (see WebTouch.cs).
mergeInto(LibraryManager.library, {
  TF_TouchDetected: function () {
    try {
      if (window.tfTouched) return 1;
      if (window.matchMedia && window.matchMedia('(pointer: coarse)').matches) return 1;
    } catch (e) {}
    return 0;
  }
});